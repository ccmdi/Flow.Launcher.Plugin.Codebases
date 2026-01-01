using System;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.Codebases
{
    public class Settings
    {
        public Editor Editor { get; set; } = Editor.Cursor;

        public SortMode SortMode { get; set; } = SortMode.GitModified;

        public List<string> SearchPaths { get; set; } = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        public string EsExePath { get; set; } = "es.exe";

        /// <summary>
        /// Maximum number of results to return when query is empty
        /// </summary>
        public int MaxResults { get; set; } = 50;

        /// <summary>
        /// Default location for creating new codebases. If empty, the create option is disabled.
        /// </summary>
        public string DefaultNewCodebaseLocation { get; set; } = "";

        /// <summary>
        /// Directory names to ignore when searching (e.g., node_modules, vendor)
        /// </summary>
        public List<string> IgnoredDirectories { get; set; } = new List<string>
        {
            "node_modules",
            "vendor",
            "__pycache__",
            ".venv",
            "venv",
            "env",
            ".env",
            "dist",
            "build",
            "bin",
            "obj",
            ".next",
            ".nuxt",
            "target",
            "out",
            "coverage",
            ".cache",
            "packages",
            ".gradle",
            "bower_components"
        };

        /// <summary>
        /// Gets the CLI command for the configured editor
        /// </summary>
        public string GetEditorCommand()
        {
            return Editor switch
            {
                Editor.Cursor => "cursor",
                Editor.VSCode => "code",
                _ => "code"
            };
        }

        /// <summary>
        /// Gets the icon path for the configured editor
        /// </summary>
        public string GetEditorIconPath()
        {
            return Editor switch
            {
                Editor.Cursor => "Images\\cursor.png",
                Editor.VSCode => "Images\\vscode.png",
                _ => "Images\\vscode.png"
            };
        }
    }
}
