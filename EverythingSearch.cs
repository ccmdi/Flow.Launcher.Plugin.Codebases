using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Flow.Launcher.Plugin.CodebaseFinder
{
    public class SearchResult
    {
        public string Path { get; set; }
        public SearchResultType Type { get; set; }
    }

    public enum SearchResultType
    {
        GitRepository,
        CodeWorkspace
    }

    public class EverythingSearch
    {
        private readonly Settings _settings;

        public EverythingSearch(Settings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Checks if es.exe is available
        /// </summary>
        public bool IsAvailable()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _settings.EsExePath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit(5000);
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Searches for .git folders and .code-workspace files under configured paths
        /// </summary>
        public List<SearchResult> Search()
        {
            var results = new List<SearchResult>();

            foreach (var searchPath in _settings.SearchPaths)
            {
                if (!Directory.Exists(searchPath))
                    continue;

                // Search for .git folders
                var gitResults = ExecuteSearch(searchPath, "folder:.git");
                foreach (var gitPath in gitResults)
                {
                    // Skip paths containing ignored directories
                    if (IsInIgnoredDirectory(gitPath))
                        continue;

                    // Get parent directory (repo root)
                    var parentDir = Path.GetDirectoryName(gitPath);
                    if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                    {
                        results.Add(new SearchResult
                        {
                            Path = parentDir,
                            Type = SearchResultType.GitRepository
                        });
                    }
                }

                // Search for .code-workspace files
                var workspaceResults = ExecuteSearch(searchPath, "ext:code-workspace");
                foreach (var workspacePath in workspaceResults)
                {
                    // Skip paths containing ignored directories
                    if (IsInIgnoredDirectory(workspacePath))
                        continue;

                    if (File.Exists(workspacePath))
                    {
                        results.Add(new SearchResult
                        {
                            Path = workspacePath,
                            Type = SearchResultType.CodeWorkspace
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Checks if a path contains any of the ignored directory names
        /// </summary>
        private bool IsInIgnoredDirectory(string path)
        {
            if (_settings.IgnoredDirectories == null || _settings.IgnoredDirectories.Count == 0)
                return false;

            var pathParts = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in pathParts)
            {
                if (_settings.IgnoredDirectories.Contains(part, StringComparer.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private List<string> ExecuteSearch(string searchPath, string query)
        {
            var results = new List<string>();

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _settings.EsExePath,
                    Arguments = $"-path \"{searchPath}\" {query}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    return results;

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(10000);

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    results = output
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Trim())
                        .Where(line => !string.IsNullOrEmpty(line))
                        .ToList();
                }
            }
            catch
            {
                // Silently fail - results will be empty
            }

            return results;
        }
    }
}
