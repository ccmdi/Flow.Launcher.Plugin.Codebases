using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Flow.Launcher.Plugin.CodebaseFinder
{
    public class ResultBuilder
    {
        private readonly Settings _settings;
        private readonly PluginInitContext _context;
        private readonly string _iconPath;

        public ResultBuilder(Settings settings, PluginInitContext context, string iconPath)
        {
            _settings = settings;
            _context = context;
            _iconPath = iconPath;
        }

        /// <summary>
        /// Builds Flow Launcher results from search results, filtered by query
        /// </summary>
        public List<Result> Build(List<SearchResult> searchResults, string query)
        {
            var results = new List<Result>();

            foreach (var searchResult in searchResults)
            {
                var result = CreateResult(searchResult);
                if (result != null)
                {
                    // Calculate match score based on query
                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        var matchResult = _context.API.FuzzySearch(query, result.Title);
                        if (matchResult.IsSearchPrecisionScoreMet())
                        {
                            result.Score = matchResult.Score;
                            result.TitleHighlightData = matchResult.MatchData;
                            results.Add(result);
                        }
                    }
                    else
                    {
                        // No query - return all results
                        results.Add(result);
                    }
                }
            }

            // Sort by score descending, then by title
            return results
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Title)
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
                IcoPath = _iconPath,
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
                IcoPath = _iconPath
            };
        }

        private Result CreateResult(SearchResult searchResult)
        {
            string title;
            string subTitle;
            string targetPath;

            switch (searchResult.Type)
            {
                case SearchResultType.GitRepository:
                    // For git repos, title is folder name, path is to folder
                    title = Path.GetFileName(searchResult.Path) ?? searchResult.Path;
                    subTitle = searchResult.Path;
                    targetPath = searchResult.Path;
                    break;

                case SearchResultType.CodeWorkspace:
                    // For workspaces, title is filename, path is to file
                    title = Path.GetFileName(searchResult.Path) ?? searchResult.Path;
                    subTitle = searchResult.Path;
                    targetPath = searchResult.Path;
                    break;

                default:
                    return null;
            }

            return new Result
            {
                Title = title,
                SubTitle = subTitle,
                IcoPath = _iconPath,
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
