// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Configuration;
using osu.Framework.Platform;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Configuration
{
    public class TournamentConfigManager : IniConfigManager<StorageConfig>
    {
        protected override string Filename => "tournament-lazer.ini";

        private const string default_tournament = "default";

        private static readonly object shared_lock = new object();
        private static readonly Dictionary<string, TournamentConfigManager> shared_managers = new Dictionary<string, TournamentConfigManager>();

        public TournamentConfigManager(Storage storage)
            : base(storage)
        {
        }

        /// <summary>
        /// The process-wide config manager for a given storage root.
        /// </summary>
        /// <remarks>
        /// Never create short-lived instances of this class. The ini file is loaded by parsing values into the
        /// bindables, and every value that differs from its default queues a debounced background save (disposing
        /// the manager also saves). A throwaway reader therefore writes its loaded snapshot back to the file about
        /// 100ms later, silently overwriting newer values written by another instance. Sharing one instance per
        /// storage keeps a single source of truth. The shared instance must never be disposed.
        /// </remarks>
        public static TournamentConfigManager GetShared(Storage storage)
        {
            string key = storage.GetFullPath(string.Empty);

            lock (shared_lock)
            {
                if (!shared_managers.TryGetValue(key, out TournamentConfigManager manager))
                    shared_managers.Add(key, manager = new TournamentConfigManager(storage));

                return manager;
            }
        }

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            SetDefault(StorageConfig.CurrentTournament, default_tournament);
            SetDefault(StorageConfig.RefereeCallbackPort, 60727);
            SetDefault(StorageConfig.WebSocketPort, 50727);
            SetDefault(StorageConfig.RefereeClientId, string.Empty);
            SetDefault(StorageConfig.RefereeClientSecret, string.Empty);
            SetDefault(StorageConfig.RefereeAccessToken, string.Empty);
            SetDefault(StorageConfig.RefereeRefreshToken, string.Empty);
            SetDefault(StorageConfig.RefereeExpiresAt, 0L);
        }

        // Save() (rather than PerformSave()) so an explicit save also cancels any queued debounced save.
        public bool SaveIni() => Save();
    }

    public enum StorageConfig
    {
        CurrentTournament,

        /// <summary>
        /// Local port used for the referee OAuth callback listener.
        /// </summary>
        RefereeCallbackPort,

        /// <summary>
        /// Local port used by the tosu-compatible WebSocket data stream.
        /// </summary>
        WebSocketPort,

        /// <summary>
        /// OAuth client ID for the referee hub API. One user, one client.
        /// </summary>
        RefereeClientId,

        /// <summary>
        /// OAuth client secret for the referee hub API. One user, one client.
        /// </summary>
        RefereeClientSecret,

        /// <summary>
        /// Referee hub API access token. Represents the user, never shared.
        /// </summary>
        RefereeAccessToken,

        /// <summary>
        /// Referee hub API refresh token. Represents the user, never shared.
        /// </summary>
        RefereeRefreshToken,

        /// <summary>
        /// Referee hub API access token expiry as unix seconds.
        /// </summary>
        RefereeExpiresAt,
    }
}
