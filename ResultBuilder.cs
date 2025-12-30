using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.CodebaseFinder
{
    public class ResultBuilder
    {
        private readonly Settings _settings;
        private readonly PluginInitContext _context;
        private static readonly Regex LangFilterRegex = new Regex(@"\blang:(\w+)\b", RegexOptions.IgnoreCase);

        public ResultBuilder(Settings settings, PluginInitContext context)
        {
            _settings = settings;
            _context = context;
        }

        private string EditorIconPath => _settings.GetEditorIconPath();

        /// <summary>
        /// Builds Flow Launcher results from search results, filtered by query
        /// Supports lang:xyz filter (e.g., "code lang:rust myproject")
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

                var result = CreateResult(searchResult);
                if (result != null)
                {
                    // Calculate match score based on remaining query (after lang: removed)
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
                        // No text query - return all results (that passed lang filter)
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

            // No query - keep recency order (already sorted by date modified from Everything)
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
        /// Creates a result for when no codebases are found
        /// </summary>
        public Result CreateNoResultsResult(string query)
        {
            return new Result
            {
                Title = "No codebases found",
                SubTitle = string.IsNullOrWhiteSpace(query)
                    ? "No .git folders or .code-workspace files found in search paths"
                    : $"No codebases matching '{query}'",
                IcoPath = EditorIconPath
            };
        }

        private Result CreateResult(SearchResult searchResult)
        {
            string title;
            string subTitle;
            string targetPath;
            string iconPath;

            switch (searchResult.Type)
            {
                case SearchResultType.GitRepository:
                    // For git repos, title is folder name, path is to folder
                    title = Path.GetFileName(searchResult.Path) ?? searchResult.Path;
                    targetPath = searchResult.Path;
                    // Show all languages in subtitle if known
                    var langDisplay = searchResult.PrimaryLanguage != Languages.Unknown
                        ? string.Join(", ", searchResult.Languages)
                        : null;
                    subTitle = langDisplay != null
                        ? $"{searchResult.Path} • {langDisplay}"
                        : searchResult.Path;
                    // Use custom icon if available, otherwise primary language icon
                    iconPath = !string.IsNullOrEmpty(searchResult.CustomIconPath)
                        ? searchResult.CustomIconPath
                        : LanguageDetector.GetIconPath(searchResult.PrimaryLanguage);
                    break;

                case SearchResultType.CodeWorkspace:
                    // For workspaces, title is filename, path is to file
                    title = Path.GetFileName(searchResult.Path) ?? searchResult.Path;
                    subTitle = searchResult.Path;
                    targetPath = searchResult.Path;
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
                Action = _ => OpenInEditor(targetPath),
                ContextData = searchResult
            };
        }

        private bool OpenInEditor(string path)
        {
            try
            {
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
