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
        /// The directory (relative to the game's storage) holding all tournaments.
        /// </summary>
        public const string TOURNAMENTS_DIRECTORY = @"tournaments-lazer";

        /// <summary>
        /// The storage where all tournaments are located.
        /// </summary>
        public readonly Storage AllTournaments;

        public readonly Bindable<string> CurrentTournament;

        protected TournamentConfigManager TournamentConfigManager { get; }

        public void SaveIni() => TournamentConfigManager.SaveIni();

        public TournamentStorage(Storage storage)
            : base(GetAllTournamentsStorage(storage), string.Empty)
        {
            AllTournaments = UnderlyingStorage;

            TournamentConfigManager = TournamentConfigManager.GetShared(storage);

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
        /// Returns the storage that contains all tournament directories.
        /// </summary>
        public static Storage GetAllTournamentsStorage(Storage storage) => storage.GetStorageForDirectory(TOURNAMENTS_DIRECTORY);

        /// <summary>
        /// Lists all tournament directory names without creating a <see cref="TournamentStorage"/> instance.
        /// </summary>
        public static IEnumerable<string> ListTournaments(Storage storage) =>
            GetAllTournamentsStorage(storage).GetDirectories(string.Empty).Order(StringComparer.CurrentCultureIgnoreCase);

        /// <summary>
        /// Reads the persisted current tournament.
        /// </summary>
        /// <remarks>
        /// Uses the process-wide config manager. Never create a throwaway manager just to read a value: loading an
        /// ini file mutates the bindables, which queues a debounced background save that would write the loaded
        /// snapshot back to disk a moment later (see <see cref="TournamentConfigManager.GetShared"/>).
        /// </remarks>
        public static string GetCurrentTournament(Storage storage)
            => TournamentConfigManager.GetShared(storage).Get<string>(StorageConfig.CurrentTournament);

        /// <summary>
        /// Persists the current tournament so the next <see cref="TournamentStorage"/> instance uses it.
        /// </summary>
        public static void SetCurrentTournament(Storage storage, string tournament)
        {
            var config = TournamentConfigManager.GetShared(storage);

            config.SetValue(StorageConfig.CurrentTournament, tournament);
            config.SaveIni();
        }

        /// <summary>
        /// Local port used for the referee OAuth callback listener.
        /// </summary>
        public Bindable<int> RefereeCallbackPort => TournamentConfigManager.GetBindable<int>(StorageConfig.RefereeCallbackPort);

        /// <summary>
        /// Local port used by the tosu-compatible WebSocket data stream.
        /// </summary>
        public Bindable<int> WebSocketPort => TournamentConfigManager.GetBindable<int>(StorageConfig.WebSocketPort);

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
