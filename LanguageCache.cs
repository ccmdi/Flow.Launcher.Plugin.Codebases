using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.CodebaseFinder
{
    public class LanguageCacheEntry
    {
        public string Language { get; set; } = Languages.Unknown;
        public DateTime DetectedAt { get; set; } = DateTime.MinValue;
    }

    public class LanguageCache
    {
        private readonly string _cachePath;
        private readonly LanguageDetector _detector;
        private readonly TimeSpan _staleThreshold;
        private ConcurrentDictionary<string, LanguageCacheEntry> _cache;
        private readonly object _saveLock = new();
        private bool _isDirty;

        public LanguageCache(string pluginDirectory, TimeSpan? staleThreshold = null)
        {
            _cachePath = Path.Combine(pluginDirectory, "language_cache.json");
            _detector = new LanguageDetector();
            _staleThreshold = staleThreshold ?? TimeSpan.FromHours(24);
            _cache = new ConcurrentDictionary<string, LanguageCacheEntry>(StringComparer.OrdinalIgnoreCase);

            Load();
        }

        /// <summary>
        /// Gets the cached language for a path, or Unknown if not cached
        /// </summary>
        public string GetLanguage(string path)
        {
            if (_cache.TryGetValue(path, out var entry))
                return entry.Language;
            return Languages.Unknown;
        }

        /// <summary>
        /// Checks if a path needs to be re-indexed
        /// </summary>
        public bool IsStale(string path)
        {
            if (!_cache.TryGetValue(path, out var entry))
                return true;

            return DateTime.UtcNow - entry.DetectedAt > _staleThreshold;
        }

        /// <summary>
        /// Detects and caches the language for a path
        /// </summary>
        public string DetectAndCache(string path)
        {
            var language = _detector.Detect(path);

            _cache[path] = new LanguageCacheEntry
            {
                Language = language,
                DetectedAt = DateTime.UtcNow
            };

            _isDirty = true;
            return language;
        }

        /// <summary>
        /// Refreshes stale entries in the background
        /// </summary>
        public Task RefreshStaleEntriesAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var refreshed = 0;

                foreach (var path in paths)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (IsStale(path))
                    {
                        DetectAndCache(path);
                        refreshed++;

                        // Save periodically during large refreshes
                        if (refreshed % 20 == 0)
                            Save();
                    }
                }

                // Final save
                if (_isDirty)
                    Save();
            }, cancellationToken);
        }

        /// <summary>
        /// Removes entries for paths that no longer exist
        /// </summary>
        public void CleanupMissingPaths()
        {
            var toRemove = new List<string>();

            foreach (var kvp in _cache)
            {
                if (!Directory.Exists(kvp.Key) && !File.Exists(kvp.Key))
                    toRemove.Add(kvp.Key);
            }

            foreach (var path in toRemove)
            {
                _cache.TryRemove(path, out _);
                _isDirty = true;
            }

            if (_isDirty)
                Save();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_cachePath))
                {
                    var json = File.ReadAllText(_cachePath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, LanguageCacheEntry>>(json);

                    if (data != null)
                    {
                        _cache = new ConcurrentDictionary<string, LanguageCacheEntry>(
                            data, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch
            {
                // Start fresh if cache is corrupted
                _cache = new ConcurrentDictionary<string, LanguageCacheEntry>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public void Save()
        {
            if (!_isDirty)
                return;

            lock (_saveLock)
            {
                try
                {
                    var json = JsonSerializer.Serialize(
                        new Dictionary<string, LanguageCacheEntry>(_cache),
                        new JsonSerializerOptions { WriteIndented = true });

                    File.WriteAllText(_cachePath, json);
                    _isDirty = false;
                }
                catch
                {
                    // Ignore save errors
                }
            }
        }
    }
}
