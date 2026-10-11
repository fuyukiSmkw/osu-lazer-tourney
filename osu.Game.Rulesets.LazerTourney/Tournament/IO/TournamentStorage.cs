// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.IO;
using osu.Game.Rulesets.LazerTourney.Tournament.Configuration;

namespace osu.Game.Rulesets.LazerTourney.Tournament.IO
{
    public class TournamentStorage : WrappedStorage
    {
        /// <summary>
        /// The storage where all tournaments are located.
        /// </summary>
        public readonly Storage AllTournaments;

        public readonly Bindable<string> CurrentTournament;

        protected TournamentConfigManager TournamentConfigManager { get; }

        public TournamentStorage(Storage storage)
            : base(storage.GetStorageForDirectory("tournaments-lazer"), string.Empty)
        {
            AllTournaments = UnderlyingStorage;

            TournamentConfigManager = new TournamentConfigManager(storage);

            CurrentTournament = TournamentConfigManager.GetBindable<string>(StorageConfig.CurrentTournament);

            ChangeTargetStorage(AllTournaments.GetStorageForDirectory(CurrentTournament.Value));

            Logger.Log("Using tournament storage: " + GetFullPath(string.Empty));

            CurrentTournament.BindValueChanged(updateTournament);
        }

        private void updateTournament(ValueChangedEvent<string> newTournament)
        {
            ChangeTargetStorage(AllTournaments.GetStorageForDirectory(newTournament.NewValue));
            Logger.Log("Changing tournament storage: " + GetFullPath(string.Empty));
        }

        public IEnumerable<string> ListTournaments() => AllTournaments.GetDirectories(string.Empty).Order(StringComparer.CurrentCultureIgnoreCase);

        /// <summary>
        /// OAuth client ID for the referee hub API.
        /// </summary>
        public Bindable<string> RefereeClientId => TournamentConfigManager.GetBindable<string>(StorageConfig.RefereeClientId);

        /// <summary>
        /// OAuth client secret for the referee hub API.
        /// </summary>
        public Bindable<string> RefereeClientSecret => TournamentConfigManager.GetBindable<string>(StorageConfig.RefereeClientSecret);

        /// <summary>
        /// Referee hub API access token.
        /// </summary>
        public string RefereeAccessToken
        {
            get => TournamentConfigManager.Get<string>(StorageConfig.RefereeAccessToken);
            set => TournamentConfigManager.SetValue(StorageConfig.RefereeAccessToken, value);
        }

        /// <summary>
        /// Referee hub API refresh token.
        /// </summary>
        public string RefereeRefreshToken
        {
            get => TournamentConfigManager.Get<string>(StorageConfig.RefereeRefreshToken);
            set => TournamentConfigManager.SetValue(StorageConfig.RefereeRefreshToken, value);
        }

        /// <summary>
        /// Referee hub API access token expiry as unix seconds.
        /// </summary>
        public long RefereeExpiresAt
        {
            get => TournamentConfigManager.Get<long>(StorageConfig.RefereeExpiresAt);
            set => TournamentConfigManager.SetValue(StorageConfig.RefereeExpiresAt, value);
        }
    }
}
