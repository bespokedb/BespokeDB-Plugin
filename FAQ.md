# BespokeDB Plugin FAQ

Welcome to the Frequently Asked Questions for the BespokeDB Emby Plugin! Here we cover some of the specific behaviors and technical quirks of how the plugin interacts with Emby.

## Best Practices

- **Provider Order**: Make sure the order of BespokeDB lookups is above all other lookups in your Emby library settings. 
  - *Caveat*: Sometimes Emby requires you to reorder the providers even if BespokeDB is already at the top. It might "refresh" to the bottom, requiring you to move it back to the top.
- **Rich Data**: BespokeDB will not fetch actor images, logos, or disc art at this time. Make sure to add another provider (like TMDB) below it to keep your rich data intact.
- **Boxsets & Collections**: Boxsets from collections (where available) will be created automatically in your Emby Collections, even if you did not originally buy the movie as part of a physical boxset.
- **Isolated Libraries**: BespokeDB artwork works best within an isolated Library (e.g. a dedicated "Criterion" library). Because our curated poster sizes will sometimes be different than the standard 2:3 ratio, they may look odd in the interface if placed directly next to other standard Hollywood movies. *(Note: Support for Arrow Video is coming soon!)*

### Why aren't my Box Sets/Collections showing up automatically after a library scan?
Emby has native limitations with how it handles 3rd-party collection grouping during standard scans. To solve this, we built a dedicated Scheduled Task. After your library finishes scanning, simply navigate to **Scheduled Tasks -> Library -> Sync BespokeDB Collections** and run it. This task will securely group your movies together into stunning collections while completely respecting your native Emby minimum collection thresholds!

### Why did the plugin skip my standard Hollywood movies?
This is entirely by design! The plugin downloads a highly compressed "smart cache" of our entire database every night. When you scan your library, the plugin checks this local cache first. If a movie doesn't belong to a supported distributor (like the Criterion Collection or A24), the plugin immediately ignores it. This saves your server from making wasteful API calls and speeds up your scans tremendously.

### How does the 16:9 Collection Banner toggle work?
Emby natively expects all collection folders to use a vertical 2:3 poster format. When you enable the 16:9 Banner toggle in the BespokeDB plugin settings, we use a clever workaround: the plugin promotes stunning wide backdrop images to become the "Primary Image", and shifts the traditional poster to the "Box Cover" slot. This tricks Emby into rendering beautiful wide banners for your collections!

### Do I need a separate API key for each library?
No! Your API key (Client ID and Client Secret) is entered once in the global **Plugins -> BespokeDB** dashboard. It will automatically apply to any library where you have enabled the BespokeDB metadata providers.

### I added a supported movie, but it didn't match automatically. What do I do?
Occasionally, Emby might misidentify a movie's initial title (especially if the file name is messy). You can easily fix this by clicking the three dots (`...`) on the movie in Emby, selecting **Identify**, and typing in the exact title. The plugin will intercept the search and pull down the correct BespokeDB metadata.

### Why is my API Key failing to authenticate?
Double-check that you copied the **Client ID** and **Client Secret** exactly as they appear in your [Bespokedb.cloud](https://bespokedb.cloud) dashboard, with no trailing spaces. Also, ensure your Emby server has an active outbound internet connection so it can reach the BespokeDB Cloud.

### I found a bug or have a feature request! Where do I submit it?
We'd love to hear your feedback! You can submit all bug reports, feature requests, and support tickets directly through our web portal at [https://bespokedb.cloud](https://bespokedb.cloud). Just log in to your dashboard to get in touch with us!
