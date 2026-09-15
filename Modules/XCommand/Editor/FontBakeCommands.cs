using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using XFramework.Command;

namespace XFramework.Editor
{
    /// <summary>
    /// TMP 字体图集烘焙命令（XFramework CLI）。
    ///
    /// 背景：项目主字体通常是按已有文案预烘焙的小字符集，新加的中文文案只要用到图集里没有的字，
    /// 就会整字不显示（TMP 缺字直接不绘制）。这条命令把「项目里实际用到的字符」扫出来，
    /// 与图集做差集，把缺的字补进去，省去每次手工打开 Font Asset Creator。
    ///
    /// 默认字体取 TMP Settings 里配置的默认字体，可以用 --font 指定；默认字符集文件与代码扫描目录
    /// 都是约定路径，项目没有对应文件时用 --chars-file / --code-root 覆盖即可。
    ///
    /// 用法：
    ///   xframeworkcli.ps1 exec 'font_bake' --target editor                     只报告缺字，不修改
    ///   xframeworkcli.ps1 exec 'font_bake --apply' --target editor             补字并保存
    ///   xframeworkcli.ps1 exec 'font_bake --chars "新增文案" --apply' --target editor
    ///   xframeworkcli.ps1 exec 'font_bake --all-common --apply --timeout 900' --target editor
    /// </summary>
    public sealed class FontBakeCommands : XCommandGroup
    {
        /// <summary>约定字符集文件：--all-common 未配合 --chars-file 时使用，缺失只报告不报错。</summary>
        private const string DefaultCharsFilePath = "Assets/Font/3500字完整版.txt";

        /// <summary>默认代码扫描目录：--include-code 未配合 --code-root 时使用。</summary>
        private const string DefaultCodeScanRoot = "Assets/Scripts";

        /// <summary>每批提交给 TMP 的字符数。SDF 逐字光栅化，分批可以避免一次卡太久。</summary>
        private const int BakeChunkSize = 256;

        /// <summary>报告里最多列出的字符数。</summary>
        private const int PreviewLimit = 200;

        private static readonly string[] ScanExtensions = { ".prefab", ".unity", ".asset", ".json" };

        /// <summary>默认跳过的第三方/非文案目录，避免把无关字符烘进图集。</summary>
        private static readonly string[] ExcludedFolders =
        {
            "Assets/TextMesh Pro/",
            "Assets/WX-WASM-SDK-V2/",
            "Assets/Plugins/"
        };

        /// <summary>匹配 YAML 里的 m_text / m_Text 取值（带引号或裸值）。</summary>
        private static readonly Regex TextValueRegex =
            new Regex("m_[tT]ext:[ \\t]*(?:\"((?:[^\"\\\\]|\\\\.)*)\"|([^\\r\\n]*))", RegexOptions.Compiled);

