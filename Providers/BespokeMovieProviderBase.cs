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

namespace BespokeDB.Plugin.Providers
{
    /// <summary>
    /// Provides metadata for movies from the BespokeDB API.
    /// </summary>
    public abstract class BespokeMovieProviderBase : IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder
    {
        private readonly ILogger _logger;
        private readonly System.Net.Http.HttpClient _httpClient;
        private readonly IHttpClient _embyHttpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="BespokeMovieProviderBase"/> class.
        /// </summary>
        /// <param name="logManager">The log manager.</param>
        /// <param name="httpClient">The Emby HTTP client.</param>
        protected BespokeMovieProviderBase(ILogManager logManager, IHttpClient httpClient)
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
        /// Gets the name of the studio.
        /// </summary>
        protected abstract string StudioName { get; }

        /// <summary>
        /// Gets the order in which this provider runs. Lower numbers run first.
        /// </summary>
        public int Order => 10;

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        {
            _logger.Info($"BespokeDB search requested for: {searchInfo.Name}");
            var results = new List<RemoteSearchResult>();

            if (Plugin.Instance?.CacheManager != null && !Plugin.Instance.CacheManager.IsMovieInCache(searchInfo.Name, DatabaseId))
            {
                _logger.Info($"Skipping BespokeDB search API call for '{searchInfo.Name}' - not found in local cache.");
                return results;
            }

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
            if (searchInfo.Year.HasValue && !query.Contains(searchInfo.Year.Value.ToString()))
            {
                query += $" ({searchInfo.Year.Value})";
            }

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
                            var result = new RemoteSearchResult
                            {
                                Name = movieJson.GetProperty("title").GetString(),
                                ProviderIds = new ProviderIdDictionary { { $"BespokeDB_{DatabaseId}", movieJson.GetProperty("id").GetString()! } },
                                SearchProviderName = Name
                            };

                            if (movieJson.TryGetProperty("release_year", out var yearProp) && yearProp.ValueKind == JsonValueKind.Number)
                            {
                                result.ProductionYear = yearProp.GetInt32();
                            }

                            if (movieJson.TryGetProperty("primary_image_url", out var imgProp) && imgProp.ValueKind == JsonValueKind.String)
                            {
                                result.ImageUrl = imgProp.GetString();
                            }

                            results.Add(result);
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
        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            _logger.Info($"BespokeDB metadata requested for: {info.Name}");
            var result = new MetadataResult<Movie>();

            if (Plugin.Instance?.CacheManager != null && !Plugin.Instance.CacheManager.IsMovieInCache(info.Name, DatabaseId))
            {
                _logger.Info($"Skipping BespokeDB metadata API call for '{info.Name}' - not found in local cache.");
                return result;
            }

            var searchResults = await GetSearchResults(info, cancellationToken).ConfigureAwait(false);

            var config = Plugin.Instance?.Configuration;
            if (config == null || string.IsNullOrWhiteSpace(config.ClientId)) return result;

            var token = await Plugin.Instance!.TokenCache.GetOrFetchTokenAsync(config.ClientId, config.ClientSecret).ConfigureAwait(false);
            if (string.IsNullOrEmpty(token)) return result;

            string query = info.Name;
            if (info.Year.HasValue && !query.Contains(info.Year.Value.ToString()))
            {
                query += $" ({info.Year.Value})";
            }

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
                        var enumerator = resultsArray.EnumerateArray();
                        if (enumerator.MoveNext())
                        {
                            var movieJson = enumerator.Current;
                            result.Item = new Movie();

                            if (movieJson.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
                            {
                                result.Item.Name = titleProp.GetString();
                            }

                            if (movieJson.TryGetProperty("overview", out var overviewProp) && overviewProp.ValueKind == JsonValueKind.String)
                            {
                                result.Item.Overview = overviewProp.GetString();
                            }

                            if (movieJson.TryGetProperty("release_year", out var yearProp) && yearProp.ValueKind == JsonValueKind.Number)
                            {
                                result.Item.ProductionYear = yearProp.GetInt32();
                            }

                            if (movieJson.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                            {
                                result.Item.SetProviderId($"BespokeDB_{DatabaseId}", idProp.GetString());
                            }

                            result.Item.AddStudio(StudioName);
                            if (config.EnableCollections && config.GroupByStudioCollection)
                            {
                                result.Item.AddCollection(StudioName);
                                result.Item.SetProviderId("BespokeStudioCollection", StudioName);
                            }

                            if (movieJson.TryGetProperty("cast", out var castArray) && castArray.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var castMember in castArray.EnumerateArray())
                                {
                                    if (castMember.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                                    {
                                        var person = new PersonInfo { Name = nameProp.GetString(), Type = PersonType.Actor };
                                        if (castMember.TryGetProperty("role", out var roleProp) && roleProp.ValueKind == JsonValueKind.String)
                                        {
                                            person.Role = roleProp.GetString();
                                        }
                                        result.AddPerson(person);
                                    }
                                }
                            }

                            if (movieJson.TryGetProperty("crew", out var crewArray) && crewArray.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var crewMember in crewArray.EnumerateArray())
                                {
                                    if (crewMember.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                                    {
                                        var roleStr = crewMember.TryGetProperty("role", out var roleProp) && roleProp.ValueKind == JsonValueKind.String ? roleProp.GetString() : "";

                                        PersonType type = PersonType.GuestStar;
                                        if (roleStr == "Director") type = PersonType.Director;
                                        else if (roleStr == "Producer") type = PersonType.Producer;
                                        else if (roleStr == "Screenplay" || roleStr == "Writer") type = PersonType.Writer;
                                        else if (roleStr == "Music" || roleStr == "Composer") type = PersonType.Composer;

                                        var person = new PersonInfo { Name = nameProp.GetString(), Type = type, Role = roleStr };
                                        result.AddPerson(person);
                                    }
                                }
                            }

                            if (config.EnableCollections)
                            {
                                if (movieJson.TryGetProperty("collections", out var collectionsArray) && collectionsArray.ValueKind == JsonValueKind.Array)
                                {
                                    int i = 0;
                                    foreach (var colProp in collectionsArray.EnumerateArray())
                                    {
                                        if (colProp.ValueKind == JsonValueKind.String)
                                        {
                                            result.Item.SetProviderId($"BespokeCollection_{DatabaseId}_{i}", colProp.GetString());
                                            i++;
                                        }
                                    }
                                }
                            }

                            result.HasMetadata = true;
                            return result; // Return first match
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException($"Error getting metadata from BespokeDB collection {DatabaseId}", ex);
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
