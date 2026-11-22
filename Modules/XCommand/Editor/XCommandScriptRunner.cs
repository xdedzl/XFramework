using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using XFramework.Command;

namespace XFramework.Editor
{
    /// <summary>
    /// 临时 C# 代码执行（cs-run，仅编辑器）：把一段完整 C# 代码编译成程序集后立即加载执行，
    /// 不触发脚本域重载。代码需自带 using 与 public static 无参入口方法 Run。
    /// </summary>
    internal static class XCommandScriptRunner
    {
        internal const string Usage = "cs-run --file PATH | cs-run --code \"CODE\"";

        private const string EntryMethodName = "Run";
        private const string GeneratedSourceName = "XCommandScript.cs";
        private const string GeneratedAssemblyName = "XCommandScript.dll";

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        internal static object Execute(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, new[] { "file", "code" }, Array.Empty<string>());
            args.RequireValueCount(0, $"usage: {Usage}");

            string file = args.GetString("file");
            string code = args.GetString("code");
            if (string.IsNullOrEmpty(file) == string.IsNullOrEmpty(code))
                throw new ArgumentException($"必须且只能提供 --file 或 --code 之一。usage: {Usage}");

            string source = code ?? File.ReadAllText(ResolveSourcePath(file));
            string outputDirectory = Path.Combine(ProjectRoot, "Temp", "XCommandScripts");
            Directory.CreateDirectory(outputDirectory);
            string sourcePath = Path.Combine(outputDirectory, GeneratedSourceName);
            string assemblyPath = Path.Combine(outputDirectory, GeneratedAssemblyName);
            File.WriteAllText(sourcePath, source, new UTF8Encoding(false));

            Compile(sourcePath, assemblyPath);
            return Invoke(assemblyPath) ?? "OK";
        }

        private static string ResolveSourcePath(string path)
        {
            if (path.StartsWith("persistent:/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("data:/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("temp:/", StringComparison.OrdinalIgnoreCase))
                return XCommandUtility.Path.ResolvePath(path);
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(ProjectRoot, path));
        }

        private static void Compile(string sourcePath, string assemblyPath)
        {
            string contentsPath = EditorApplication.applicationContentsPath;
            string dotnetPath = Path.Combine(contentsPath, "NetCoreRuntime",
                Application.platform == RuntimePlatform.WindowsEditor ? "dotnet.exe" : "dotnet");
            string compilerPath = Path.Combine(contentsPath, "DotNetSdkRoslyn", "csc.dll");

            List<string> arguments = new List<string>
            {
                Quote(compilerPath),
                "-nologo",
                "-nostdlib",
                "-noconfig",
                "-langversion:latest",
                "-target:library",
                "-out:" + Quote(assemblyPath),
            };
            foreach (string reference in GetReferences())
                arguments.Add("-r:" + Quote(reference));
            arguments.Add(Quote(sourcePath));

            ProcessStartInfo startInfo = new ProcessStartInfo(dotnetPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = string.Join(" ", arguments),
            };

            using (Process process = Process.Start(startInfo))
            {
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                string output = (outputTask.GetAwaiter().GetResult() + errorTask.GetAwaiter().GetResult()).Trim();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"C# 编译失败：\n{output}");
            }
        }

        private static List<string> GetReferences()
        {
            string contentsPath = EditorApplication.applicationContentsPath;
            List<string> references = new List<string>
            {
                Path.Combine(contentsPath, "NetStandard", "ref", "2.1.0", "netstandard.dll"),
            };
            foreach (string path in Directory.GetFiles(Path.Combine(contentsPath, "Managed", "UnityEngine"), "*.dll"))
            {
                // UnityEditor.dll 与 UnityEditor.*Module.dll 类型重复，只引用后者
                if (Path.GetFileName(path) != "UnityEditor.dll")
                    references.Add(path);
            }
            references.AddRange(Directory.GetFiles(Path.Combine(ProjectRoot, "Library", "ScriptAssemblies"), "*.dll"));
            return references;
        }

        private static object Invoke(string assemblyPath)
        {
            Assembly assembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
            MethodInfo entry = FindEntry(assembly);
            try
            {
                return entry.Invoke(null, null);
            }
            catch (TargetInvocationException exception)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private static MethodInfo FindEntry(Assembly assembly)
        {
            MethodInfo entry = null;
            foreach (Type type in assembly.GetTypes())
            {
                MethodInfo method = type.GetMethod(EntryMethodName, BindingFlags.Public | BindingFlags.Static,
                    null, Type.EmptyTypes, null);
                if (method == null)
                    continue;
                if (entry != null)
                    throw new InvalidOperationException(
                        $"找到多个入口：{entry.ReflectedType.FullName}.{EntryMethodName} 与 {type.FullName}.{EntryMethodName}，请只保留一个 public static 无参 {EntryMethodName} 方法。");
                entry = method;
            }

            return entry ?? throw new InvalidOperationException($"未找到入口，请在代码里定义 public static 无参方法 {EntryMethodName}()。");
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
        }
    }
}
