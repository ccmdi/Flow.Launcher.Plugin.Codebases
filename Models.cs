using System;

namespace Flow.Launcher.Plugin.Codebases
{
    // ============================================================
    // Enums
    // ============================================================

    public enum Editor
    {
        Cursor,
        VSCode
    }

    public enum SortMode
    {
        GitModified,
        LastOpened
    }

    public enum SearchResultType
    {
        GitRepository,
        CodeWorkspace
    }

    // ============================================================
    // Language Constants
    // ============================================================

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

    // ============================================================
    // Search Models
    // ============================================================

    public class SearchResult
    {
        public string Path { get; set; }
        public SearchResultType Type { get; set; }
        public string[] Languages { get; set; } = new[] { Codebases.Languages.Unknown };
        public string CustomIconPath { get; set; }
        public string RemoteUrl { get; set; }

        public string PrimaryLanguage => Languages?.Length > 0 ? Languages[0] : Codebases.Languages.Unknown;
    }

    public class CachedSearchResult
    {
        public string Path { get; set; }
        public SearchResultType Type { get; set; }
        public string CustomIconPath { get; set; }
    }

    // ============================================================
    // Cache Entry Models
    // ============================================================

    public class LanguageCacheEntry
    {
        public string[] Languages { get; set; } = new[] { Codebases.Languages.Unknown };
        public DateTime DetectedAt { get; set; } = DateTime.MinValue;
        public string RemoteUrl { get; set; } = "";
    }

    public class UsageCacheEntry
    {
        public DateTime LastOpenedAt { get; set; }
    }
}
