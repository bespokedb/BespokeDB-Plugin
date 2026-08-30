using MediaBrowser.Model.Plugins;
using System.Collections.Generic;
using System.ComponentModel;

namespace BespokeDB.Plugin.Configuration
{
    /// <summary>
    /// Image formats for collections.
    /// </summary>
    public enum CollectionImageFormat
    {
        /// <summary>
        /// Poster image format (2:3).
        /// </summary>
        Poster,

        /// <summary>
        /// Banner image format (16:9).
        /// </summary>
        Banner
    }

    /// <summary>
    /// Represents the global configuration for the BespokeDB plugin.
    /// Emby automatically serializes and deserializes this class.
    /// </summary>
    public class PluginConfiguration : Emby.Web.GenericEdit.EditableOptionsBase
    {
        /// <summary>
        /// Gets the editor title for the plugin configuration page.
        /// </summary>
        public override string EditorTitle => "BespokeDB Setup";

        /// <summary>
        /// Gets the editor description for the plugin configuration page.
        /// </summary>
        public override string EditorDescription => "Configure API credentials and enable target databases. Register for a free API Key at https://bespokedb.cloud.";

        /// <summary>
        /// Gets or sets the Client ID used to authenticate with the BespokeDB API.
        /// </summary>
        [DisplayName("BespokeDB Client ID")]
        [Description("Enter the Client ID generated from your https://bespokedb.cloud dashboard.")]
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Client Secret used to authenticate with the BespokeDB API.
        /// </summary>
        [DisplayName("BespokeDB Client Secret")]
        [Description("Enter the Client Secret generated from your https://bespokedb.cloud dashboard.")]
        public string ClientSecret { get; set; } = string.Empty;



        /// <summary>
        /// Gets or sets the timestamp of the last successful nightly sync.
        /// </summary>
        [DisplayName("Last Successful Nightly Sync")]
        [Description("The timestamp of the last successful nightly sync. Run the 'Sync BespokeDB Movie Cache' Scheduled Task to update this.")]
        public string LastSuccessfulNightlySync { get; set; } = "Never";

        /// <summary>
        /// Gets or sets the preferred image type for collections (Poster or Banner).
        /// </summary>
        [DisplayName("Collection Image Format")]
        [Description("Select the aspect ratio for the collection images.")]
        public CollectionImageFormat CollectionImageType { get; set; } = CollectionImageFormat.Poster;

        /// <summary>
        /// Gets or sets a value indicating whether collections are enabled.
        /// </summary>
        [DisplayName("Enable Collections")]
        [Description("Automatically add movies to collections based on their box set metadata.")]
        public bool EnableCollections { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to group movies by studio collection.
        /// </summary>
        [DisplayName("Group by Studio Collection")]
        [Description("Automatically add all movies from a specific studio (e.g., Criterion Collection, A24 Films) into a single overarching collection.")]
        public bool GroupByStudioCollection { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to enable the Criterion Collection database.
        /// </summary>
        [DisplayName("Enable Criterion Collection")]
        [Description("Enable querying and caching for the Criterion Collection database.")]
        public bool EnableCriterionCollection { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to enable the A24 Films database.
        /// </summary>
        [DisplayName("Enable A24 Films")]
        [Description("Enable querying and caching for the A24 Films database.")]
        public bool EnableA24Films { get; set; } = true;
    }
}
