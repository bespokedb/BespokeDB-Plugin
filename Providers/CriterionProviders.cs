using MediaBrowser.Model.Logging;
using MediaBrowser.Common.Net;

namespace BespokeDB.Plugin.Providers
{
    /// <summary>
    /// Provides movie metadata for the Criterion Collection.
    /// </summary>
    public class CriterionMovieProvider : BespokeMovieProviderBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CriterionMovieProvider"/> class.
        /// </summary>
        /// <param name="logManager">The log manager.</param>
        /// <param name="httpClient">The HTTP client.</param>
        public CriterionMovieProvider(ILogManager logManager, IHttpClient httpClient) : base(logManager, httpClient) { }

        /// <inheritdoc />
        public override string Name => "BespokeDB - Criterion Collection";

        /// <inheritdoc />
        protected override string DatabaseId => "criterion_collection";

        /// <inheritdoc />
        protected override string StudioName => "Criterion Collection";
    }

    /// <summary>
    /// Provides box set metadata for the Criterion Collection.
    /// </summary>
    public class CriterionBoxSetProvider : BespokeBoxSetProviderBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CriterionBoxSetProvider"/> class.
        /// </summary>
        /// <param name="logManager">The log manager.</param>
        /// <param name="httpClient">The HTTP client.</param>
        public CriterionBoxSetProvider(ILogManager logManager, IHttpClient httpClient) : base(logManager, httpClient) { }

        /// <inheritdoc />
        public override string Name => "BespokeDB - Criterion Collection";

        /// <inheritdoc />
        protected override string DatabaseId => "criterion_collection";
    }

    /// <summary>
    /// Provides images for the Criterion Collection.
    /// </summary>
    public class CriterionImageProvider : BespokeImageProviderBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CriterionImageProvider"/> class.
        /// </summary>
        /// <param name="logManager">The log manager.</param>
        /// <param name="httpClient">The HTTP client.</param>
        public CriterionImageProvider(ILogManager logManager, IHttpClient httpClient) : base(logManager, httpClient) { }

        /// <inheritdoc />
        public override string Name => "BespokeDB - Criterion Collection";

        /// <inheritdoc />
        protected override string DatabaseId => "criterion_collection";
    }
}