        /// <summary>匹配 C# 里的字符串字面量。</summary>
        private static readonly Regex CodeStringRegex =
            new Regex("\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

        [XCommandEntry("font_bake",
            name = "烘焙 TMP 字体图集",
            order = 20,
            mode = XCommandMode.Editor,
            category = "Font",
            description = "扫描项目中 TMP 文本用到的字符，把字体图集里缺失的字补进去。默认只报告（dry-run），加 --apply 才会写入字体资产；字体默认取 TMP Settings 的默认字体。字符很多时请配合 CLI 的 --timeout（例如 --timeout 900）。",
            usage = "font_bake [--apply] [--chars \"文本\"] [--chars-file 路径] [--all-common] [--font 路径] [--include-code] [--code-root 路径] [--no-scan] [--include-all] [--list]")]
        private static object BakeTmpFont(string argument)
        {
            BakeOptions options = BakeOptions.Parse(argument);
            string fontPath = string.IsNullOrEmpty(options.FontPath) ? ResolveDefaultFontPath() : options.FontPath;
            if (string.IsNullOrEmpty(fontPath))
            {
                return "未指定字体资产，且 TMP Settings 里没有配置默认字体。请用 --font <Assets/...> 指定要烘焙的字体资产。";
            }

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (fontAsset == null)
            {
                return $"未找到字体资产：{fontPath}";
            }

            fontAsset.ReadFontAssetDefinition();

            var log = new StringBuilder();
            log.AppendLine("===== TMP 字体图集烘焙 =====");
            log.AppendLine($"字体资产：{fontPath}");
            log.AppendLine($"Atlas Population Mode：{fontAsset.atlasPopulationMode}　Multi Atlas：{fontAsset.isMultiAtlasTexturesEnabled}　图集张数：{fontAsset.atlasTextures.Length}");
            log.AppendLine($"图集已有字形：{CharacterLookupKeys(fontAsset).Count}");

            if (options.ListOnly)
            {
                log.AppendLine("已有字符：" + DescribeCharacters(CharacterLookupKeys(fontAsset)));
                return log.ToString();
            }

            var collected = new SortedSet<char>();

            if (!options.NoScan)
            {
                int scannedFiles = ScanProjectText(collected, options.IncludeAll);
                log.AppendLine($"扫描 YAML 资源：{scannedFiles} 个文件");
            }

            string charsFile = options.CharsFile;
            if (string.IsNullOrEmpty(charsFile) && options.AllCommon)
            {
                charsFile = DefaultCharsFilePath;
            }
            if (!string.IsNullOrEmpty(charsFile))
            {
                if (TryReadCharactersFile(charsFile, collected, out int fileChars, out string fileError))
                {
                    log.AppendLine($"字符集文件：{charsFile}（新增 {fileChars} 个字符）");
                }
                else
                {
                    log.AppendLine($"字符集文件读取失败：{fileError}");
                }
            }

            if (options.IncludeCode)
            {
                int codeChars = ScanCodeText(collected, options.CodeRoot);
                log.AppendLine($"扫描 C# 字符串：新增 {codeChars} 个中文字符");
            }

            if (!string.IsNullOrEmpty(options.Chars))
            {
                int added = AddCharacters(collected, Unescape(options.Chars));
                log.AppendLine($"命令行指定字符：新增 {added} 个");
            }

            var missing = new List<char>();
            foreach (char c in collected)
            {
                if (!IsIgnorable(c) && !HasCharacter(fontAsset, c))
                {
                    missing.Add(c);
                }
            }

            log.AppendLine($"项目用字：{collected.Count} 个唯一字符，其中图集缺失 {missing.Count} 个");

            if (missing.Count == 0)
            {
                log.AppendLine("图集已覆盖全部字符，无需烘焙。");
                return log.ToString();
            }

            log.AppendLine("缺失字符：" + DescribeCharacters(missing));

            if (!options.Apply)
            {
                log.AppendLine("（dry-run：未修改任何资产，确认无误后加 --apply 执行烘焙）");
                return log.ToString();
            }

            EnsureBakeable(fontAsset, log);
            int atlasCountBefore = fontAsset.atlasTextures.Length;

            var stillMissing = new List<char>();
            for (int i = 0; i < missing.Count; i += BakeChunkSize)
            {
                int count = Math.Min(BakeChunkSize, missing.Count - i);
                string chunk = new string(missing.ToArray(), i, count);
                if (!fontAsset.TryAddCharacters(chunk, out string notAdded))
                {
                    foreach (char c in notAdded)
                    {
                        stillMissing.Add(c);
                    }
                }
                EditorUtility.DisplayProgressBar("TMP 字体烘焙", $"{Math.Min(i + count, missing.Count)}/{missing.Count}", (float)(i + count) / missing.Count);
            }
            EditorUtility.ClearProgressBar();

            EditorUtility.SetDirty(fontAsset);
            Texture2D[] atlases = fontAsset.atlasTextures;
            for (int i = 0; i < atlases.Length; i++)
            {
                if (atlases[i] != null)
                {
                    EditorUtility.SetDirty(atlases[i]);
                }
            }
            AssetDatabase.SaveAssets();

            int baked = missing.Count - stillMissing.Count;
            log.AppendLine($"烘焙完成：成功 {baked} 个，失败 {stillMissing.Count} 个");
            if (baked > 0)
            {
                log.AppendLine($"图集张数：{atlasCountBefore} → {fontAsset.atlasTextures.Length}");
                log.AppendLine("字体资产已保存，回 Unity 重新进入界面即可看到新字。");
            }
            if (stillMissing.Count > 0)
            {
                log.AppendLine("仍未加入：" + DescribeCharacters(stillMissing));
                log.AppendLine("可能原因：字体资产为 Static 模式、源字体文件缺失，或图集无法扩容。");
            }

            return log.ToString();
        }

        [MenuItem("XFramework/Tools/烘焙 TMP 字体图集（仅报告）")]
        private static void BakeFontAtlasMenu()
        {
            Debug.Log(BakeTmpFont(string.Empty));
        }

        /// <summary>
        /// 无头模式入口（不依赖打开着的 Editor）。用法：
        ///   Unity.exe -batchmode -quit -projectPath &lt;项目路径&gt;
        ///            -executeMethod XFramework.Editor.FontBakeCommands.BakeFontAtlasInBatchMode
        /// 等价于执行一次 <c>font_bake --apply</c>。
        /// </summary>
        public static void BakeFontAtlasInBatchMode()
        {
            int exitCode = 0;
            try
            {
                AssetDatabase.Refresh();
                object result = BakeTmpFont("--apply");
                Debug.Log(result);
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[font_bake] 烘焙失败：{exception}");
                exitCode = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        #region 扫描

        private static int ScanProjectText(SortedSet<char> characters, bool includeAll)
        {
            if (!Directory.Exists("Assets"))
            {
                return 0;
            }

            int fileCount = 0;
            string[] files = Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                if (!IsScannableAsset(path, includeAll))
                {
                    continue;
                }

                string content;
                try
                {
                    content = File.ReadAllText(path, Encoding.UTF8);
                }
                catch (Exception)
                {
                    continue;
                }

                fileCount++;
                foreach (Match match in TextValueRegex.Matches(content))
                {
                    string raw = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                    AddCharacters(characters, Unescape(raw));
                }
            }

            return fileCount;
        }

        private static int ScanCodeText(SortedSet<char> characters, string root)
        {
            string scanRoot = string.IsNullOrEmpty(root) ? DefaultCodeScanRoot : root;
            if (!Directory.Exists(scanRoot))
            {
                return 0;
            }

            int added = 0;
            string[] files = Directory.GetFiles(scanRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string content;
                try
                {
                    content = File.ReadAllText(files[i], Encoding.UTF8);
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (Match match in CodeStringRegex.Matches(content))
                {
                    string literal = Unescape(match.Groups[1].Value);
                    for (int c = 0; c < literal.Length; c++)
                    {
                        if (IsWideCharacter(literal[c]) && characters.Add(literal[c]))
                        {
                            added++;
                        }
                    }
                }
            }

            return added;
        }

        private static bool TryReadCharactersFile(string path, SortedSet<char> characters, out int count, out string error)
        {
            count = 0;
            error = null;
            string resolved = ResolveProjectPath(path);
            if (!File.Exists(resolved))
            {
                error = $"文件不存在：{resolved}";
                return false;
            }

            try
            {
                count = AddCharacters(characters, File.ReadAllText(resolved, Encoding.UTF8));
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        #endregion

        #region 烘焙

        private static void EnsureBakeable(TMP_FontAsset fontAsset, StringBuilder log)
        {
            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                AtlasPopulationMode previous = fontAsset.atlasPopulationMode;
                fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                log.AppendLine($"已将 Atlas Population Mode 从 {previous} 改为 Dynamic。");
            }

            if (!fontAsset.isMultiAtlasTexturesEnabled)
            {
                fontAsset.isMultiAtlasTexturesEnabled = true;
                log.AppendLine("已开启 Multi Atlas Textures：图集写满时会自动新建图集。");
            }
        }

        /// <summary>默认烘焙 TMP Settings 里配置的默认字体，项目换字体后不需要改代码。</summary>
        private static string ResolveDefaultFontPath()
        {
            TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
            return defaultFont == null ? null : AssetDatabase.GetAssetPath(defaultFont);
        }

        private static bool HasCharacter(TMP_FontAsset fontAsset, char character)
        {
            return fontAsset.characterLookupTable != null && fontAsset.characterLookupTable.ContainsKey(character);
        }

        private static List<char> CharacterLookupKeys(TMP_FontAsset fontAsset)
        {
            var result = new List<char>();
            if (fontAsset.characterLookupTable == null)
            {
                return result;
            }

            foreach (uint code in fontAsset.characterLookupTable.Keys)
            {
                if (code <= char.MaxValue)
                {
                    result.Add((char)code);
                }
            }

            result.Sort();
            return result;
        }

        #endregion

        #region 工具

        private static int AddCharacters(SortedSet<char> characters, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int added = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (IsIgnorable(c))
                {
                    continue;
                }
                if (characters.Add(c))
                {
                    added++;
                }
            }

            return added;
        }

        private static bool IsIgnorable(char c)
        {
            return char.IsControl(c) || char.IsWhiteSpace(c);
        }

        private static bool IsWideCharacter(char c)
        {
            return (c >= 0x2E80 && c <= 0x2EFF)
                || (c >= 0x3000 && c <= 0x303F)
                || (c >= 0x4E00 && c <= 0x9FFF)
                || (c >= 0xF900 && c <= 0xFAFF)
                || (c >= 0xFF00 && c <= 0xFFEF);
        }

        private static bool IsScannableAsset(string assetPath, bool includeAll)
        {
            string extension = Path.GetExtension(assetPath).ToLowerInvariant();
            bool matched = false;
            for (int i = 0; i < ScanExtensions.Length; i++)
            {
                if (ScanExtensions[i] == extension)
                {
                    matched = true;
                    break;
                }
            }
            if (!matched)
            {
                return false;
            }

            if (includeAll)
            {
                return true;
            }

            for (int i = 0; i < ExcludedFolders.Length; i++)
            {
                if (assetPath.StartsWith(ExcludedFolders[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static string ResolveProjectPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            string normalized = path.Replace('\\', '/');
            if (Path.IsPathRooted(normalized))
            {
                return normalized;
            }

            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            return projectRoot == null ? normalized : projectRoot.FullName.Replace('\\', '/') + "/" + normalized;
        }

        /// <summary>把 YAML / C# 字符串里的转义还原成实际字符（\uXXXX、\n、\" 等）。</summary>
        private static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0)
            {
                return value;
            }

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c != '\\' || i + 1 >= value.Length)
                {
                    builder.Append(c);
                    continue;
                }

                char next = value[i + 1];
                switch (next)
                {
                    case 'u':
                        if (i + 5 < value.Length && ushort.TryParse(value.Substring(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort code))
                        {
                            builder.Append((char)code);
                            i += 5;
                        }
                        else
                        {
                            builder.Append(next);
                            i++;
                        }
                        break;
                    case 'n':
                        builder.Append('\n');
                        i++;
                        break;
                    case 'r':
                        builder.Append('\r');
                        i++;
                        break;
                    case 't':
                        builder.Append('\t');
                        i++;
                        break;
                    case '"':
                        builder.Append('"');
                        i++;
                        break;
                    case '\\':
                        builder.Append('\\');
                        i++;
                        break;
                    default:
                        builder.Append(next);
                        i++;
                        break;
                }
            }

            return builder.ToString();
        }

        private static string DescribeCharacters(IReadOnlyList<char> characters)
        {
            if (characters == null || characters.Count == 0)
            {
                return "<无>";
            }

            var builder = new StringBuilder();
            int limit = Math.Min(characters.Count, PreviewLimit);
            for (int i = 0; i < limit; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }
                builder.Append(characters[i]);
            }
            if (characters.Count > limit)
            {
                builder.Append($" …（共 {characters.Count} 个，此处只显示前 {limit} 个）");
            }

            return builder.ToString();
        }

        private static List<string> Tokenize(string line)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(line))
            {
                return tokens;
            }

            var builder = new StringBuilder();
            bool inQuote = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuote)
                {
                    if (c == '\\' && i + 1 < line.Length && (line[i + 1] == '"' || line[i + 1] == '\\'))
                    {
                        builder.Append(line[++i]);
                    }
                    else if (c == '"')
                    {
                        inQuote = false;
                    }
                    else
                    {
                        builder.Append(c);
                    }
                    continue;
                }

                if (c == '"')
                {
                    inQuote = true;
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (builder.Length > 0)
                    {
                        tokens.Add(builder.ToString());
                        builder.Length = 0;
                    }
                }
                else
                {
                    builder.Append(c);
                }
            }

