using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flow.Launcher.Plugin.Codebases
{
    public class LanguageDetector
    {
        private readonly HashSet<string> _ignoredDirectories;
        private const int MaxFilesToScan = 500;

        // Extensions to skip (binary, media, etc.)
        private static readonly HashSet<string> SkipExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".ico", ".svg", ".webp", ".bmp",
            ".mp3", ".mp4", ".wav", ".avi", ".mov", ".mkv", ".webm",
            ".exe", ".dll", ".so", ".dylib", ".bin", ".obj", ".o",
            ".zip", ".tar", ".gz", ".rar", ".7z",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx",
            ".lock", ".sum", ".mod"
        };

        // Map extensions to languages
        private static readonly Dictionary<string, string> ExtensionToLanguage = new(StringComparer.OrdinalIgnoreCase)
        {
            // TypeScript
            { ".ts", Languages.TypeScript },
            { ".tsx", Languages.TypeScript },
            { ".mts", Languages.TypeScript },
            { ".cts", Languages.TypeScript },

            // JavaScript
            { ".js", Languages.JavaScript },
            { ".jsx", Languages.JavaScript },
            { ".mjs", Languages.JavaScript },
            { ".cjs", Languages.JavaScript },

            // Python
            { ".py", Languages.Python },
            { ".pyw", Languages.Python },
            { ".pyx", Languages.Python },

            // Rust
            { ".rs", Languages.Rust },

            // Go
            { ".go", Languages.Go },

            // C#
            { ".cs", Languages.CSharp },

            // Java
            { ".java", Languages.Java },

            // Kotlin
            { ".kt", Languages.Kotlin },
            { ".kts", Languages.Kotlin },

            // Ruby
            { ".rb", Languages.Ruby },
            { ".rake", Languages.Ruby },

            // PHP
            { ".php", Languages.PHP },

            // Swift
            { ".swift", Languages.Swift },

            // Dart
            { ".dart", Languages.Dart },

            // C++
            { ".cpp", Languages.Cpp },
            { ".cc", Languages.Cpp },
            { ".cxx", Languages.Cpp },
            { ".hpp", Languages.Cpp },
            { ".hxx", Languages.Cpp },

            // C
            { ".c", Languages.C },
            { ".h", Languages.C },

            // Elixir
            { ".ex", Languages.Elixir },
            { ".exs", Languages.Elixir },

            // Shell
            { ".sh", Languages.Shell },
            { ".bash", Languages.Shell },
            { ".zsh", Languages.Shell },

            // Lua
            { ".lua", Languages.Lua },
        };

        public LanguageDetector(IEnumerable<string> ignoredDirectories = null)
        {
            _ignoredDirectories = new HashSet<string>(
                ignoredDirectories ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
        }

        private const double MinLanguageThreshold = 0.20; // 20% threshold

        /// <summary>
        /// Detects languages in a repository that make up at least 20% of the codebase
        /// Returns array sorted by prevalence (most common first)
        /// </summary>
        public string[] Detect(string repoPath)
        {
            if (!Directory.Exists(repoPath))
                return new[] { Languages.Unknown };

            try
            {
                var languageCounts = new Dictionary<string, int>();
                var filesScanned = 0;

                ScanDirectory(repoPath, languageCounts, ref filesScanned);

                if (languageCounts.Count == 0)
                    return new[] { Languages.Unknown };

                var totalFiles = languageCounts.Values.Sum();

                // Return all languages that make up at least 20% of the codebase
                var significantLanguages = languageCounts
                    .Where(kvp => (double)kvp.Value / totalFiles >= MinLanguageThreshold)
                    .OrderByDescending(kvp => kvp.Value)
                    .Select(kvp => kvp.Key)
                    .ToArray();

                return significantLanguages.Length > 0
                    ? significantLanguages
                    : new[] { languageCounts.OrderByDescending(kvp => kvp.Value).First().Key };
            }
            catch
            {
                return new[] { Languages.Unknown };
            }
        }

        private void ScanDirectory(string path, Dictionary<string, int> languageCounts, ref int filesScanned)
        {
            if (filesScanned >= MaxFilesToScan)
                return;

            try
            {
                // Process files in current directory
                foreach (var file in Directory.EnumerateFiles(path))
                {
                    if (filesScanned >= MaxFilesToScan)
                        return;

                    var ext = Path.GetExtension(file);

                    // Skip binary/media files
                    if (SkipExtensions.Contains(ext))
                        continue;

                    if (ExtensionToLanguage.TryGetValue(ext, out var language))
                    {
                        if (!languageCounts.ContainsKey(language))
                            languageCounts[language] = 0;
                        languageCounts[language]++;
                        filesScanned++;
                    }
                }

                // Recursively scan subdirectories
                foreach (var dir in Directory.EnumerateDirectories(path))
                {
                    if (filesScanned >= MaxFilesToScan)
                        return;

                    var dirName = Path.GetFileName(dir);

                    // Skip ignored directories
                    if (_ignoredDirectories.Contains(dirName))
                        continue;

                    // Skip hidden directories (starting with .)
                    if (dirName.StartsWith("."))
                        continue;

                    ScanDirectory(dir, languageCounts, ref filesScanned);
                }
            }
            catch
            {
                // Ignore access errors and continue
            }
        }

        /// <summary>
        /// Gets the icon filename for a language
        /// </summary>
        public static string GetIconPath(string language)
        {
            return language switch
            {
                Languages.TypeScript => "Images\\lang_typescript.png",
                Languages.JavaScript => "Images\\lang_javascript.png",
                Languages.Python => "Images\\lang_python.png",
                Languages.Rust => "Images\\lang_rust.png",
                Languages.Go => "Images\\lang_go.png",
                Languages.CSharp => "Images\\lang_csharp.png",
                Languages.Java => "Images\\lang_java.png",
                Languages.Kotlin => "Images\\lang_kotlin.png",
                Languages.Ruby => "Images\\lang_ruby.png",
                Languages.PHP => "Images\\lang_php.png",
                Languages.Swift => "Images\\lang_swift.png",
                Languages.Dart => "Images\\lang_dart.png",
                Languages.Cpp => "Images\\lang_cpp.png",
                Languages.C => "Images\\lang_c.png",
                Languages.Elixir => "Images\\lang_elixir.png",
                Languages.Shell => "Images\\lang_shell.png",
                Languages.Lua => "Images\\lang_lua.png",
                _ => "Images\\lang_unknown.png"
            };
        }
    }
}
