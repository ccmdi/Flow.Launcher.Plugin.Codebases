using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Codebases
{
    public class SearchResultCache
    {
        private readonly string _cachePath;
        private readonly EverythingSearch _search;
        private List<CachedSearchResult> _cachedResults;
        private readonly object _lock = new();
        private bool _isRefreshing;
        private DateTime _lastRefresh = DateTime.MinValue;

        public event Action OnCacheUpdated;

        public SearchResultCache(string pluginDirectory, EverythingSearch search)
        {
            _cachePath = Path.Combine(pluginDirectory, "search_cache.json");
            _search = search;
            _cachedResults = new List<CachedSearchResult>();
            Load();
        }

        /// <summary>
        /// Gets cached results immediately, triggers background refresh if needed
        /// </summary>
        public List<SearchResult> GetResults(bool triggerRefresh = true)
        {
            List<SearchResult> results;

            lock (_lock)
            {
                results = ToSearchResults(_cachedResults);
            }

            // Trigger background refresh if it's been a while or cache is empty
            if (triggerRefresh && ShouldRefresh())
            {
                RefreshInBackground();
            }

            return results;
        }

        /// <summary>
        /// Forces a synchronous refresh (for first run or manual refresh)
        /// </summary>
        public List<SearchResult> ForceRefresh()
        {
            var freshResults = _search.Search();
            UpdateCache(freshResults);
            return freshResults;
        }

        private bool ShouldRefresh()
        {
            // Refresh if cache is empty or older than 30 seconds
            return _cachedResults.Count == 0 ||
                   DateTime.UtcNow - _lastRefresh > TimeSpan.FromSeconds(30);
        }

        private void RefreshInBackground()
        {
            lock (_lock)
            {
                if (_isRefreshing)
                    return;
                _isRefreshing = true;
            }

            Task.Run(() =>
            {
                try
                {
                    var freshResults = _search.Search();
                    UpdateCache(freshResults);
                    OnCacheUpdated?.Invoke();
                }
                finally
                {
                    lock (_lock)
                    {
                        _isRefreshing = false;
                    }
                }
            });
        }

        private void UpdateCache(List<SearchResult> results)
        {
            var cached = new List<CachedSearchResult>();
            foreach (var r in results)
            {
                cached.Add(new CachedSearchResult
                {
                    Path = r.Path,
                    Type = r.Type,
                    CustomIconPath = r.CustomIconPath
                });
            }

            lock (_lock)
            {
                _cachedResults = cached;
                _lastRefresh = DateTime.UtcNow;
            }

            Save();
        }

        private List<SearchResult> ToSearchResults(List<CachedSearchResult> cached)
        {
            var results = new List<SearchResult>();
            foreach (var c in cached)
            {
                results.Add(new SearchResult
                {
                    Path = c.Path,
                    Type = c.Type,
                    CustomIconPath = c.CustomIconPath
                });
            }
            return results;
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_cachePath))
                {
                    var json = File.ReadAllText(_cachePath);
                    var data = JsonSerializer.Deserialize<List<CachedSearchResult>>(json);
                    if (data != null)
                    {
                        _cachedResults = data;
                    }
                }
            }
            catch
            {
                _cachedResults = new List<CachedSearchResult>();
            }
        }

        private void Save()
        {
            try
            {
                List<CachedSearchResult> toSave;
                lock (_lock)
                {
                    toSave = new List<CachedSearchResult>(_cachedResults);
                }

                var json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_cachePath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }
    }
}
