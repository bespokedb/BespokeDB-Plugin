using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Controller.Entities;

namespace BespokeDB.Plugin.Providers
{
    /// <summary>
    /// Represents the external ID for Criterion Collection films.
    /// </summary>
    public class CriterionMovieExternalId : IExternalId
    {
        /// <inheritdoc />
        public string Name => "BespokeDB - Criterion Collection";

        /// <inheritdoc />
        public string Key => "BespokeDB_criterion_collection";

        /// <inheritdoc />
        public string UrlFormatString => "https://www.criterion.com/films/{0}";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item)
        {
            return item is Movie;
        }
    }

    /// <summary>
    /// Represents the external ID for Criterion Collection box sets.
    /// </summary>
    public class CriterionBoxSetExternalId : IExternalId
    {
        /// <inheritdoc />
        public string Name => "BespokeDB - Criterion Collection";

        /// <inheritdoc />
        public string Key => "BespokeDB_criterion_collection_boxset";

        /// <inheritdoc />
        public string UrlFormatString => "https://www.criterion.com/boxsets/{0}";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item)
        {
            return item is BoxSet;
        }
    }
}
