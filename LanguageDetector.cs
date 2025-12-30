using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flow.Launcher.Plugin.CodebaseFinder
{
    public static class Languages
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
            (new[] { "tsconfig.json" }, Array.Empty<string>(), Languages.TypeScript),

            // Rust
            (new[] { "Cargo.toml" }, Array.Empty<string>(), Languages.Rust),

            // Go
            (new[] { "go.mod" }, Array.Empty<string>(), Languages.Go),

            // C# (check for .csproj or .sln files)
            (Array.Empty<string>(), new[] { "*.csproj", "*.sln" }, Languages.CSharp),

            // Java/Kotlin (Kotlin uses Gradle too, check for .kt files)
            (new[] { "build.gradle.kts" }, Array.Empty<string>(), Languages.Kotlin),
            (new[] { "pom.xml", "build.gradle" }, Array.Empty<string>(), Languages.Java),

            // Python
            (new[] { "pyproject.toml", "setup.py", "requirements.txt", "Pipfile" }, Array.Empty<string>(), Languages.Python),

            // Ruby
            (new[] { "Gemfile" }, Array.Empty<string>(), Languages.Ruby),

            // PHP
            (new[] { "composer.json" }, Array.Empty<string>(), Languages.PHP),

            // Swift
            (new[] { "Package.swift" }, Array.Empty<string>(), Languages.Swift),
            (Array.Empty<string>(), new[] { "*.xcodeproj", "*.xcworkspace" }, Languages.Swift),

            // Dart/Flutter
            (new[] { "pubspec.yaml" }, Array.Empty<string>(), Languages.Dart),

            // Elixir
            (new[] { "mix.exs" }, Array.Empty<string>(), Languages.Elixir),

            // JavaScript (after TypeScript check)
            (new[] { "package.json" }, Array.Empty<string>(), Languages.JavaScript),

            // C/C++ (check for common build files)
            (new[] { "CMakeLists.txt" }, Array.Empty<string>(), Languages.Cpp),
            (Array.Empty<string>(), new[] { "*.vcxproj" }, Languages.Cpp),
            (new[] { "Makefile" }, new[] { "*.cpp", "*.cc", "*.cxx" }, Languages.Cpp),
            (new[] { "Makefile" }, new[] { "*.c" }, Languages.C),

            // Shell
            (Array.Empty<string>(), new[] { "*.sh" }, Languages.Shell),

            // Lua
            (Array.Empty<string>(), new[] { "*.lua" }, Languages.Lua),
        };

        // Fallback: check for source file extensions
        private static readonly Dictionary<string, string> ExtensionFallbacks = new(StringComparer.OrdinalIgnoreCase)
        {
            { ".ts", Languages.TypeScript },
            { ".tsx", Languages.TypeScript },
            { ".js", Languages.JavaScript },
            { ".jsx", Languages.JavaScript },
            { ".py", Languages.Python },
            { ".rs", Languages.Rust },
            { ".go", Languages.Go },
            { ".cs", Languages.CSharp },
            { ".java", Languages.Java },
            { ".kt", Languages.Kotlin },
            { ".rb", Languages.Ruby },
            { ".php", Languages.PHP },
            { ".swift", Languages.Swift },
            { ".dart", Languages.Dart },
            { ".cpp", Languages.Cpp },
            { ".cc", Languages.Cpp },
            { ".cxx", Languages.Cpp },
            { ".c", Languages.C },
            { ".h", Languages.C },
            { ".ex", Languages.Elixir },
            { ".exs", Languages.Elixir },
            { ".sh", Languages.Shell },
            { ".lua", Languages.Lua },
        };

        /// <summary>
        /// Detects the primary language of a repository
        /// </summary>
        public string Detect(string repoPath)
        {
            if (!Directory.Exists(repoPath))
                return Languages.Unknown;

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
                return Languages.Unknown;
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

            return Languages.Unknown;
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
