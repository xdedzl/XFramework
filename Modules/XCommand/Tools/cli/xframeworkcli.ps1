Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$script:ProtocolVersion = 1
$script:RegistrationDirectory = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'XFrameworkCLI\servers'

function ConvertTo-CliJson {
    param([object]$Value, [bool]$Pretty)
    if ($Pretty) {
        return $Value | ConvertTo-Json -Depth 64
    }
    return $Value | ConvertTo-Json -Depth 64 -Compress
}

function Exit-CliError {
    param([int]$ExitCode, [string]$Status, [string]$Message, [bool]$Pretty)
    $response = [ordered]@{
        protocolVersion = $script:ProtocolVersion
        requestId = $null
        exitCode = $ExitCode
        status = $Status
        message = $Message
    }
    [Console]::Error.WriteLine($Message)
    [Console]::Out.WriteLine((ConvertTo-CliJson $response $Pretty))
    exit $ExitCode
}

function Get-CliOptions {
    param([string[]]$Tokens, [string[]]$Allowed)
    $options = [ordered]@{
        processId = $null
        port = $null
        target = $null
        timeoutSeconds = 60.0
        noWait = $false
        confirm = $false
        availableOnly = $false
        pretty = $false
    }
    for ($index = 0; $index -lt $Tokens.Count; $index++) {
        $name = $Tokens[$index]
        if ($Allowed -notcontains $name) {
            Exit-CliError 2 'InvalidArguments' "Unknown option: $name" $options.pretty
        }
        switch ($name) {
            '--pid' {
                if (++$index -ge $Tokens.Count) {
                    Exit-CliError 2 'InvalidArguments' '--pid requires a value.' $options.pretty
                }
                $parsedProcessId = 0
                if (-not [int]::TryParse($Tokens[$index], [ref]$parsedProcessId) -or $parsedProcessId -le 0) {
                    Exit-CliError 2 'InvalidArguments' '--pid must be a positive integer.' $options.pretty
                }
                $options.processId = $parsedProcessId
            }
            '--port' {
                if (++$index -ge $Tokens.Count) {
                    Exit-CliError 2 'InvalidArguments' '--port requires a value.' $options.pretty
                }
                $parsedPort = 0
                if (-not [int]::TryParse($Tokens[$index], [ref]$parsedPort) -or $parsedPort -lt 1 -or $parsedPort -gt 65535) {
                    Exit-CliError 2 'InvalidArguments' '--port must be between 1 and 65535.' $options.pretty
                }
                $options.port = $parsedPort
            }
            '--target' {
                if (++$index -ge $Tokens.Count) {
                    Exit-CliError 2 'InvalidArguments' '--target requires editor or player.' $options.pretty
                }
                $target = $Tokens[$index].ToLowerInvariant()
                if ($target -ne 'editor' -and $target -ne 'player') {
                    Exit-CliError 2 'InvalidArguments' '--target requires editor or player.' $options.pretty
                }
                $options.target = $target
            }
            '--timeout' {
                if (++$index -ge $Tokens.Count) {
                    Exit-CliError 2 'InvalidArguments' '--timeout requires a value in seconds.' $options.pretty
                }
                try {
                    $parsedTimeout = [double]::Parse($Tokens[$index], [Globalization.CultureInfo]::InvariantCulture)
                }
                catch {
                    Exit-CliError 2 'InvalidArguments' '--timeout must be a number greater than zero.' $options.pretty
                }
                if ($parsedTimeout -le 0) {
                    Exit-CliError 2 'InvalidArguments' '--timeout must be a number greater than zero.' $options.pretty
                }
                $options.timeoutSeconds = $parsedTimeout
            }
            '--no-wait' { $options.noWait = $true }
            '--confirm' { $options.confirm = $true }
            '--available' { $options.availableOnly = $true }
            '--pretty' { $options.pretty = $true }
        }
    }
    return [pscustomobject]$options
}

