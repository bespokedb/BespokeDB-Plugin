using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Logging;

namespace BespokeDB.Plugin.Cache
{
    /// <summary>
    /// Manages a local cache of movie titles to minimize API requests.
    /// </summary>
    public class MovieCacheManager
    {
        private readonly ILogger _logger;
        private HashSet<string>? _criterionCache;
        private HashSet<string>? _a24Cache;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private bool _isLoaded;
        private readonly object _loadLock = new object();

        private string CacheFilePathCriterion => Path.Combine(Plugin.Instance?.DataFolderPath ?? "", "NightlyCache-Criterion.json");
        private string CacheFilePathA24 => Path.Combine(Plugin.Instance?.DataFolderPath ?? "", "NightlyCache-A24.json");

        /// <summary>
        /// Initializes a new instance of the <see cref="MovieCacheManager"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public MovieCacheManager(ILogger logger)
        {
            _logger = logger;
            _criterionCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _a24Cache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private void EnsureLoaded()
        {
            if (!_isLoaded)
            {
                lock (_loadLock)
                {
                    if (!_isLoaded)
                    {
                        LoadCache();
                        _isLoaded = true;
                    }
                }
            }
        }

        private void LoadCache()
        {
            try
            {
                if (File.Exists(CacheFilePathCriterion))
                {
                    _logger.Info($"Loading nightly cache from {CacheFilePathCriterion}");
                    var json = File.ReadAllText(CacheFilePathCriterion);
                    _criterionCache = ParseCacheJson(json);
                }
                else
                {
                    _logger.Info("Nightly cache not found. Loading Embedded StarterCache-Criterion.json");
                    _criterionCache = LoadEmbeddedCache("BespokeDB.Plugin.StarterCache-Criterion.json");
                }

                if (File.Exists(CacheFilePathA24))
                {
                    _logger.Info($"Loading nightly cache from {CacheFilePathA24}");
                    var json = File.ReadAllText(CacheFilePathA24);
                    _a24Cache = ParseCacheJson(json);
                }
                else
                {
                    _logger.Info("Nightly cache not found. Loading Embedded StarterCache-A24.json");
                    _a24Cache = LoadEmbeddedCache("BespokeDB.Plugin.StarterCache-A24.json");
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error loading movie cache. Falling back to embedded cache.", ex);
                _criterionCache = LoadEmbeddedCache("BespokeDB.Plugin.StarterCache-Criterion.json");
                _a24Cache = LoadEmbeddedCache("BespokeDB.Plugin.StarterCache-A24.json");
            }
        }

        private HashSet<string>? LoadEmbeddedCache(string resourceName)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    _logger.Warn($"{resourceName} not found in embedded resources.");
                    return null;
                }

                using StreamReader reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                return ParseCacheJson(json);
            }
            catch (Exception ex)
            {
                _logger.ErrorException($"Error loading embedded starter cache {resourceName}.", ex);
                return null;
            }
        }

        private HashSet<string>? ParseCacheJson(string json)
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
            {
                var newCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in dataArray.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var s = item.GetString();
                        if (s != null) newCache.Add(s);
                    }
                }
                _logger.Info($"Successfully parsed {newCache.Count} titles.");
                return newCache;
            }
            else
            {
                _logger.Warn("Cache JSON is invalid or missing 'data' array.");
                return null;
            }
        }

        /// <summary>
        /// Synchronizes the nightly cache from the BespokeDB API.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task SyncCacheNightlyAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var config = Plugin.Instance?.Configuration;
                if (config == null || string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.ClientSecret))
                {
                    _logger.Warn("Cannot sync nightly cache. API credentials not configured.");
                    return;
                }

                if (Plugin.Instance == null) return;
                var token = await Plugin.Instance.TokenCache.GetOrFetchTokenAsync(config.ClientId, config.ClientSecret).ConfigureAwait(false);
                if (string.IsNullOrEmpty(token))
                {
                    _logger.Warn("Failed to obtain API token for nightly cache sync.");
                    return;
                }

                using var client = new HttpClient();

                if (config.EnableCriterionCollection)
                {
                    await SyncSpecificCacheAsync(client, token, "criterion", CacheFilePathCriterion, cancellationToken).ConfigureAwait(false);
                }

                if (config.EnableA24Films)
                {
                    await SyncSpecificCacheAsync(client, token, "a24", CacheFilePathA24, cancellationToken).ConfigureAwait(false);
                }

                _isLoaded = true; // Mark as loaded if sync succeeds (or even partially succeeds)
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Failed to sync nightly cache.", ex);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task SyncSpecificCacheAsync(HttpClient client, string token, string collectionName, string filePath, CancellationToken cancellationToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://bespokedb.cloud/api/v1/cache/nightly/{collectionName}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                _logger.Info($"Downloading nightly cache for {collectionName} from API...");
                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                // Validate json before saving
                var parsed = ParseCacheJson(json);
                if (parsed != null)
                {
                    File.WriteAllText(filePath, json);
                    _logger.Info($"Successfully saved nightly cache to {filePath}");
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException($"Failed to sync nightly cache for {collectionName}.", ex);
            }
        }

        /// <summary>
        /// Checks if a movie title is present in the cache for the requested collection.
        /// </summary>
        /// <param name="title">The movie title to check.</param>
        /// <param name="collectionName">The collection (e.g. "criterion_collection" or "a24_films").</param>
        /// <returns>True if the movie is in the cache or if the cache is empty, otherwise false.</returns>
        public bool IsMovieInCache(string title, string collectionName)
        {
            var config = Plugin.Instance?.Configuration;
            if (config == null) return false;

            EnsureLoaded();

            var normalized = NormalizeTitle(title);

            if (collectionName == "criterion_collection" && config.EnableCriterionCollection)
            {
                if (_criterionCache == null || _criterionCache.Count == 0) return true; // Fail open
                return _criterionCache.Contains(normalized);
            }
            else if (collectionName == "a24_films" && config.EnableA24Films)
            {
                if (_a24Cache == null || _a24Cache.Count == 0) return true; // Fail open
                return _a24Cache.Contains(normalized);
            }

            return false;
        }

        /// <summary>
        /// Normalizes a title by removing diacritics and non-alphanumeric characters.
        /// </summary>
        /// <param name="title">The raw title.</param>
        /// <returns>The normalized string.</returns>
        public static string NormalizeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;

            var normalizedString = title.Normalize(NormalizationForm.FormKD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            var noDiacritics = stringBuilder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

            // Remove non-alphanumeric characters
            return Regex.Replace(noDiacritics, "[^a-z0-9]", "");
        }
    }
}
