using System;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.CodebaseFinder
{
    public enum Editor
    {
        Cursor,
        VSCode
    }

    public class Settings
    {
        public Editor Editor { get; set; } = Editor.Cursor;

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
    }
}
