using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flow.Launcher.Plugin.CodebaseFinder
{
    public static class Language
    {
        public const string TypeScript = "TypeScript";
        public const string JavaScript = "JavaScript";
        public const string Python = "Python";
        public const string Rust = "Rust";
        public const string Go = "Go";
        public const string CSharp = "C#";
        public const string Java = "Java";
        public const string Kotlin = "Kotlin";
        public const string Ruby = "Ruby";
        public const string PHP = "PHP";
        public const string Swift = "Swift";
        public const string Dart = "Dart";
        public const string Cpp = "C++";
        public const string C = "C";
        public const string Elixir = "Elixir";
        public const string Shell = "Shell";
        public const string Lua = "Lua";
        public const string Unknown = "Unknown";
    }

    public class LanguageDetector
    {
        // Signature files mapped to languages (checked in order - first match wins)
        private static readonly List<(string[] Files, string[] Patterns, string Language)> SignatureRules = new()
        {
            // TypeScript (check before JavaScript since TS projects also have package.json)
            (new[] { "tsconfig.json" }, Array.Empty<string>(), Language.TypeScript),

            // Rust
            (new[] { "Cargo.toml" }, Array.Empty<string>(), Language.Rust),

            // Go
            (new[] { "go.mod" }, Array.Empty<string>(), Language.Go),

            // C# (check for .csproj or .sln files)
            (Array.Empty<string>(), new[] { "*.csproj", "*.sln" }, Language.CSharp),

            // Java/Kotlin (Kotlin uses Gradle too, check for .kt files)
            (new[] { "build.gradle.kts" }, Array.Empty<string>(), Language.Kotlin),
            (new[] { "pom.xml", "build.gradle" }, Array.Empty<string>(), Language.Java),

            // Python
            (new[] { "pyproject.toml", "setup.py", "requirements.txt", "Pipfile" }, Array.Empty<string>(), Language.Python),

            // Ruby
            (new[] { "Gemfile" }, Array.Empty<string>(), Language.Ruby),

            // PHP
            (new[] { "composer.json" }, Array.Empty<string>(), Language.PHP),

            // Swift
            (new[] { "Package.swift" }, Array.Empty<string>(), Language.Swift),
            (Array.Empty<string>(), new[] { "*.xcodeproj", "*.xcworkspace" }, Language.Swift),

            // Dart/Flutter
            (new[] { "pubspec.yaml" }, Array.Empty<string>(), Language.Dart),

            // Elixir
            (new[] { "mix.exs" }, Array.Empty<string>(), Language.Elixir),

            // JavaScript (after TypeScript check)
            (new[] { "package.json" }, Array.Empty<string>(), Language.JavaScript),

            // C/C++ (check for common build files)
            (new[] { "CMakeLists.txt" }, Array.Empty<string>(), Language.Cpp),
            (Array.Empty<string>(), new[] { "*.vcxproj" }, Language.Cpp),
            (new[] { "Makefile" }, new[] { "*.cpp", "*.cc", "*.cxx" }, Language.Cpp),
            (new[] { "Makefile" }, new[] { "*.c" }, Language.C),

            // Shell
            (Array.Empty<string>(), new[] { "*.sh" }, Language.Shell),

            // Lua
            (Array.Empty<string>(), new[] { "*.lua" }, Language.Lua),
        };

        // Fallback: check for source file extensions
        private static readonly Dictionary<string, string> ExtensionFallbacks = new(StringComparer.OrdinalIgnoreCase)
        {
            { ".ts", Language.TypeScript },
            { ".tsx", Language.TypeScript },
            { ".js", Language.JavaScript },
            { ".jsx", Language.JavaScript },
            { ".py", Language.Python },
            { ".rs", Language.Rust },
            { ".go", Language.Go },
            { ".cs", Language.CSharp },
            { ".java", Language.Java },
            { ".kt", Language.Kotlin },
            { ".rb", Language.Ruby },
            { ".php", Language.PHP },
            { ".swift", Language.Swift },
            { ".dart", Language.Dart },
            { ".cpp", Language.Cpp },
            { ".cc", Language.Cpp },
            { ".cxx", Language.Cpp },
            { ".c", Language.C },
            { ".h", Language.C },
            { ".ex", Language.Elixir },
            { ".exs", Language.Elixir },
            { ".sh", Language.Shell },
            { ".lua", Language.Lua },
        };

        /// <summary>
        /// Detects the primary language of a repository
        /// </summary>
        public string Detect(string repoPath)
        {
            if (!Directory.Exists(repoPath))
                return Language.Unknown;

            try
            {
                // First, try signature file detection
                foreach (var (files, patterns, language) in SignatureRules)
                {
                    // Check for exact file matches
                    foreach (var file in files)
                    {
                        if (File.Exists(Path.Combine(repoPath, file)))
                            return language;
                    }

                    // Check for pattern matches (e.g., *.csproj)
                    foreach (var pattern in patterns)
                    {
                        try
                        {
                            if (Directory.EnumerateFiles(repoPath, pattern, SearchOption.TopDirectoryOnly).Any())
                                return language;
                        }
                        catch
                        {
                            // Ignore access errors
                        }
                    }
                }

                // Fallback: scan top-level files for known extensions
                return DetectByExtension(repoPath);
            }
            catch
            {
                return Language.Unknown;
            }
        }

        private string DetectByExtension(string repoPath)
        {
            try
            {
                var files = Directory.EnumerateFiles(repoPath, "*", SearchOption.TopDirectoryOnly)
                    .Take(100); // Limit scan

                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file);
                    if (ExtensionFallbacks.TryGetValue(ext, out var language))
                        return language;
                }

                // Check src folder if exists
                var srcPath = Path.Combine(repoPath, "src");
                if (Directory.Exists(srcPath))
                {
                    var srcFiles = Directory.EnumerateFiles(srcPath, "*", SearchOption.TopDirectoryOnly)
                        .Take(50);

                    foreach (var file in srcFiles)
                    {
                        var ext = Path.GetExtension(file);
                        if (ExtensionFallbacks.TryGetValue(ext, out var language))
                            return language;
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return Language.Unknown;
        }

        /// <summary>
        /// Gets the icon filename for a language
        /// </summary>
        public static string GetIconPath(string language)
        {
            return language switch
            {
                Language.TypeScript => "Images\\lang_typescript.png",
                Language.JavaScript => "Images\\lang_javascript.png",
                Language.Python => "Images\\lang_python.png",
                Language.Rust => "Images\\lang_rust.png",
                Language.Go => "Images\\lang_go.png",
                Language.CSharp => "Images\\lang_csharp.png",
                Language.Java => "Images\\lang_java.png",
                Language.Kotlin => "Images\\lang_kotlin.png",
                Language.Ruby => "Images\\lang_ruby.png",
                Language.PHP => "Images\\lang_php.png",
                Language.Swift => "Images\\lang_swift.png",
                Language.Dart => "Images\\lang_dart.png",
                Language.Cpp => "Images\\lang_cpp.png",
                Language.C => "Images\\lang_c.png",
                Language.Elixir => "Images\\lang_elixir.png",
                Language.Shell => "Images\\lang_shell.png",
                Language.Lua => "Images\\lang_lua.png",
                _ => "Images\\lang_unknown.png"
            };
        }
    }
}
