using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.CodebaseFinder
{
    public class Main : IPlugin, ISettingProvider, IContextMenu
    {
        private PluginInitContext _context;
        private Settings _settings;
        private EverythingSearch _search;
        private ResultBuilder _resultBuilder;
        private LanguageCache _languageCache;
        private CancellationTokenSource _refreshCts;

        // Expose for settings panel rebuild button
        public LanguageCache LanguageCache => _languageCache;
        public EverythingSearch Search => _search;

        public void Init(PluginInitContext context)
        {
            _context = context;
            _settings = context.API.LoadSettingJsonStorage<Settings>();
            _search = new EverythingSearch(_settings);
            _resultBuilder = new ResultBuilder(_settings, context);

            // Initialize language cache with settings for ignored directories
            _languageCache = new LanguageCache(context.CurrentPluginMetadata.PluginDirectory, _settings);

            // Start background refresh of language cache
            StartBackgroundRefresh();
        }

        private void StartBackgroundRefresh()
        {
            _refreshCts?.Cancel();
            _refreshCts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                // Small delay to let the app start
                await Task.Delay(2000, _refreshCts.Token);

                if (!_search.IsAvailable())
                    return;

                // Get all repo paths
                var searchResults = _search.Search();
                var repoPaths = searchResults
                    .Where(r => r.Type == SearchResultType.GitRepository)
                    .Select(r => r.Path)
                    .ToList();

                // Refresh stale entries
                await _languageCache.RefreshStaleEntriesAsync(repoPaths, _refreshCts.Token);

                // Cleanup entries for deleted repos
                _languageCache.CleanupMissingPaths();
            }, _refreshCts.Token);
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            var searchText = query.Search?.Trim() ?? string.Empty;

            // Check if es.exe is available
            if (!_search.IsAvailable())
            {
                results.Add(_resultBuilder.CreateEsNotFoundResult());
                return results;
            }

            // Perform search
            var searchResults = _search.Search();

            // Enrich with language info from cache
            foreach (var result in searchResults)
            {
                if (result.Type == SearchResultType.GitRepository)
                {
                    var cachedLanguage = _languageCache.GetLanguage(result.Path);
                    if (cachedLanguage != Languages.Unknown)
                    {
                        result.Language = cachedLanguage;
                    }
                    else if (_languageCache.IsStale(result.Path))
                    {
                        // Detect synchronously for uncached repos (first time)
                        result.Language = _languageCache.DetectAndCache(result.Path);
                    }
                }
            }

            // Build results with fuzzy matching
            results = _resultBuilder.Build(searchResults, searchText);

            // If no results, show helpful message
            if (results.Count == 0)
            {
                results.Add(_resultBuilder.CreateNoResultsResult(searchText));
            }

            return results;
        }

        public Control CreateSettingPanel()
        {
            return new SettingsControl(_settings, _context, this);
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var contextMenus = new List<Result>();

            if (selectedResult.ContextData is SearchResult searchResult &&
                searchResult.Type == SearchResultType.GitRepository)
            {
                contextMenus.Add(new Result
                {
                    Title = "Rebuild language cache",
                    SubTitle = $"Re-detect language for {Path.GetFileName(searchResult.Path)}",
                    IcoPath = LanguageDetector.GetIconPath(searchResult.Language),
                    Action = _ =>
                    {
                        Task.Run(() =>
                        {
                            var newLanguage = _languageCache.ForceRebuild(searchResult.Path);
                            _context.API.ShowMsg("Language Cache",
                                $"Detected: {newLanguage}",
                                LanguageDetector.GetIconPath(newLanguage));
                        });
                        return true;
                    }
                });
            }

            return contextMenus;
        }
    }
}
