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
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Logging;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Configuration;
using BespokeDB.Plugin.Configuration;

namespace BespokeDB.Plugin.Providers
{
    /// <summary>
    /// Provides images for movies from the BespokeDB API.
    /// </summary>
    public abstract class BespokeImageProviderBase : IRemoteImageProvider, IHasOrder
    {
        private readonly ILogger _logger;
        private readonly System.Net.Http.HttpClient _httpClient;
        private readonly IHttpClient _embyHttpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="BespokeImageProviderBase"/> class.
        /// </summary>
        /// <param name="logManager">The log manager.</param>
        /// <param name="httpClient">The Emby HTTP client.</param>
        protected BespokeImageProviderBase(ILogManager logManager, IHttpClient httpClient)
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
        public bool Supports(BaseItem item)
        {
            return item is Movie || item is BoxSet;
        }

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new List<ImageType>
            {
                ImageType.Primary,
                ImageType.Backdrop
            };
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
        {
            _logger.Info($"BespokeDB images requested for: {item.Name} (Type: {item.GetType().Name})");
            var images = new List<RemoteImageInfo>();

            var config = Plugin.Instance?.Configuration;
            if (config == null || string.IsNullOrWhiteSpace(config.ClientId)) return images;

            var token = await Plugin.Instance!.TokenCache.GetOrFetchTokenAsync(config.ClientId, config.ClientSecret).ConfigureAwait(false);
            if (string.IsNullOrEmpty(token)) return images;

            string query = item.Name;
            if (item is Movie && item.ProductionYear.HasValue) query += $" ({item.ProductionYear.Value})";

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
                        foreach (var movieJson in resultsArray.EnumerateArray())
                        {
                            bool isMatch = false;
                            if (item is BoxSet)
                            {
                                isMatch = movieJson.TryGetProperty("item_type", out var typeProp) && typeProp.GetString() == "boxsets";
                            }
                            else
                            {
                                bool isBoxset = movieJson.TryGetProperty("item_type", out var typeProp) && typeProp.GetString() == "boxsets";
                                isMatch = !isBoxset;
                            }

                            if (isMatch)
                            {
                                bool wantsBanner = item is BoxSet && config.CollectionImageType == CollectionImageFormat.Banner;
                                string primaryImageUrl = string.Empty;
                                string bannerUrl = string.Empty;

                                if (movieJson.TryGetProperty("primary_image_url", out var primaryProp) && primaryProp.ValueKind == JsonValueKind.String)
                                {
                                    primaryImageUrl = primaryProp.GetString() ?? string.Empty;
                                }

                                if (movieJson.TryGetProperty("images", out var imagesProp) && imagesProp.ValueKind == JsonValueKind.Object)
                                {
                                    if (imagesProp.TryGetProperty("backdrops", out var backdropsProp) && backdropsProp.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var backdrop in backdropsProp.EnumerateArray())
                                        {
                                            if (backdrop.ValueKind == JsonValueKind.String && backdrop.GetString() != "none")
                                            {
                                                string bdUrl = backdrop.GetString() ?? string.Empty;
                                                if (string.IsNullOrEmpty(bannerUrl)) bannerUrl = bdUrl;

                                                // Add as backdrop anyway
                                                images.Add(new RemoteImageInfo
                                                {
                                                    ProviderName = Name,
                                                    Type = ImageType.Backdrop,
                                                    Url = bdUrl,
                                                    ThumbnailUrl = bdUrl
                                                });
                                            }
                                        }
                                    }
                                }

                                if (wantsBanner && !string.IsNullOrEmpty(bannerUrl))
                                {
                                    // Set the first backdrop as Primary instead
                                    images.Add(new RemoteImageInfo
                                    {
                                        ProviderName = Name,
                                        Type = ImageType.Primary,
                                        Url = bannerUrl,
                                        ThumbnailUrl = bannerUrl
                                    });

                                    // Still add the poster image, but as a secondary image (e.g. Box)
                                    if (!string.IsNullOrEmpty(primaryImageUrl))
                                    {
                                        images.Add(new RemoteImageInfo
                                        {
                                            ProviderName = Name,
                                            Type = ImageType.Box,
                                            Url = primaryImageUrl,
                                            ThumbnailUrl = primaryImageUrl
                                        });
                                    }
                                }
                                else if (!string.IsNullOrEmpty(primaryImageUrl))
                                {
                                    // Default behavior (Poster mode, or Movie)
                                    images.Add(new RemoteImageInfo
                                    {
                                        ProviderName = Name,
                                        Type = ImageType.Primary,
                                        Url = primaryImageUrl,
                                        ThumbnailUrl = primaryImageUrl
                                    });
                                }

                                return images; // Return images for first exact match
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException($"Error getting images from BespokeDB collection {DatabaseId}", ex);
            }
            return images;
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
