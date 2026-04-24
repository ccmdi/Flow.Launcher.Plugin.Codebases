using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Codebases
{
    public class LanguageCache
    {
        private readonly string _cachePath;
        private readonly Settings _settings;
        private readonly TimeSpan _staleThreshold;
        private ConcurrentDictionary<string, LanguageCacheEntry> _cache;
        private readonly object _saveLock = new();
        private bool _isDirty;

        public LanguageCache(string pluginDirectory, Settings settings, TimeSpan? staleThreshold = null)
        {
            _cachePath = Path.Combine(pluginDirectory, "language_cache.json");
            _settings = settings;
            _staleThreshold = staleThreshold ?? TimeSpan.FromHours(24);
            _cache = new ConcurrentDictionary<string, LanguageCacheEntry>(StringComparer.OrdinalIgnoreCase);

            Load();
        }

        private LanguageDetector CreateDetector()
        {
            return new LanguageDetector(_settings.IgnoredDirectories);
        }

        /// <summary>
        /// Gets the cached languages for a path, or Unknown if not cached
        /// </summary>
        public string[] GetLanguages(string path)
        {
            if (_cache.TryGetValue(path, out var entry))
                return entry.Languages;
            return new[] { Languages.Unknown };
        }

        /// <summary>
        /// Gets the cached remote url for a path, or empty string if not cached
        /// </summary>
        public string GetRemoteUrl(string path)
        {
            if (_cache.TryGetValue(path, out var entry))
                return entry.RemoteUrl;
            return "";
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
        /// Detects and caches languages for a path
        /// </summary>
        public string[] DetectAndCache(string path)
        {
            var detector = CreateDetector();
            var languages = detector.Detect(path);
            var remoteUrl = ParseGitRemoteUrl(path);

            _cache[path] = new LanguageCacheEntry
            {
                Languages = languages,
                RemoteUrl = remoteUrl,
                DetectedAt = DateTime.UtcNow
            };

            _isDirty = true;
            return languages;
        }

        /// <summary>
        /// Parses the origin remote URL from .git/config
        /// </summary>
        private static string ParseGitRemoteUrl(string repoPath)
        {
            try
            {
                var gitConfigPath = Path.Combine(repoPath, ".git", "config");
                if (!File.Exists(gitConfigPath))
                    return "";

                var content = File.ReadAllText(gitConfigPath);

                // Match [remote "origin"] section and extract url
                var match = Regex.Match(content,
                    @"\[remote\s+""origin""\].*?url\s*=\s*(.+?)(?:\r?\n|$)",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    var url = match.Groups[1].Value.Trim();
                    return NormalizeGitUrl(url);
                }
            }
            catch
            {
                // Silently fail
            }

            return "";
        }

        /// <summary>
        /// Converts git URLs to browser-friendly HTTPS URLs
        /// </summary>
        private static string NormalizeGitUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return "";

            // Convert SSH format: git@github.com:user/repo.git -> https://github.com/user/repo
            if (url.StartsWith("git@"))
            {
                url = Regex.Replace(url, @"^git@([^:]+):(.+)$", "https://$1/$2");
            }

            // Remove .git suffix
            if (url.EndsWith(".git"))
            {
                url = url.Substring(0, url.Length - 4);
            }

            return url;
        }

        /// <summary>
        /// Force rebuilds the cache for a single path
        /// </summary>
        public string[] ForceRebuild(string path)
        {
            // Remove existing entry to force re-detection
            _cache.TryRemove(path, out _);
            return DetectAndCache(path);
        }

        /// <summary>
        /// Rebuilds cache for all paths in the background
        /// </summary>
        public Task RebuildAllAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var pathList = paths.ToList();
                var rebuilt = 0;

                foreach (var path in pathList)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // Force rebuild regardless of staleness
                    _cache.TryRemove(path, out _);
                    DetectAndCache(path);
                    rebuilt++;

                    // Save periodically
                    if (rebuilt % 20 == 0)
                        Save();
                }

                // Final save
                if (_isDirty)
                    Save();
            }, cancellationToken);
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
