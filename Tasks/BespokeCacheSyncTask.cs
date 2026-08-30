using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;

namespace BespokeDB.Plugin.Tasks
{
    /// <summary>
    /// Scheduled task that downloads the nightly BespokeDB title cache.
    /// </summary>
    public class BespokeCacheSyncTask : IScheduledTask
    {
        /// <inheritdoc />
        public string Name => "Sync BespokeDB Movie Cache";

        /// <inheritdoc />
        public string Key => "BespokeCacheSyncTask";

        /// <inheritdoc />
        public string Description => "Downloads the nightly BespokeDB title cache to prevent unnecessary API queries.";

        /// <inheritdoc />
        public string Category => "BespokeDB";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerDaily,
                    TimeOfDayTicks = TimeSpan.FromHours(4).Ticks,
                    MaxRuntimeTicks = TimeSpan.FromMinutes(10).Ticks
                }
            };
        }

        /// <inheritdoc />
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            if (Plugin.Instance?.CacheManager != null)
            {
                progress.Report(10);
                await Plugin.Instance.CacheManager.SyncCacheNightlyAsync(cancellationToken).ConfigureAwait(false);

                // Update configuration on success
                Plugin.Instance.Configuration.LastSuccessfulNightlySync = $"Last successful nightly sync {DateTime.UtcNow:M/d/yyyy H:mm} UTC";
                Plugin.Instance.SaveConfiguration();

                progress.Report(100);
            }
        }
    }
}
