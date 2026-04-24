using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Flow.Launcher.Plugin.Codebases
{
    public class EverythingSearch
    {
        private readonly Settings _settings;
        private static readonly object _queryLock = new();
        private bool _dllAvailable = true;

        public EverythingSearch(Settings settings)
        {
            _settings = settings;
        }

        public bool IsAvailable()
        {
            if (!_dllAvailable)
                return false;

            try
            {
                return EverythingSDK.Everything_GetMajorVersion() > 0;
            }
            catch (DllNotFoundException)
            {
                _dllAvailable = false;
                return false;
            }
        }

        public List<SearchResult> Search()
        {
            var results = new List<SearchResult>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var searchPath in _settings.SearchPaths)
            {
                if (!Directory.Exists(searchPath))
                    continue;

                var normalizedPath = searchPath.TrimEnd('\\', '/');

                var gitResults = ExecuteSearch($"\"{normalizedPath}\\\" folder:.git");
                foreach (var gitPath in gitResults)
                {
                    if (IsInIgnoredDirectory(gitPath))
                        continue;

                    var parentDir = Path.GetDirectoryName(gitPath);
                    if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                    {
                        var key = $"git:{parentDir}";
                        if (seenPaths.Contains(key))
                            continue;
                        seenPaths.Add(key);

                        results.Add(new SearchResult
                        {
                            Path = parentDir,
                            Type = SearchResultType.GitRepository,
                            CustomIconPath = FindCustomIcon(parentDir)
                        });
                    }
                }

                var workspaceResults = ExecuteSearch($"\"{normalizedPath}\\\" ext:code-workspace");
                foreach (var workspacePath in workspaceResults)
                {
                    if (IsInIgnoredDirectory(workspacePath))
                        continue;

                    if (File.Exists(workspacePath))
                    {
                        var key = $"ws:{workspacePath}";
                        if (seenPaths.Contains(key))
                            continue;
                        seenPaths.Add(key);

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

        private string FindCustomIcon(string repoPath)
        {
            try
            {
                var preferredNames = new[] { "app.ico", "icon.ico", "favicon.ico", "logo.ico" };

                foreach (var name in preferredNames)
                {
                    var iconPath = Path.Combine(repoPath, name);
                    if (File.Exists(iconPath))
                        return iconPath;
                }

                var icoFiles = Directory.GetFiles(repoPath, "*.ico", SearchOption.TopDirectoryOnly);
                if (icoFiles.Length > 0)
                    return icoFiles[0];
            }
            catch
            {
            }

            return null;
        }

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

        private List<string> ExecuteSearch(string query)
        {
            var results = new List<string>();

            try
            {
                lock (_queryLock)
                {
                    EverythingSDK.Everything_SetSearchW(query);
                    EverythingSDK.Everything_SetSort(EverythingSDK.EVERYTHING_SORT_DATE_MODIFIED_DESCENDING);
                    EverythingSDK.Everything_SetRequestFlags(EverythingSDK.EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME);
                    EverythingSDK.Everything_QueryW(true);

                    var numResults = EverythingSDK.Everything_GetNumResults();
                    var sb = new StringBuilder(1024);

                    for (uint i = 0; i < numResults; i++)
                    {
                        sb.Clear();
                        EverythingSDK.Everything_GetResultFullPathName(i, sb, 1024);
                        var path = sb.ToString();
                        if (!string.IsNullOrEmpty(path))
                            results.Add(path);
                    }
                }
            }
            catch (DllNotFoundException)
            {
                _dllAvailable = false;
            }
            catch
            {
            }

            return results;
        }
    }
}
