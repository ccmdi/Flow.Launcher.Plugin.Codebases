using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.Codebases
{
    public class ResultBuilder
    {
        private readonly Settings _settings;
        private readonly PluginInitContext _context;
        private readonly UsageTracker _usageTracker;
        private static readonly Regex LangFilterRegex = new Regex(@"\blang:(\w+)\b", RegexOptions.IgnoreCase);
        private static readonly Regex RemoteRegex = new Regex(@"(?:^|\s)--remote(?:\s|$)", RegexOptions.IgnoreCase);

        public ResultBuilder(Settings settings, PluginInitContext context, UsageTracker usageTracker)
        {
            _settings = settings;
            _context = context;
            _usageTracker = usageTracker;
        }

        private string EditorIconPath => _settings.GetEditorIconPath();

        /// <summary>
        /// Builds Flow Launcher results from search results, filtered by query
        /// Supports lang:xyz filter (e.g., "code lang:rust myproject")
        /// Supports --remote flag (e.g., "code myproject --remote")
        /// </summary>
        public List<Result> Build(List<SearchResult> searchResults, string query)
        {
            var results = new List<Result>();

            // Parse language filter from query
            string languageFilter = null;
            var searchQuery = query ?? "";

            var langMatch = LangFilterRegex.Match(searchQuery);
            if (langMatch.Success)
            {
                languageFilter = langMatch.Groups[1].Value;
                // Remove the lang: filter from the search query
                searchQuery = LangFilterRegex.Replace(searchQuery, "").Trim();
            }

            // Parse --remote flag from query
            var isRemoteMode = RemoteRegex.IsMatch(searchQuery);
            if (isRemoteMode)
            {
                searchQuery = RemoteRegex.Replace(searchQuery, "").Trim();
            }

            foreach (var searchResult in searchResults)
            {
                // Apply language filter if specified - check all languages in the array
                if (!string.IsNullOrEmpty(languageFilter))
                {
                    var matchesFilter = searchResult.Languages.Any(lang =>
                        lang.Contains(languageFilter, StringComparison.OrdinalIgnoreCase));
                    if (!matchesFilter)
                        continue;
                }

                // Skip results without remote URL when in remote mode
                if (isRemoteMode && string.IsNullOrEmpty(searchResult.RemoteUrl))
                    continue;

                var result = CreateResult(searchResult, isRemoteMode);
                if (result != null)
                {
                    // Calculate match score based on remaining query (after lang:/--remote removed)
                    if (!string.IsNullOrWhiteSpace(searchQuery))
                    {
                        var matchResult = _context.API.FuzzySearch(searchQuery, result.Title);
                        if (matchResult.IsSearchPrecisionScoreMet())
                        {
                            result.Score = matchResult.Score;
                            result.TitleHighlightData = matchResult.MatchData;
                            results.Add(result);
                        }
                    }
                    else
                    {
                        // No text query - return all results (that passed filters)
                        results.Add(result);
                    }
                }
            }

            // When querying, sort by score; otherwise preserve recency order from Everything
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                return results
                    .OrderByDescending(r => r.Score)
                    .ThenBy(r => r.Title)
                    .Take(_settings.MaxResults)
                    .ToList();
            }

            // No query - sort based on configured SortMode
            if (_settings.SortMode == SortMode.LastOpened)
            {
                // Partition into opened and never-opened
                var opened = new List<Result>();
                var neverOpened = new List<Result>();

                foreach (var result in results)
                {
                    if (result.ContextData is SearchResult sr && _usageTracker.GetLastOpened(sr.Path) != null)
                    {
                        opened.Add(result);
                    }
                    else
                    {
                        neverOpened.Add(result);
                    }
                }

                // Sort opened by last-opened time descending
                opened = opened
                    .OrderByDescending(r =>
                    {
                        var sr = r.ContextData as SearchResult;
                        return sr != null ? _usageTracker.GetLastOpened(sr.Path) : DateTime.MinValue;
                    })
                    .ToList();

                // Combine: opened first, then never-opened (keep git-modified order)
                return opened
                    .Concat(neverOpened)
                    .Take(_settings.MaxResults)
                    .ToList();
            }

            // GitModified mode - keep recency order (already sorted by date modified from Everything)
            return results
                .Take(_settings.MaxResults)
                .ToList();
        }

        /// <summary>
        /// Creates an error result for when es.exe is not found
        /// </summary>
        public Result CreateEsNotFoundResult()
        {
            return new Result
            {
                Title = "Everything CLI not found",
                SubTitle = "Install Everything from voidtools.com and ensure es.exe is in PATH or configured",
                IcoPath = EditorIconPath,
                Action = _ =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://www.voidtools.com/",
                        UseShellExecute = true
                    });
                    return true;
                }
            };
        }

        /// <summary>
        /// Creates a result for when no codebases are found.
        /// If a query is provided and DefaultNewCodebaseLocation is configured,
        /// offers to create a new codebase instead.
        /// </summary>
        public Result CreateNoResultsResult(string query)
        {
            // Strip filters from query before using as codebase name
            var cleanQuery = query ?? "";
            cleanQuery = LangFilterRegex.Replace(cleanQuery, "").Trim();
            cleanQuery = RemoteRegex.Replace(cleanQuery, "").Trim();

            // If there's a query and a default location is set, offer to create
            if (!string.IsNullOrWhiteSpace(cleanQuery) &&
                !string.IsNullOrWhiteSpace(_settings.DefaultNewCodebaseLocation) &&
                Directory.Exists(_settings.DefaultNewCodebaseLocation))
            {
                var newPath = Path.Combine(_settings.DefaultNewCodebaseLocation, cleanQuery);
                return new Result
                {
                    Title = $"Create '{cleanQuery}'",
                    SubTitle = newPath,
                    IcoPath = EditorIconPath,
                    Action = _ => CreateAndOpenCodebase(newPath)
                };
            }

            return new Result
            {
                Title = "No codebases found",
                SubTitle = string.IsNullOrWhiteSpace(query)
                    ? "No .git folders or .code-workspace files found in search paths"
                    : $"No codebases matching '{query}'",
                IcoPath = EditorIconPath
            };
        }

        private bool CreateAndOpenCodebase(string path)
        {
            try
            {
                // Create the directory
                Directory.CreateDirectory(path);

                // Record usage for sorting
                _usageTracker.RecordOpen(path);

                // Open in editor
                var editorCommand = _settings.GetEditorCommand();
                var startInfo = new ProcessStartInfo
                {
                    FileName = editorCommand,
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                _context.API.ShowMsg("Error", $"Failed to create codebase: {ex.Message}");
                return false;
            }
        }

        private Result CreateResult(SearchResult searchResult, bool isRemote = false)
        {
            string title;
            string subTitle;
            string iconPath;

            switch (searchResult.Type)
            {
                case SearchResultType.GitRepository:
                    // For git repos, title is folder name, path is to folder
                    title = Path.GetFileName(searchResult.Path) ?? searchResult.Path;

                    if (isRemote && !string.IsNullOrEmpty(searchResult.RemoteUrl))
                    {
                        // Remote mode: show remote URL in subtitle
                        subTitle = searchResult.RemoteUrl;
                    }
                    else
                    {
                        // Normal mode: show path and languages
                        var langDisplay = searchResult.PrimaryLanguage != Languages.Unknown
                            ? string.Join(", ", searchResult.Languages)
                            : null;
                        subTitle = langDisplay != null
                            ? $"{searchResult.Path} • {langDisplay}"
                            : searchResult.Path;
                    }

                    // Use custom icon if available, otherwise primary language icon
                    iconPath = !string.IsNullOrEmpty(searchResult.CustomIconPath)
                        ? searchResult.CustomIconPath
                        : LanguageDetector.GetIconPath(searchResult.PrimaryLanguage);
                    break;

                case SearchResultType.CodeWorkspace:
                    // For workspaces, title is filename, path is to file
                    title = Path.GetFileName(searchResult.Path) ?? searchResult.Path;
                    subTitle = searchResult.Path;
                    // Use editor icon for workspaces
                    iconPath = EditorIconPath;
                    break;

                default:
                    return null;
            }

            return new Result
            {
                Title = title,
                SubTitle = subTitle,
                IcoPath = iconPath,
                Action = HandleAction(searchResult, isRemote),
                ContextData = searchResult
            };
        }

        private Func<ActionContext, bool> HandleAction(SearchResult searchResult, bool isRemote = false)
        {
            if(isRemote)
            {
                return _ => OpenInBrowser(searchResult.RemoteUrl);
            }
            else
            {
                return _ => OpenInEditor(searchResult.Path);
            }
        }

        private bool OpenInBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                _context.API.ShowMsg("Error", $"Failed to open browser: {ex.Message}");
                return false;
            }
        }

        private bool OpenInEditor(string path)
        {
            try
            {
                // Record usage for sorting
                _usageTracker.RecordOpen(path);

                var editorCommand = _settings.GetEditorCommand();

                var startInfo = new ProcessStartInfo
                {
                    FileName = editorCommand,
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                _context.API.ShowMsg("Error", $"Failed to open editor: {ex.Message}");
                return false;
            }
        }
    }
}
