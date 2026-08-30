using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Controller.Entities;

namespace BespokeDB.Plugin.Providers
{
    /// <summary>
    /// Represents the external ID for A24 films.
    /// </summary>
    public class A24MovieExternalId : IExternalId
    {
        /// <inheritdoc />
        public string Name => "BespokeDB - A24 Films";

        /// <inheritdoc />
        public string Key => "BespokeDB_a24_films";

        /// <inheritdoc />
        public string UrlFormatString => "https://a24films.com/films/{0}";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item)
        {
            return item is Movie;
        }
    }

    /// <summary>
    /// Represents the external ID for A24 box sets.
    /// </summary>
    public class A24BoxSetExternalId : IExternalId
    {
        /// <inheritdoc />
        public string Name => "BespokeDB - A24 Films";

        /// <inheritdoc />
        public string Key => "BespokeDB_a24_films_boxset";

        /// <inheritdoc />
        public string UrlFormatString => "https://a24films.com/films/{0}";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item)
        {
            return item is BoxSet;
        }
    }
}
