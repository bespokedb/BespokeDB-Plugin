using System;
using System.IO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using BespokeDB.Plugin.Configuration;
using BespokeDB.Plugin.Security;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Drawing;

namespace BespokeDB.Plugin
{
    /// <summary>
    /// The main entry point for the BespokeDB Emby plugin.
    /// </summary>
    public class Plugin : MediaBrowser.Controller.Plugins.BasePluginSimpleUI<PluginConfiguration>, IHasThumbImage
    {
        /// <summary>
        /// Gets the global instance of the plugin.
        /// </summary>
        public static Plugin? Instance { get; private set; }

        /// <summary>
        /// Gets the JWT token cache used across providers.
        /// </summary>
        public JwtCache TokenCache { get; private set; }

        /// <summary>
        /// Gets the plugin configuration.
        /// </summary>
        public PluginConfiguration Configuration => this.GetOptions();

        /// <summary>
        /// Gets the movie cache manager used to prevent unnecessary API queries.
        /// </summary>
        public Cache.MovieCacheManager CacheManager { get; private set; }

        /// <summary>
        /// Gets the application host for DI resolution.
        /// </summary>
        public MediaBrowser.Common.IApplicationHost AppHost { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="applicationHost">The application host.</param>
        /// <param name="logManager">The log manager.</param>
        public Plugin(MediaBrowser.Common.IApplicationHost applicationHost, ILogManager logManager)
            : base(applicationHost)
        {
            Instance = this;
            AppHost = applicationHost;
            TokenCache = new JwtCache(logManager.GetLogger(Name));
            CacheManager = new Cache.MovieCacheManager(logManager.GetLogger(Name));
        }

        /// <inheritdoc />
        public override string Name => "BespokeDB Metadata";

        /// <inheritdoc />
        public override Guid Id => new Guid("5A3D3C9D-9988-4267-932F-EBD0DF8CCF7B");

        /// <inheritdoc />
        public ImageFormat ThumbImageFormat => ImageFormat.Png;

        /// <inheritdoc />
        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.png")!;
        }

        /// <summary>
        /// Saves the plugin configuration.
        /// </summary>
        public void SaveConfiguration()
        {
            base.SaveOptions(Configuration);
        }
    }
}
