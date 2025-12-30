using System.Collections.Generic;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.CodebaseFinder
{
    public class Main : IPlugin, ISettingProvider
    {
        private PluginInitContext _context;
        private Settings _settings;
        private EverythingSearch _search;
        private ResultBuilder _resultBuilder;

        public void Init(PluginInitContext context)
        {
            _context = context;
            _settings = context.API.LoadSettingJsonStorage<Settings>();
            _search = new EverythingSearch(_settings);
            _resultBuilder = new ResultBuilder(_settings, context);
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
            return new SettingsControl(_settings, _context);
        }
    }
}
