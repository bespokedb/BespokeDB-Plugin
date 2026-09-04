# BespokeDB for Emby

![.NET Version](https://img.shields.io/badge/.NET-8.0-blue)
![Emby Server](https://img.shields.io/badge/Emby%20Server-4.9%2B-green)
![License](https://img.shields.io/badge/License-MIT-blue)

BespokeDB is a premium metadata plugin for Emby Server. It seamlessly enriches your movie libraries with highly curated metadata, gorgeous artwork, and automated collections for distributors like the **Criterion Collection** and **A24**.

## Why BespokeDB?

Standard metadata scrapers often pull generic, user-generated data that can clutter your pristine movie collection. BespokeDB was created to provide a tailored, premium experience for cinephiles by offering:
- **Authentic Artwork**: Original posters directly from Criterion and A24 in their native dimensions.
- **Curated Descriptions**: Overrides generic overviews with the studio's intended descriptions.
- **Streamlined Cast Lists**: Focuses exclusively on actors highlighted by the studio to reduce UI clutter.
- **Official Backdrops**: High-quality imagery sourced directly from the collections' websites.
- **External Links**: Direct outbound links to the official collection store pages.
- **Future-Proof Metadata**: Lays the foundation for upcoming releases, tracking fields like spine numbers and special features.

## Prerequisites

Before installing the plugin, you must register for a free API Key at [https://bespokedb.cloud](https://bespokedb.cloud). You will need your **Client ID** and **Client Secret** to authenticate the plugin within Emby.

## Features

- **Rich Movie Metadata**: Automatically downloads accurate release years, overviews, and official external links directly from the BespokeDB Cloud.
- **Automated Box Sets**: Instantly organizes your movies into their official collections (e.g., *The Criterion Collection* or *A24 Films*) without any manual grouping required. 
- **Stunning Artwork**: 
  - Downloads official, high-resolution primary posters.
  - Pulls in a curated array of beautiful backdrop images for your movies and collections.
- **Collection Banners**: Collections default to a classic 2:3 poster layout, with an optional toggle to automatically format them as stunning 16:9 banners directly within the Emby UI, elevating backdrops to the main view and shifting posters to the cover.
- **Granular Library Control**: Selectively enable or disable specific providers (like *BespokeDB - Criterion Collection* or *BespokeDB - A24 Films*) on a per-library basis.
- **Privacy Focused**: Built-in smart caching (which updates automatically nightly) ensures the plugin only requests metadata for movies that actually exist in our collections. The rest of your library data remains completely private and stays on your server.

## Installation & Setup

1. **Install the Plugin**: Download the latest release and copy `BespokeDB.Plugin.dll` to your Emby Server's `/plugins` folder.
2. **Restart Emby**: Restart your Emby Server to load the plugin.
3. **Authenticate**: Navigate to **Plugins -> BespokeDB** in your Emby Dashboard and enter your provided Client ID and Client Secret. You can also configure your preferred image formats here.
4. **Enable Providers**: Go to **Settings -> Library**, edit your desired movie library, and enable your chosen BespokeDB providers under "Movie metadata downloaders".
5. **Scan & Sync**: 
   - Run a standard library scan to refresh your movie metadata.
   - Go to **Scheduled Tasks -> Library** and run **Sync BespokeDB Collections** to instantly build your official box sets!

## Privacy & Terms

By installing and using the BespokeDB plugin, you agree to our [Terms of Service](https://bespokedb.cloud/terms.html) and [Privacy Policy](https://bespokedb.cloud/privacy.html). We are strictly committed to data minimization. We only collect telemetry on the specific movies queried that exist in our database; we do **not** collect telemetry or data on any other movies in your personal media library. Any telemetry collected is securely tied to your API key (acting as a pseudonymous token) and strictly conforms to our data retention schedule.

## FAQ

Have questions about how box sets are generated, how caching works, or how to troubleshoot matching? Check out our [Frequently Asked Questions (FAQ)](FAQ.md) guide!

## License
Distributed under the MIT License. See `LICENSE` for more information.