function Remove-StaleRegistration {
    param([string]$Path)
    $trimCharacters = [char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $root = [IO.Path]::GetFullPath($script:RegistrationDirectory).TrimEnd($trimCharacters) + [IO.Path]::DirectorySeparatorChar
    $target = [IO.Path]::GetFullPath($Path)
    if ($target.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $target -Force
    }
}

function Get-LiveCliServers {
    if (-not (Test-Path -LiteralPath $script:RegistrationDirectory -PathType Container)) {
        return @()
    }

    $servers = New-Object System.Collections.Generic.List[object]
    foreach ($file in Get-ChildItem -LiteralPath $script:RegistrationDirectory -Filter '*.json' -File) {
        try {
            $registration = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        }
        catch {
            [Console]::Error.WriteLine("Ignoring invalid XFramework CLI registration: $($file.FullName)")
            continue
        }

        $process = Get-Process -Id ([int]$registration.pid) -ErrorAction SilentlyContinue
        if ($null -eq $process) {
            Remove-StaleRegistration $file.FullName
            continue
        }
        try {
            $processStartTimeUtcTicks = $process.StartTime.ToUniversalTime().Ticks
        }
        catch {
            Remove-StaleRegistration $file.FullName
            continue
        }
        if ($processStartTimeUtcTicks -ne [long]$registration.processStartTimeUtcTicks) {
            Remove-StaleRegistration $file.FullName
            continue
        }
        $registration | Add-Member -NotePropertyName registrationPath -NotePropertyValue $file.FullName -Force
        $servers.Add($registration)
    }
    return $servers.ToArray()
}

function Test-ProjectPathMatch {
    param([string]$CurrentPath, [string]$ProjectPath)
    $trimCharacters = [char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $current = [IO.Path]::GetFullPath($CurrentPath).TrimEnd($trimCharacters)
    $project = [IO.Path]::GetFullPath($ProjectPath).TrimEnd($trimCharacters)
    if ($current.Equals($project, [StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }
    return $current.StartsWith($project + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

function Select-CliServer {
    param([object]$Options)
    if ($null -ne $Options.port) {
        return [pscustomobject]@{
            pid = 0
            port = $Options.port
            processType = 'ExplicitPort'
            projectPath = ''
        }
    }

    $servers = @(Get-LiveCliServers)
    if ($null -ne $Options.target) {
        $processType = if ($Options.target -eq 'editor') { 'Editor' } else { 'DevelopmentPlayer' }
        $servers = @($servers | Where-Object { $_.processType -eq $processType })
    }
    if ($null -ne $Options.processId) {
        $servers = @($servers | Where-Object { [int]$_.pid -eq $Options.processId })
        if ($servers.Count -ne 1) {
            Exit-CliError 3 'ServerNotFound' "No live XFramework CLI Server matched PID $($Options.processId)." $Options.pretty
        }
        return $servers[0]
    }

    $currentPath = (Get-Location).Path
    $projectMatches = @($servers | Where-Object { Test-ProjectPathMatch $currentPath ([string]$_.projectPath) })
    if ($projectMatches.Count -eq 1) {
        return $projectMatches[0]
    }
    if ($projectMatches.Count -gt 1) {
        Exit-CliError 3 'AmbiguousServer' 'Multiple XFramework CLI Servers match the current project. Use --target, --pid, or --port.' $Options.pretty
    }
    if ($servers.Count -eq 1) {
        return $servers[0]
    }
    if ($servers.Count -eq 0) {
        Exit-CliError 3 'ServerNotFound' 'No live XFramework CLI Server was found.' $Options.pretty
    }
    Exit-CliError 3 'AmbiguousServer' 'Multiple XFramework CLI Servers are running. Use --target, --pid, or --port.' $Options.pretty
}

function New-CliRequest {
    param([string]$Action, [string]$CommandLine, [object]$Options)
    return [ordered]@{
        protocolVersion = $script:ProtocolVersion
        requestId = [Guid]::NewGuid().ToString('N')
        action = $Action
        commandLine = $CommandLine
        waitForOperation = -not $Options.noWait
        timeoutSeconds = $Options.timeoutSeconds
        confirm = $Options.confirm
        availableOnly = $Options.availableOnly
    }
}

function Invoke-CliRequest {
    param([object]$Server, [object]$Request, [double]$TimeoutSeconds)
    $client = $null
    $reader = $null
    $writer = $null
    try {
        $client = [Net.Sockets.TcpClient]::new()
        $connectTask = $client.ConnectAsync('127.0.0.1', [int]$Server.port)
        if (-not $connectTask.Wait(5000)) {
            throw "Timed out connecting to XFramework CLI Server on port $($Server.port)."
        }
        $client.ReceiveTimeout = [int]([Math]::Ceiling(($TimeoutSeconds + 5.0) * 1000.0))
        $client.SendTimeout = 5000
        $stream = $client.GetStream()
        $utf8 = [Text.UTF8Encoding]::new($false)
        $reader = [IO.StreamReader]::new($stream, $utf8, $false, 1024, $true)
        $writer = [IO.StreamWriter]::new($stream, $utf8, 1024, $true)
        $writer.NewLine = "`n"
        $writer.AutoFlush = $true
        $writer.WriteLine((ConvertTo-Json $Request -Depth 32 -Compress))
        $responseJson = $reader.ReadLine()
        if ([string]::IsNullOrWhiteSpace($responseJson)) {
            throw 'XFramework CLI Server closed the connection without a response.'
        }
        $response = $responseJson | ConvertFrom-Json
        return [pscustomobject]@{
            Json = $responseJson
            Response = $response
        }
    }
    finally {
        if ($null -ne $writer) { $writer.Dispose() }
        if ($null -ne $reader) { $reader.Dispose() }
        if ($null -ne $client) { $client.Dispose() }
    }
}

function Write-CliResponse {
    param([object]$Result, [bool]$Pretty)
    if ($Pretty) {
        [Console]::Out.WriteLine((ConvertTo-CliJson $Result.Response $true))
    }
    else {
        [Console]::Out.WriteLine($Result.Json)
    }
    if ([int]$Result.Response.exitCode -ne 0) {
        [Console]::Error.WriteLine([string]$Result.Response.message)
    }
    exit ([int]$Result.Response.exitCode)
}

function Write-CliHelp {
    $help = [ordered]@{
        protocolVersion = $script:ProtocolVersion
        exitCode = 0
        status = 'Succeeded'
        usage = @(
            'xframeworkcli.ps1 help',
            'xframeworkcli.ps1 servers [--pretty]',
            'xframeworkcli.ps1 ping [--pid PID|--port PORT] [--target editor|player] [--pretty]',
            'xframeworkcli.ps1 commands [--available] [--pid PID|--port PORT] [--target editor|player] [--pretty]',
            'xframeworkcli.ps1 diagnostics [--pid PID|--port PORT] [--target editor|player] [--pretty]',
            "xframeworkcli.ps1 exec '<complete GM command line>' [--no-wait] [--timeout SECONDS] [--confirm] [--pid PID|--port PORT] [--target editor|player] [--pretty]"
        )
        examples = @(
            "xframeworkcli.ps1 exec 'recompile' --target editor",
            "xframeworkcli.ps1 exec 'play-start'",
            "xframeworkcli.ps1 exec 'ui-list --compact'",
            "xframeworkcli.ps1 exec 'ui-act --name StartButton click'",
            "xframeworkcli.ps1 exec 'screenshot temp:/autotest.png'"
        )
    }
    [Console]::Out.WriteLine((ConvertTo-CliJson $help $false))
    exit 0
}

$cliArguments = @($args)
if ($cliArguments.Count -eq 0) {
    Write-CliHelp
}

$verb = $cliArguments[0].ToLowerInvariant()
switch ($verb) {
    'help' {
        Write-CliHelp
    }
    'servers' {
        $optionTokens = if ($cliArguments.Count -gt 1) { @($cliArguments[1..($cliArguments.Count - 1)]) } else { @() }
        $options = Get-CliOptions $optionTokens @('--pretty')
        $response = [ordered]@{
            protocolVersion = $script:ProtocolVersion
            requestId = $null
            exitCode = 0
            status = 'Succeeded'
            servers = @(Get-LiveCliServers)
        }
        [Console]::Out.WriteLine((ConvertTo-CliJson $response $options.pretty))
        exit 0
    }
    'ping' {
        $optionTokens = if ($cliArguments.Count -gt 1) { @($cliArguments[1..($cliArguments.Count - 1)]) } else { @() }
        $options = Get-CliOptions $optionTokens @('--pid', '--port', '--target', '--pretty')
        $server = Select-CliServer $options
        try {
            $result = Invoke-CliRequest $server (New-CliRequest 'ping' $null $options) 5.0
        }
        catch {
            Exit-CliError 4 'TransportError' $_.Exception.Message $options.pretty
        }
        Write-CliResponse $result $options.pretty
    }
    'commands' {
        $optionTokens = if ($cliArguments.Count -gt 1) { @($cliArguments[1..($cliArguments.Count - 1)]) } else { @() }
        $options = Get-CliOptions $optionTokens @('--pid', '--port', '--target', '--available', '--pretty')
        $server = Select-CliServer $options
        try {
            $result = Invoke-CliRequest $server (New-CliRequest 'commands' $null $options) 5.0
        }
        catch {
            Exit-CliError 4 'TransportError' $_.Exception.Message $options.pretty
        }
        Write-CliResponse $result $options.pretty
    }
    'diagnostics' {
        $optionTokens = if ($cliArguments.Count -gt 1) { @($cliArguments[1..($cliArguments.Count - 1)]) } else { @() }
        $options = Get-CliOptions $optionTokens @('--pid', '--port', '--target', '--pretty')
        $server = Select-CliServer $options
        try {
            $result = Invoke-CliRequest $server (New-CliRequest 'diagnostics' $null $options) 5.0
        }
        catch {
            Exit-CliError 4 'TransportError' $_.Exception.Message $options.pretty
        }
        Write-CliResponse $result $options.pretty
    }
    'exec' {
        if ($cliArguments.Count -lt 2 -or [string]::IsNullOrWhiteSpace($cliArguments[1])) {
            Exit-CliError 2 'InvalidArguments' 'exec requires the complete GM command line as one quoted argument.' $false
        }
        $commandLine = $cliArguments[1]
        $optionTokens = if ($cliArguments.Count -gt 2) { @($cliArguments[2..($cliArguments.Count - 1)]) } else { @() }
        $options = Get-CliOptions $optionTokens @('--pid', '--port', '--target', '--timeout', '--no-wait', '--confirm', '--pretty')
        $server = Select-CliServer $options
        try {
            $result = Invoke-CliRequest $server (New-CliRequest 'execute' $commandLine $options) $options.timeoutSeconds
        }
        catch {
            Exit-CliError 4 'TransportError' $_.Exception.Message $options.pretty
        }
        Write-CliResponse $result $options.pretty
    }
    default {
        Exit-CliError 2 'InvalidArguments' "Unknown command: $verb" $false
    }
}
