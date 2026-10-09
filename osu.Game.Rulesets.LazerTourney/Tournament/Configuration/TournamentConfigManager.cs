// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Configuration;
using osu.Framework.Platform;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Configuration
{
    public class TournamentConfigManager : IniConfigManager<StorageConfig>
    {
        protected override string Filename => "tournament-lazer.ini";

        private const string default_tournament = "default";

        public TournamentConfigManager(Storage storage)
            : base(storage)
        {
        }

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            SetDefault(StorageConfig.CurrentTournament, default_tournament);
            SetDefault(StorageConfig.RefereeClientId, string.Empty);
            SetDefault(StorageConfig.RefereeClientSecret, string.Empty);
            SetDefault(StorageConfig.RefereeAccessToken, string.Empty);
            SetDefault(StorageConfig.RefereeRefreshToken, string.Empty);
            SetDefault(StorageConfig.RefereeExpiresAt, 0L);
        }
    }

    public enum StorageConfig
    {
        CurrentTournament,

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
