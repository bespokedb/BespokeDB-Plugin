using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using MediaBrowser.Model.Logging;

namespace BespokeDB.Plugin.Security
{
    /// <summary>
    /// Manages in-memory caching of the JWT token to minimize API calls to BespokeDB.
    /// </summary>
    public class JwtCache
    {
        private readonly ILogger _logger;
        private string? _cachedToken;
        private DateTime _tokenExpiration = DateTime.MinValue;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly HttpClient _httpClient; // Using standard HttpClient per modern .NET 8 practices

        /// <summary>
        /// Initializes a new instance of the <see cref="JwtCache"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public JwtCache(ILogger logger)
        {
            _logger = logger;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        /// <summary>
        /// Gets a valid JWT token, fetching a new one from the API if the current one is expired or missing.
        /// </summary>
        /// <param name="clientId">The API client ID.</param>
        /// <param name="clientSecret">The API client secret.</param>
        /// <returns>A valid JWT bearer token, or null if authentication failed.</returns>
        public async Task<string?> GetOrFetchTokenAsync(string clientId, string clientSecret)
        {
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                _logger.Warn("BespokeDB Client ID or Secret is missing in configuration.");
                return null;
            }

            // Check if token is still valid (with a 5-minute buffer)
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiration.AddMinutes(-5))
            {
                return _cachedToken;
            }

            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                // Double-check inside lock
                if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiration.AddMinutes(-5))
                {
                    return _cachedToken;
                }

                _logger.Info("BespokeDB JWT token expired or missing. Fetching a new token...");

                var requestBody = new
                {
                    client_id = clientId,
                    client_secret = clientSecret
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");

                using var response = await _httpClient.PostAsync("https://bespokedb.cloud/oauth/token", content).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var json = JsonSerializer.Deserialize<JsonElement>(responseString);

                    if (json.TryGetProperty("access_token", out var tokenProp))
                    {
                        _cachedToken = tokenProp.GetString();

                        // Default to 1 hour (3600 seconds) if not provided
                        int expiresIn = 3600;
                        if (json.TryGetProperty("expires_in", out var expiresProp) && expiresProp.TryGetInt32(out var parsedExpires))
                        {
                            expiresIn = parsedExpires;
                        }

                        _tokenExpiration = DateTime.UtcNow.AddSeconds(expiresIn);
                        _logger.Info("Successfully retrieved and cached new BespokeDB JWT token.");


                        return _cachedToken;
                    }
                    throw new Exception("BespokeDB API returned success but 'access_token' was missing from the response.");
                }
                else
                {
                    var errorResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _logger.Error($"Failed to authenticate with BespokeDB API. Status: {response.StatusCode}. Response: {errorResponse}");
                    throw new Exception($"Authentication failed with status {response.StatusCode}. Please verify your Client ID and Secret.");
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.ErrorException("Network error occurred while connecting to BespokeDB API.", ex);
                throw new Exception("Unable to connect to BespokeDB API. Please check your network connection.", ex);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