            if (builder.Length > 0)
            {
                tokens.Add(builder.ToString());
            }

            return tokens;
        }

        #endregion

        private sealed class BakeOptions
        {
            public bool Apply;
            public bool NoScan;
            public bool IncludeCode;
            public bool IncludeAll;
            public bool ListOnly;
            public bool AllCommon;
            public string Chars;
            public string CharsFile;
            public string FontPath;
            public string CodeRoot;

            public static BakeOptions Parse(string argument)
            {
                var options = new BakeOptions();
                List<string> tokens = Tokenize(argument);
                for (int i = 0; i < tokens.Count; i++)
                {
                    string token = tokens[i];
                    switch (token)
                    {
                        case "--apply":
                            options.Apply = true;
                            break;
                        case "--no-scan":
                            options.NoScan = true;
                            break;
                        case "--include-code":
                            options.IncludeCode = true;
                            break;
                        case "--include-all":
                            options.IncludeAll = true;
                            break;
                        case "--list":
                            options.ListOnly = true;
                            break;
                        case "--all-common":
                            options.AllCommon = true;
                            break;
                        case "--chars":
                            options.Chars = NextValue(tokens, ref i, token);
                            break;
                        case "--chars-file":
                            options.CharsFile = NextValue(tokens, ref i, token);
                            break;
                        case "--font":
                            options.FontPath = NextValue(tokens, ref i, token);
                            break;
                        case "--code-root":
                            options.CodeRoot = NextValue(tokens, ref i, token);
                            break;
                        default:
                            throw new ArgumentException($"无法识别的参数：{token}。可用参数见 font_bake 的 usage。");
                    }
                }

                return options;
            }

            private static string NextValue(List<string> tokens, ref int index, string name)
            {
                if (index + 1 >= tokens.Count)
                {
                    throw new ArgumentException($"参数 {name} 缺少取值。");
                }

                return tokens[++index];
            }
        }
    }
}
