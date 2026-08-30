using MediaBrowser.Model.Logging;
using MediaBrowser.Common.Net;

namespace BespokeDB.Plugin.Providers
{
    /// <summary>
    /// Provides movie metadata for A24 Films.
    /// </summary>
    public class A24MovieProvider : BespokeMovieProviderBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="A24MovieProvider"/> class.
        /// </summary>
        /// <param name="logManager">The log manager.</param>
        /// <param name="httpClient">The HTTP client.</param>
        public A24MovieProvider(ILogManager logManager, IHttpClient httpClient) : base(logManager, httpClient) { }

        /// <inheritdoc />
        public override string Name => "BespokeDB - A24 Films";

        /// <inheritdoc />
        protected override string DatabaseId => "a24_films";

        /// <inheritdoc />
        protected override string StudioName => "A24 Films";
    }

    /// <summary>
    /// Provides box set metadata for A24 Films.
    /// </summary>
    public class A24BoxSetProvider : BespokeBoxSetProviderBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="A24BoxSetProvider"/> class.
        /// </summary>
        /// <param name="logManager">The log manager.</param>
        /// <param name="httpClient">The HTTP client.</param>
        public A24BoxSetProvider(ILogManager logManager, IHttpClient httpClient) : base(logManager, httpClient) { }

        /// <inheritdoc />
        public override string Name => "BespokeDB - A24 Films";

        /// <inheritdoc />
        protected override string DatabaseId => "a24_films";
    }

    /// <summary>
    /// Provides images for A24 Films.
    /// </summary>
    public class A24ImageProvider : BespokeImageProviderBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="A24ImageProvider"/> class.
        /// </summary>
        /// <param name="logManager">The log manager.</param>
        /// <param name="httpClient">The HTTP client.</param>
        public A24ImageProvider(ILogManager logManager, IHttpClient httpClient) : base(logManager, httpClient) { }

        /// <inheritdoc />
        public override string Name => "BespokeDB - A24 Films";

        /// <inheritdoc />
        protected override string DatabaseId => "a24_films";
    }
}
