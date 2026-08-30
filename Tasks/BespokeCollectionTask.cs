using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Model.Logging;
using BespokeDB.Plugin.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using System.Net.Http.Headers;
using System.IO;
using MediaBrowser.Model.Configuration;

namespace BespokeDB.Plugin.Tasks
{
    /// <summary>
    /// Scheduled task to scan for BespokeDB movies and safely create collections respecting Emby's native thresholds.
    /// </summary>
    public class BespokeCollectionTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ICollectionManager _collectionManager;
        private readonly MediaBrowser.Controller.IServerApplicationPaths _appPaths;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="BespokeCollectionTask"/> class.
        /// </summary>
        public BespokeCollectionTask(ILibraryManager libraryManager, ICollectionManager collectionManager, MediaBrowser.Controller.IServerApplicationPaths appPaths, ILogManager logManager)
        {
            _libraryManager = libraryManager;
            _collectionManager = collectionManager;
            _appPaths = appPaths;
            _logger = logManager.GetLogger(GetType().Name);
        }

        /// <inheritdoc />
        public string Name => "Sync BespokeDB Collections";
        /// <inheritdoc />
        public string Key => "BespokeDBSyncCollectionsTask";
        /// <inheritdoc />
        public string Description => "Scans movie libraries and creates collections for BespokeDB box sets based on Emby thresholds.";
        /// <inheritdoc />
        public string Category => "Library";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(24).Ticks
                }
            };
        }

        /// <inheritdoc />
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var config = Plugin.Instance?.Configuration;
            if (config == null || !config.EnableCollections)
            {
                _logger.Info("BespokeDB collections are disabled in plugin configuration.");
                return;
            }

            var movies = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { nameof(Movie) },
                IsVirtualItem = false,
                Recursive = true,
                HasAnyProviderId = new[] { "BespokeDB_criterion_collection", "BespokeDB_a24_films" }
            }).Cast<Movie>().ToList();

            _logger.Info($"Found {movies.Count} movies using HasAnyProviderId query.");

            var collectionsToCreate = new Dictionary<string, List<long>>();
            var boxsetSlugs = new Dictionary<string, (string db, string slug)>();

            // (collName) -> list of (movieId, minItems)
            var movieCollectionInfo = new Dictionary<string, List<(long MovieId, int MinItems)>>();

            int count = 0;
            foreach (var movie in movies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Log movie being processed
                _logger.Info($"Processing movie: {movie.Name} (ID: {movie.InternalId})");

                // Check if the movie has any BespokeDB metadata attached to it
                if (movie.ProviderIds != null && (movie.ProviderIds.ContainsKey("BespokeDB_criterion_collection") || movie.ProviderIds.ContainsKey("BespokeDB_a24_films")))
                {
                    var movieCollections = new List<string>();

                    if (movie.ProviderIds.TryGetValue("BespokeStudioCollection", out var studioColl))
                    {
                        movieCollections.Add(studioColl);
                    }
                    
                    // If metadata hasn't been fully refreshed, deduce the studio collection from the presence of the provider ID
                    if (Plugin.Instance!.Configuration.GroupByStudioCollection)
                    {
                        if (movie.ProviderIds.ContainsKey("BespokeDB_criterion_collection") && !movieCollections.Contains("Criterion Collection"))
                        {
                            movieCollections.Add("Criterion Collection");
                        }
                        if (movie.ProviderIds.ContainsKey("BespokeDB_a24_films") && !movieCollections.Contains("A24"))
                        {
                            movieCollections.Add("A24");
                        }
                    }

                    foreach (var kvp in movie.ProviderIds)
                    {
                        if (kvp.Key.StartsWith("BespokeCollection_"))
                        {
                            // Key format: BespokeCollection_{db}_{i}
                            var parts = kvp.Key.Split('_');
                            if (parts.Length >= 3)
                            {
                                string db = parts[1] + "_" + parts[2]; // e.g. criterion_collection
                                string slug = kvp.Value;

                                // We temporarily use the slug as the collection name until we resolve it
                                movieCollections.Add(slug);
                                if (!boxsetSlugs.ContainsKey(slug))
                                {
                                    boxsetSlugs[slug] = (db, slug);
                                }
                            }
                        }
                    }

                    _logger.Info($"Movie {movie.Name} has {movieCollections.Count} Collections found via ProviderIds.");

                    var libraryOptions = _libraryManager.GetLibraryOptions(movie);
                    int minItems = libraryOptions?.MinCollectionItems ?? 2;

                    foreach (var collName in movieCollections)
                    {
                        if (string.IsNullOrWhiteSpace(collName)) continue;

                        if (!movieCollectionInfo.ContainsKey(collName))
                        {
                            movieCollectionInfo[collName] = new List<(long MovieId, int MinItems)>();
                        }

                        // Prevent duplicate entries
                        if (!movieCollectionInfo[collName].Any(m => m.MovieId == movie.InternalId))
                        {
                            movieCollectionInfo[collName].Add((movie.InternalId, minItems));
                        }
                    }
                }

                count++;
                progress.Report((double)count / movies.Count * 25);
            }

            // Process thresholds
            foreach (var kvp in movieCollectionInfo)
            {
                string collName = kvp.Key;
                var items = kvp.Value;
                var approvedMovieIds = new List<long>();

                int minItems = items.Min(i => i.MinItems);
                if (items.Count >= minItems)
                {
                    approvedMovieIds.AddRange(items.Select(i => i.MovieId));
                }

                if (approvedMovieIds.Count > 0)
                {
                    collectionsToCreate[collName] = approvedMovieIds.Distinct().ToList();
                }
            }

            int createdCount = 0;

            var token = await Plugin.Instance!.TokenCache.GetOrFetchTokenAsync(config.ClientId, config.ClientSecret).ConfigureAwait(false);
            using var httpClient = new HttpClient();
            using var imageHttpClient = new HttpClient();
            if (!string.IsNullOrEmpty(token))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Now create the BoxSets using ICollectionManager for movies that met the threshold
            foreach (var kvp in collectionsToCreate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var collectionIdentifier = kvp.Key;
                var movieIds = kvp.Value;

                string resolvedName = collectionIdentifier;
                string? overview = null;
                string? imageUrl = null;
                string? itemUrl = null;
                var backdrops = new List<string>();

                (string db, string slug) slugInfo = default;
                if (boxsetSlugs.TryGetValue(collectionIdentifier, out slugInfo))
                {
                    try
                    {
                        string url = $"https://bespokedb.cloud/api/v1/items/{slugInfo.db}/{slugInfo.slug}";
                        var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                        if (response.IsSuccessStatusCode)
                        {
                            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                            var json = JsonSerializer.Deserialize<JsonElement>(jsonString);

                            if (json.TryGetProperty("result", out var resultObj) && resultObj.ValueKind == JsonValueKind.Object)
                            {
                                if (resultObj.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
                                {
                                    var str = titleProp.GetString();
                                    if (str != null) resolvedName = str;
                                }
                                if (resultObj.TryGetProperty("overview", out var overviewProp) && overviewProp.ValueKind == JsonValueKind.String)
                                {
                                    overview = overviewProp.GetString();
                                }
                                if (resultObj.TryGetProperty("movie_link", out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
                                {
                                    itemUrl = urlProp.GetString();
                                }
                                if (resultObj.TryGetProperty("primary_image_url", out var imgProp) && imgProp.ValueKind == JsonValueKind.String)
                                {
                                    imageUrl = imgProp.GetString();
                                }
                                if (resultObj.TryGetProperty("images", out var imagesProp) && imagesProp.ValueKind == JsonValueKind.Object)
                                {
                                    if (imagesProp.TryGetProperty("backdrops", out var backdropsArray) && backdropsArray.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var bgProp in backdropsArray.EnumerateArray())
                                        {
                                            if (bgProp.ValueKind == JsonValueKind.String)
                                            {
                                                var bgStr = bgProp.GetString();
                                                if (bgStr != null) backdrops.Add(bgStr);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            _logger.Warn($"Failed to fetch boxset metadata for {slugInfo.slug}. Status: {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorException($"Error resolving boxset {slugInfo.slug}", ex);
                    }
                }
                else if (collectionIdentifier == "Criterion Collection" || collectionIdentifier == "A24")
                {
                    string cleanToken = string.IsNullOrEmpty(token) ? string.Empty : (token.StartsWith("Bearer ") ? token.Substring(7) : token);
                    string queryToken = Uri.EscapeDataString(cleanToken);
                    string baseEndpoint = "https://bespokedb.cloud/api/v1/media/image?path=";
                    
                    if (collectionIdentifier == "Criterion Collection")
                    {
                        resolvedName = "Criterion Collection";
                        overview = "The Criterion Collection is a continuing series of important classic and contemporary films on home video. Its editions often include restored transfers, commentary tracks, and supplemental features.";
                        imageUrl = $"{baseEndpoint}assets/studio_collections/criterion_poster.jpg&token={queryToken}";
                        backdrops.Add($"{baseEndpoint}assets/studio_collections/criterion_banner.jpg&token={queryToken}");
                        slugInfo = ("criterion_collection", "criterion_studio_rollup");
                    }
                    else if (collectionIdentifier == "A24")
                    {
                        resolvedName = "A24";
                        overview = "A24 is an American independent entertainment company that specializes in film and television production, as well as film distribution.";
                        imageUrl = $"{baseEndpoint}assets/studio_collections/a24_poster.jpeg&token={queryToken}";
                        backdrops.Add($"{baseEndpoint}assets/studio_collections/a24_banner.jpeg&token={queryToken}");
                        slugInfo = ("a24_films", "a24_studio_rollup");
                    }
                }

                try
                {
                    _logger.Info($"Ensuring collection exists: '{resolvedName}' with {movieIds.Count} movies.");

                    var options = new CollectionCreationOptions
                    {
                        Name = resolvedName,
                        ItemIdList = movieIds.ToArray()
                    };

                    var collection = await _collectionManager.CreateCollection(options);

                    if (collection != null)
                    {
                        bool metadataUpdated = false;
                        if (!string.IsNullOrEmpty(overview) && collection.Overview != overview)
                        {
                            collection.Overview = overview;
                            metadataUpdated = true;
                        }

                        if (collection.ProviderIds != null && !string.IsNullOrEmpty(itemUrl))
                        {
                            var currentUrl = collection.ProviderIds.TryGetValue("BespokeDB_URL", out var val) ? val : null;
                            if (currentUrl != itemUrl)
                            {
                                collection.ProviderIds["BespokeDB_URL"] = itemUrl;
                                metadataUpdated = true;
                            }
                        }

                        bool wantsBanner = config.CollectionImageType == CollectionImageFormat.Banner;
                        string? primaryUrlToDownload = imageUrl;
                        string? boxUrlToDownload = string.Empty;

                        // Filter out any backdrops that are literally 'none' or contain 'path=none' (from the image proxy)
                        backdrops = backdrops.Where(b => !string.IsNullOrEmpty(b) && b != "none" && !b.Contains("path=none")).ToList();

                        if (wantsBanner)
                        {
                            if (backdrops.Count > 0)
                            {
                                primaryUrlToDownload = backdrops[0];
                                boxUrlToDownload = imageUrl;
                            }
                            else
                            {
                                _logger.Info($"16:9 Banner requested for collection '{resolvedName}', but no backdrop exists. Falling back to 2:3 poster.");
                                primaryUrlToDownload = imageUrl;
                                boxUrlToDownload = string.Empty;
                            }
                        }

                        if (!string.IsNullOrEmpty(primaryUrlToDownload) && primaryUrlToDownload != "none" && !primaryUrlToDownload.Contains("path=none"))
                        {
                            try
                            {
                                string extension = Path.GetExtension(new Uri(primaryUrlToDownload).AbsolutePath);
                                if (string.IsNullOrEmpty(extension)) extension = ".jpg";
                                string targetDir = Path.Combine(_appPaths.DataPath, "BespokeDB_Images");
                                string targetPath = Path.Combine(targetDir, $"collection_{slugInfo.slug}_primary{extension}");

                                if (!File.Exists(targetPath))
                                {
                                    var imageBytes = await imageHttpClient.GetByteArrayAsync(primaryUrlToDownload, cancellationToken).ConfigureAwait(false);
                                    Directory.CreateDirectory(targetDir);
                                    await File.WriteAllBytesAsync(targetPath, imageBytes, cancellationToken).ConfigureAwait(false);
                                }

                                if (File.Exists(targetPath))
                                {
                                    var imageInfo = new MediaBrowser.Controller.Entities.ItemImageInfo
                                    {
                                        Path = targetPath,
                                        Type = MediaBrowser.Model.Entities.ImageType.Primary,
                                        DateModified = File.GetLastWriteTimeUtc(targetPath)
                                    };
                                    collection.SetImage(imageInfo, 0);
                                    metadataUpdated = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.ErrorException($"Failed to download primary image for collection '{resolvedName}'. URL: {primaryUrlToDownload}", ex);
                            }
                        }

                        if (!string.IsNullOrEmpty(boxUrlToDownload) && boxUrlToDownload != "none" && !boxUrlToDownload.Contains("path=none"))
                        {
                            try
                            {
                                string extension = Path.GetExtension(new Uri(boxUrlToDownload).AbsolutePath);
                                if (string.IsNullOrEmpty(extension)) extension = ".jpg";
                                string targetDir = Path.Combine(_appPaths.DataPath, "BespokeDB_Images");
                                string targetPath = Path.Combine(targetDir, $"collection_{slugInfo.slug}_box{extension}");

                                if (!File.Exists(targetPath))
                                {
                                    var imageBytes = await imageHttpClient.GetByteArrayAsync(boxUrlToDownload, cancellationToken).ConfigureAwait(false);
                                    Directory.CreateDirectory(targetDir);
                                    await File.WriteAllBytesAsync(targetPath, imageBytes, cancellationToken).ConfigureAwait(false);
                                }

                                if (File.Exists(targetPath))
                                {
                                    var imageInfo = new MediaBrowser.Controller.Entities.ItemImageInfo
                                    {
                                        Path = targetPath,
                                        Type = MediaBrowser.Model.Entities.ImageType.Box,
                                        DateModified = File.GetLastWriteTimeUtc(targetPath)
                                    };
                                    collection.SetImage(imageInfo, 0);
                                    metadataUpdated = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.ErrorException($"Failed to download box image for collection '{resolvedName}'. URL: {boxUrlToDownload}", ex);
                            }
                        }

                        for (int i = 0; i < backdrops.Count; i++)
                        {
                            string bgUrl = backdrops[i];
                            // If we used the first backdrop as the Primary (Banner) image, we might still want it as a backdrop, 
                            // or we can just add them all. The BespokeImageProvider adds them all, so we will too.
                            try
                            {
                                string extension = Path.GetExtension(new Uri(bgUrl).AbsolutePath);
                                if (string.IsNullOrEmpty(extension)) extension = ".jpg";
                                string targetDir = Path.Combine(_appPaths.DataPath, "BespokeDB_Images");
                                string targetPath = Path.Combine(targetDir, $"collection_bg_{slugInfo.slug}_{i}{extension}");

                                if (!File.Exists(targetPath))
                                {
                                    var imageBytes = await imageHttpClient.GetByteArrayAsync(bgUrl, cancellationToken).ConfigureAwait(false);
                                    Directory.CreateDirectory(targetDir);
                                    await File.WriteAllBytesAsync(targetPath, imageBytes, cancellationToken).ConfigureAwait(false);
                                }

                                if (File.Exists(targetPath))
                                {
                                    var imageInfo = new MediaBrowser.Controller.Entities.ItemImageInfo
                                    {
                                        Path = targetPath,
                                        Type = MediaBrowser.Model.Entities.ImageType.Backdrop,
                                        DateModified = File.GetLastWriteTimeUtc(targetPath)
                                    };
                                    collection.SetImage(imageInfo, i);
                                    metadataUpdated = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.ErrorException($"Failed to download backdrop image for collection '{resolvedName}'. URL: {bgUrl}", ex);
                            }
                        }

                        if (metadataUpdated)
                        {
                            collection.UpdateToRepository(MediaBrowser.Controller.Library.ItemUpdateType.ImageUpdate | MediaBrowser.Controller.Library.ItemUpdateType.MetadataEdit);
                        }

                        _logger.Info($"Successfully synced collection: {resolvedName}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.ErrorException($"Failed to create or sync collection '{resolvedName}'.", ex);
                }

                createdCount++;
                progress.Report(50 + ((double)createdCount / collectionsToCreate.Count * 50)); // Second half
            }

            _logger.Info("BespokeDB Collection Sync completed.");
        }
    }
}
