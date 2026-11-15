using System;
using System.Collections.Generic;
using System.Text;

namespace QBittorrent.Client
{
    /// <summary>
    /// Provides constants with special values for <see cref="IQBittorrentClient2.SetShareLimitsAsync"/> method.
    /// </summary>
    public static class ShareLimits
    {
        /// <summary>
        /// Provides constants for <c>ratio</c> parameter of <see cref="IQBittorrentClient2.SetShareLimitsAsync"/> method.
        /// </summary>
        public static class Ratio
        {
            /// <summary>
            /// Set global limit.
            /// </summary>
            public const double Global = -2d;

            /// <summary>
            /// Set no limit.
            /// </summary>
            public const double Unlimited = -1;
        }

        /// <summary>
        /// Provides constants for <c>seedingTime</c> parameter of <see cref="IQBittorrentClient2.SetShareLimitsAsync"/> method.
        /// </summary>
        public static class SeedingTime
        {
            /// <summary>
            /// Set global limit.
            /// </summary>
            public static readonly TimeSpan Global = TimeSpan.FromMinutes(-2);

            /// <summary>
            /// Set no limit.
            /// </summary>
            public static readonly TimeSpan Unlimited = TimeSpan.FromMinutes(-1);
        }
    }
}
