using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Common.Net;
using BespokeDB.Plugin.Configuration;

namespace BespokeDB.Plugin.Providers
{
    /// <summary>
    /// Provides metadata for BoxSets (Collections) from the BespokeDB API.
    /// </summary>
    public abstract class BespokeBoxSetProviderBase : IRemoteMetadataProvider<BoxSet, ItemLookupInfo>, IHasOrder
    {
        private readonly ILogger _logger;
        private readonly System.Net.Http.HttpClient _httpClient;
        private readonly IHttpClient _embyHttpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="BespokeBoxSetProviderBase"/> class.
        /// </summary>
        protected BespokeBoxSetProviderBase(ILogManager logManager, IHttpClient httpClient)
        {
            _logger = logManager.GetLogger(GetType().Name);
            _httpClient = new System.Net.Http.HttpClient();
            _embyHttpClient = httpClient;
        }

        /// <inheritdoc />
        public abstract string Name { get; }

        /// <summary>
        /// Gets the database ID used by the API.
        /// </summary>
        protected abstract string DatabaseId { get; }

        /// <summary>
        /// Gets the order in which this provider runs. Lower numbers run first.
        /// </summary>
        public int Order => 10;

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(ItemLookupInfo searchInfo, CancellationToken cancellationToken)
        {
            _logger.Info($"BespokeDB BoxSet search requested for: {searchInfo.Name}");
            var results = new List<RemoteSearchResult>();

            var config = Plugin.Instance?.Configuration;
            if (config == null || string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.ClientSecret))
            {
                _logger.Warn("BespokeDB credentials not configured.");
                return results;
            }

            var token = await Plugin.Instance!.TokenCache.GetOrFetchTokenAsync(config.ClientId, config.ClientSecret).ConfigureAwait(false);
            if (string.IsNullOrEmpty(token))
            {
                return results;
            }

            string query = searchInfo.Name;

            var url = $"https://bespokedb.cloud/api/v1/movies/{DatabaseId}?q={Uri.EscapeDataString(query)}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var json = JsonSerializer.Deserialize<JsonElement>(jsonString);

                    if (json.TryGetProperty("results", out var resultsArray) && resultsArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var itemJson in resultsArray.EnumerateArray())
                        {
                            // Only return items explicitly marked as boxsets
                            if (itemJson.TryGetProperty("item_type", out var typeProp) && typeProp.GetString() == "boxset")
                            {
                                var result = new RemoteSearchResult
                                {
                                    Name = itemJson.GetProperty("title").GetString(),
                                    ProviderIds = new ProviderIdDictionary { { $"BespokeDB_{DatabaseId}_boxset", itemJson.GetProperty("id").GetString()! } },
                                    SearchProviderName = Name
                                };

                                if (itemJson.TryGetProperty("primary_image_url", out var imgProp) && imgProp.ValueKind == JsonValueKind.String)
                                {
                                    result.ImageUrl = imgProp.GetString();
                                }

                                results.Add(result);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException($"Error searching BespokeDB collection {DatabaseId}", ex);
            }
            return results;
        }

        /// <inheritdoc />
        public async Task<MetadataResult<BoxSet>> GetMetadata(ItemLookupInfo info, CancellationToken cancellationToken)
        {
            _logger.Info($"BespokeDB BoxSet metadata requested for: {info.Name}");
            var result = new MetadataResult<BoxSet>();

            var searchResults = await GetSearchResults(info, cancellationToken).ConfigureAwait(false);

            var config = Plugin.Instance?.Configuration;
            if (config == null || string.IsNullOrWhiteSpace(config.ClientId)) return result;

            var token = await Plugin.Instance!.TokenCache.GetOrFetchTokenAsync(config.ClientId, config.ClientSecret).ConfigureAwait(false);
            if (string.IsNullOrEmpty(token)) return result;

            string query = info.Name;

            var url = $"https://bespokedb.cloud/api/v1/movies/{DatabaseId}?q={Uri.EscapeDataString(query)}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var json = JsonSerializer.Deserialize<JsonElement>(jsonString);

                    if (json.TryGetProperty("results", out var resultsArray) && resultsArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var itemJson in resultsArray.EnumerateArray())
                        {
                            if (itemJson.TryGetProperty("item_type", out var typeProp) && typeProp.GetString() == "boxset")
                            {
                                result.Item = new BoxSet();

                                if (itemJson.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
                                {
                                    result.Item.Name = titleProp.GetString();
                                }

                                if (itemJson.TryGetProperty("overview", out var overviewProp) && overviewProp.ValueKind == JsonValueKind.String)
                                {
                                    result.Item.Overview = overviewProp.GetString();
                                }

                                if (itemJson.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                                {
                                    result.Item.SetProviderId($"BespokeDB_{DatabaseId}_boxset", idProp.GetString());
                                }

                                result.HasMetadata = true;
                                return result; // Return first valid boxset match
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException($"Error getting BoxSet metadata from BespokeDB collection {DatabaseId}", ex);
            }
            return result;
        }

        /// <inheritdoc />
        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var options = new MediaBrowser.Common.Net.HttpRequestOptions
            {
                Url = url,
                CancellationToken = cancellationToken
            };

            return _embyHttpClient.GetResponse(options);
        }
    }
}
