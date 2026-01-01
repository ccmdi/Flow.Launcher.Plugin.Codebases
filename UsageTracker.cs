using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Flow.Launcher.Plugin.Codebases
{
    public class UsageTracker
    {
        private readonly string _cachePath;
        private ConcurrentDictionary<string, UsageCacheEntry> _cache;
        private readonly object _saveLock = new();
        private bool _isDirty;

        public UsageTracker(string pluginDirectory)
        {
            _cachePath = Path.Combine(pluginDirectory, "usage_cache.json");
            _cache = new ConcurrentDictionary<string, UsageCacheEntry>(StringComparer.OrdinalIgnoreCase);
            Load();
        }

        public void RecordOpen(string path)
        {
            var entry = _cache.GetOrAdd(path, _ => new UsageCacheEntry());
            entry.LastOpenedAt = DateTime.UtcNow;
            _isDirty = true;
            Save();
        }

        public DateTime? GetLastOpened(string path)
        {
            if (_cache.TryGetValue(path, out var entry))
            {
                return entry.LastOpenedAt;
            }
            return null;
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_cachePath))
                {
                    var json = File.ReadAllText(_cachePath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, UsageCacheEntry>>(json);
                    if (data != null)
                    {
                        _cache = new ConcurrentDictionary<string, UsageCacheEntry>(
                            data, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch
            {
                // Start fresh if cache is corrupted
                _cache = new ConcurrentDictionary<string, UsageCacheEntry>(StringComparer.OrdinalIgnoreCase);
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
                        new Dictionary<string, UsageCacheEntry>(_cache),
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
