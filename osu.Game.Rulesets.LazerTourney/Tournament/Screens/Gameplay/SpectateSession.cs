// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Online;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Metadata;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;
using osu.Game.Online.Rooms;
using osu.Game.Online.Spectator;
using osu.Game.Replays;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Online;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay.Components;
using osu.Game.Scoring;
using osu.Game.Screens.OnlinePlay.Multiplayer;
using osu.Game.Screens.OnlinePlay.Multiplayer.Spectate;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Spectate;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay
{
    /// <summary>
    /// Tournament spectate session. Replicates the watching/scoring/sync logic of
    /// <see cref="SpectatorScreen"/> + <see cref="MultiSpectatorScreen"/> without a screen stack,
    /// driving <see cref="SpectateCell"/>s owned by <see cref="GameplayScreen"/>.
    /// Layout (custom grid + idle background/logo) is preserved; data flow follows official.
    /// </summary>
    /// <remarks>
    /// Hosted under <see cref="TournamentSceneManager"/> (always present) rather than inside
    /// <see cref="GameplayScreen"/>, so spectating follows live progress no matter which screen
    /// is shown. The session itself draws nothing; all visuals stay in the gameplay cells.
    /// </remarks>
    public partial class SpectateSession : CompositeDrawable
    {
        [Resolved]
        private SpectatorClient spectatorClient { get; set; } = null!;

        [Resolved]
        private MultiplayerClient multiplayerClient { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private MetadataClient metadataClient { get; set; } = null!;

        [Resolved]
        private UserLookupCache userLookupCache { get; set; } = null!;

        [Resolved]
        private TournamentMusicController musicController { get; set; } = null!;

        [Resolved]
        private Bindable<WorkingBeatmap> beatmap { get; set; } = null!;

        private readonly HashSet<int> watchedUsers = new HashSet<int>();
        private readonly Dictionary<int, APIUser> userMap = new Dictionary<int, APIUser>();
        private readonly Dictionary<int, SpectateCell> cellsByUser = new Dictionary<int, SpectateCell>();
        private readonly Dictionary<int, SpectatorPlayerClock> clocksByUser = new Dictionary<int, SpectatorPlayerClock>();
        private readonly Dictionary<int, Score> scoresByUser = new Dictionary<int, Score>();
        private readonly HashSet<int> seenBreak = new HashSet<int>();
        private readonly HashSet<int> comboSubscribed = new HashSet<int>();
        private readonly List<(BindableInt bindable, Action<ValueChangedEvent<int>> handler)> comboSubscriptions = new List<(BindableInt, Action<ValueChangedEvent<int>>)>();

        private IReadOnlyList<SpectateCell>? redCells;
        private IReadOnlyList<SpectateCell>? blueCells;

        private long? clockItemId;
        private bool clockFromItem;
        private MasterGameplayClockContainer? masterClock;
        private SpectatorSyncManager? syncManager;
        private WorkingBeatmap? masterWorkingBeatmap;

        private HashSet<int> providerUserIds = new HashSet<int>();
        private TournamentLeaderboardProvider? leaderboardProvider;
        private bool leaderboardLoaded;

        private SkinnableSound breakSound = null!;
        private Bindable<bool> alwaysPlayFirstBreak = null!;
        private IDisposable? realmSubscription;
        private IDisposable? userWatchToken;

        private MultiplayerBeatmapAvailabilityTracker beatmapAvailabilityTracker = null!;

        private int? currentAudioUserId;
        private IAggregateAudioAdjustment? boundAdjustments;

        private bool hadRoom;

        /// <summary>
        /// Whether assignment is frozen (match in progress). Layout/count changes are ignored while frozen.
        /// </summary>
        public bool IsFrozen => multiplayerClient.Room is { State: MultiplayerRoomState.WaitingForLoad or MultiplayerRoomState.Playing };

        /// <summary>
        /// Whether the current assignment produced team scores (team versus rooms only).
        /// </summary>
        public readonly BindableBool HasTeamScores = new BindableBool();

        /// <summary>
        /// A currently spectated score, if any. Kept through result screens until reset or a new match.
        /// </summary>
        public readonly Bindable<Score?> SpectatedScore = new Bindable<Score?>();

        /// <summary>
        /// Snapshot of the playlist item being spectated, taken when spectating starts.
        /// </summary>
        public MultiplayerPlaylistItem? SpectatedItem { get; private set; }

        private void updateSpectatedScore()
            => SpectatedScore.Value = scoresByUser.Values.FirstOrDefault();

        public TournamentMatchScoreDisplay? ScoreDisplay
        {
            get;
            set
            {
                field = value;
                updateScoreBindings();
            }
        }

        public SpectateSession()
        {
            // Logic container only; never blocks input or draws anything itself.
            RelativeSizeAxes = Axes.None;
            AutoSizeAxes = Axes.None;
            Size = new osuTK.Vector2(0);
            AlwaysPresent = true;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(breakSound = new SkinnableSound(new SampleInfo("Gameplay/combobreak")));
            AddInternal(beatmapAvailabilityTracker = new MultiplayerBeatmapAvailabilityTracker());
            alwaysPlayFirstBreak = config.GetBindable<bool>(OsuSetting.AlwaysPlayFirstComboBreak);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Mirrors SpectatorScreen.LoadComplete: presence watching + user lookup gate.
            userWatchToken = metadataClient.BeginWatchingUserPresence();

            spectatorClient.WatchedUserStates.BindCollectionChanged(onUserStatesChanged);

            foreach (var kvp in spectatorClient.WatchedUserStates)
                onUserStateChanged(kvp.Key, kvp.Value);

            multiplayerClient.RoomUpdated += onRoomUpdated;
            multiplayerClient.LoadRequested += onLoadRequested;

            beatmapAvailabilityTracker.Availability.BindValueChanged(onBeatmapAvailabilityChanged, true);

            // Filtered to inserted sets, matching SpectatorScreen.beatmapsChanged.
            realmSubscription = realm.RegisterForNotifications<BeatmapSetInfo>(
                r => r.All<BeatmapSetInfo>().Where(s => !s.DeletePending),
                (sender, changes) =>
                {
                    if (changes?.InsertedIndices == null)
                        return;

                    Schedule(retryPendingMaps);
                });

            // Initial state in case we are already in a room.
            hadRoom = multiplayerClient.Room != null;
            Assign();
        }

        #region Cells and assignment

        /// <summary>
        /// Provides the layout cells. Must be called after every layout rebuild.
        /// </summary>
        public void SetCells(IReadOnlyList<SpectateCell> red, IReadOnlyList<SpectateCell> blue)
        {
            redCells = red;
            blueCells = blue;
        }

        /// <summary>
        /// Recomputes slot assignment, labels, watches and score provider.
        /// No-op while frozen unless <paramref name="force"/> is set (new match, mid-match join, reset).
        /// </summary>
        public void Assign(bool force = false)
        {
            if (!IsLoaded || redCells == null || blueCells == null)
                return;

            var room = multiplayerClient.Room;

            if (room == null)
            {
                fullTeardown();
                return;
            }

            if (IsFrozen && !force)
                return;

            long? itemId = currentItemId();

            if (!force && clockItemId != null && itemId != clockItemId && cellsByUser.Values.Any(c => c.HasPlay))
            {
                // The room advanced to the next item while spectate is still showing the previous one
                // (slow spectate). Keep showing the current item, including result screens, until
                // reset or the next LoadRequested starts a new match.
                Logger.Log("Keeping spectated item while room advanced to next item.", LoggingTarget.Runtime, LogLevel.Verbose);
                return;
            }

            if (masterClock == null || syncManager == null || clockItemId != itemId)
            {
                if (setupClocks())
                    clockItemId = itemId;
            }

            var redIds = assignSide(room, redCells, 0);
            var blueIds = assignSide(room, blueCells, 1);

            cellsByUser.Clear();

            for (int i = 0; i < redCells.Count && i < redIds.Count; i++)
            {
                if (redIds[i] != null)
                    cellsByUser[redIds[i]!.Value] = redCells[i];
            }

            for (int i = 0; i < blueCells.Count && i < blueIds.Count; i++)
            {
                if (blueIds[i] != null)
                    cellsByUser[blueIds[i]!.Value] = blueCells[i];
            }

            var assignedIds = redIds.Concat(blueIds).Where(id => id != null).Select(id => id!.Value).ToList();

            syncWatches(assignedIds);
            recreateScoreProvider(room, assignedIds);

            // Catch-up: states may already be Playing (arrived before watching, before cells
            // were set, or room data populated after the last event). startGameplay re-guards everything.
            foreach (int userId in assignedIds)
            {
                if (scoresByUser.ContainsKey(userId)
                    && cellsByUser.TryGetValue(userId, out SpectateCell? assignedCell) && assignedCell.HasPlay)
                    continue;

                if (spectatorClient.WatchedUserStates.TryGetValue(userId, out SpectatorState st) && st.State == SpectatedUserState.Playing)
                    startGameplay(userId, st);
            }

            updateScoreBindings();
            updateMusicSuspend();
        }

        /// <summary>
        /// Assigns users to one side's cells. Returns the assigned user ID per cell (null for idle slots).
        /// </summary>
        private List<int?> assignSide(MultiplayerRoom room, IReadOnlyList<SpectateCell> cells, int sideIndex)
        {
            var result = new List<int?>();
            var red = TournamentColours.GetTeamNameColour(TeamColour.Red);
            var blue = TournamentColours.GetTeamNameColour(TeamColour.Blue);

            if (room.MatchState is StandardMatchRoomState matchState && matchState.Slots?.Any(id => id != null) == true)
            {
                // Slot mode: slot order defines teams, hosting side first. Playing state is ignored here;
                // non-playing occupants keep idle backgrounds.
                int n = cells.Count;

                for (int i = 0; i < n; i++)
                {
                    int slotIndex = sideIndex * n + i;
                    int? userId = slotIndex < matchState.Slots.Length ? matchState.Slots[slotIndex] : null;
                    var user = userId == null ? null : room.Users.SingleOrDefault(u => u.UserID == userId);

                    result.Add(user?.UserID);
                    cells[i].Assign(user?.User?.Username, sideIndex == 0 ? red : blue);
                }

                return result;
            }

            bool teamMode = room.MatchState is TeamVersusRoomState;

            // Layout assigns every occupant (including not-downloaded users), matching the official
            // PlayerGrid which keeps slots visible. Playability only gates startGameplay, and users
            // outside CurrentMatchPlayingUserIds never enter Playing mid-match.
            IEnumerable<MultiplayerRoomUser> candidates = room.Users.Where(u => u.Role != MultiplayerRoomUserRole.Referee && u.State != MultiplayerUserState.Spectating);

            if (teamMode)
            {
                // Team colours follow lazer convention: team 0 is red, anything else is blue.
                candidates = candidates.Where(u => (u.MatchState as TeamVersusUserState)?.TeamID == sideIndex);
            }
            else if (sideIndex == 1)
            {
                // No teams: first 2n occupants fill red cells, then blue cells.
                candidates = candidates.Skip(redCells!.Count);
            }

            var picked = candidates.Take(cells.Count).ToList();

            for (int i = 0; i < cells.Count; i++)
            {
                if (i < picked.Count)
                {
                    var user = picked[i];
                    Color4 colour = teamMode
                        ? (sideIndex == 0 ? red : blue)
                        : Color4.White;

                    result.Add(user.UserID);
                    cells[i].Assign(user.User?.Username, colour);
                }
                else
                {
                    result.Add(null);
                    cells[i].Assign(null, Color4.White);
                }
            }

            return result;
        }

        private static bool isPlayable(MultiplayerRoomUser user)
            => user.Role != MultiplayerRoomUserRole.Referee
               && user.State != MultiplayerUserState.Spectating
               && user.BeatmapAvailability is not { State: DownloadState.NotDownloaded };

        /// <summary>
        /// Emergency reset, equivalent to the official room exit-spectate then re-spectate flow:
        /// stops all watches, clears visuals, re-asserts spectating state, then reassigns.
        /// Retained last frames/results are discarded, matching a fresh MultiSpectatorScreen.
        /// </summary>
        public void ResetSpectate()
        {
            teardownVisuals();
            stopAllWatches();

            // Re-assert spectating first, matching MultiSpectatorScreen.OnBackButton which
            // returns to Idle before the screen exits. Staying spectating resumes right away.
            var localUser = multiplayerClient.LocalUser;

            if (localUser?.State == MultiplayerUserState.Idle || localUser?.State == MultiplayerUserState.Ready)
                multiplayerClient.ChangeState(MultiplayerUserState.Spectating).FireAndForget();

            Assign(force: true);
        }

        #endregion

        #region Room events

        private void onRoomUpdated() => Schedule(() =>
        {
            var room = multiplayerClient.Room;

            if (room == null)
            {
                fullTeardown();
                hadRoom = false;
                return;
            }

            // Joined while a match is already underway (loading or playing):
            // assign immediately despite frozen state, as no LoadRequested will come for us.
            // (Vanilla covers the same case by re-firing onLoadRequested on availability changes.)
            if (!hadRoom && room.State != MultiplayerRoomState.Open)
            {
                hadRoom = true;
                Assign(force: true);
                return;
            }

            hadRoom = true;
            Assign();
        });

        private void onLoadRequested() => Schedule(() =>
        {
            if (multiplayerClient.Room == null)
                return;

            // A new match clears previous results/last frames (kept until now).
            teardownVisuals();
            musicController.MusicControlSuspended.Value = true;
            Assign(force: true);
        });

        private void updateMusicSuspend()
        {
            var room = multiplayerClient.Room;

            musicController.MusicControlSuspended.Value = room is { State: MultiplayerRoomState.WaitingForLoad or MultiplayerRoomState.Playing };
        }

        #endregion

        #region Watching and scores

        private void syncWatches(List<int> assignedIds)
        {
            foreach (int userId in watchedUsers.Except(assignedIds).ToList())
                stopWatching(userId);

            ensureUserMap(assignedIds);

            // Watch immediately, including not-downloaded occupants. Playability only gates
            // startGameplay below, matching SpectatorScreen which watches first and waits for beatmaps.
            foreach (int userId in assignedIds.Except(watchedUsers))
                startWatching(userId);
        }

        private void ensureUserMap(IEnumerable<int> userIds)
        {
            int[] missing = userIds.Where(id => !userMap.ContainsKey(id)).ToArray();

            // Seed synchronously from live room data for immediate responsiveness.
            var room = multiplayerClient.Room;

            if (room != null)
            {
                foreach (int id in missing)
                {
                    var roomUser = room.Users.SingleOrDefault(u => u.UserID == id)?.User;

                    if (roomUser != null)
                        userMap[id] = roomUser;
                }
            }

            int[] stillMissing = missing.Where(id => !userMap.ContainsKey(id)).ToArray();

            if (stillMissing.Length == 0)
                return;

            // Async completion, matching SpectatorScreen.GetUsersAsync, then retry gameplay.
            userLookupCache.GetUsersAsync(stillMissing).ContinueWith(task => Schedule(() =>
            {
                foreach (var user in task.GetResultSafely())
                {
                    if (user == null)
                        continue;

                    userMap[user.Id] = user;
                }

                foreach (int id in stillMissing)
                {
                    if (spectatorClient.WatchedUserStates.TryGetValue(id, out SpectatorState state) && state.State == SpectatedUserState.Playing)
                        startGameplay(id, state);
                }
            }));
        }

        private void startWatching(int userId)
        {
            if (!watchedUsers.Add(userId))
                return;

            spectatorClient.WatchUser(userId);

            // Catch states that arrived before watching started.
            if (spectatorClient.WatchedUserStates.TryGetValue(userId, out SpectatorState state))
                onUserStateChanged(userId, state);
        }

        private void stopWatching(int userId)
        {
            if (!watchedUsers.Remove(userId))
                return;

            spectatorClient.StopWatchingUser(userId);
        }

        private void stopAllWatches()
        {
            foreach (int userId in watchedUsers.ToList())
                stopWatching(userId);
        }

        private void onUserStatesChanged(object? sender, NotifyDictionaryChangedEventArgs<int, SpectatorState> e)
        {
            // Matches SpectatorScreen.onUserStatesChanged: only Add/Replace carry new states.
            if (e.NewItems != null)
            {
                foreach (var kvp in e.NewItems)
                    onUserStateChanged(kvp.Key, kvp.Value);
            }
        }

        private void onUserStateChanged(int userId, SpectatorState state)
        {
            if (!watchedUsers.Contains(userId))
                return;

            if (state.RulesetID == null || state.BeatmapID == null)
                return;

            if (!userMap.ContainsKey(userId))
                return;

            switch (state.State)
            {
                case SpectatedUserState.Playing:
                    startGameplay(userId, state);
                    break;

                case SpectatedUserState.Passed:
                    // Keep the score for the results screen, only release the clock.
                    // Matches SpectatorScreen.Passed + MultiSpectatorScreen.PassGameplay.
                    if (scoresByUser.TryGetValue(userId, out Score? passedScore))
                        passedScore.Replay.HasReceivedAllFrames = true;

                    removeClock(userId);
                    break;

                case SpectatedUserState.Failed:
                    // Stop at the fail frame without results, matching MultiSpectatorScreen.FailGameplay.
                    if (scoresByUser.TryGetValue(userId, out Score? failedScore))
                        failedScore.Replay.HasReceivedAllFrames = true;

                    scoresByUser.Remove(userId);
                    removeClock(userId);
                    updateSpectatedScore();
                    break;

                case SpectatedUserState.Quit:
                    // Keep the last frame greyed out until the next match or reset,
                    // matching MultiSpectatorScreen.QuitGameplay.
                    if (scoresByUser.TryGetValue(userId, out Score? quitScore))
                        quitScore.Replay.HasReceivedAllFrames = true;

                    scoresByUser.Remove(userId);
                    removeClock(userId);
                    stopWatching(userId);
                    updateSpectatedScore();

                    if (cellsByUser.TryGetValue(userId, out SpectateCell? quitCell))
                        quitCell.FadeGrey();
                    break;
            }
        }

        private void startGameplay(int userId, SpectatorState state)
        {
            if (state.RulesetID == null || state.BeatmapID == null)
            {
                Logger.Log($"Skipping spectate start for user {userId}: missing ruleset or beatmap ID.", LoggingTarget.Runtime, LogLevel.Verbose);
                return;
            }

            if (!userMap.TryGetValue(userId, out APIUser? apiUser))
            {
                apiUser = multiplayerClient.Room?.Users.SingleOrDefault(u => u.UserID == userId)?.User;

                if (apiUser == null)
                {
                    Logger.Log($"Skipping spectate start for user {userId}: user not found.", LoggingTarget.Runtime, LogLevel.Verbose);
                    return;
                }

                userMap[userId] = apiUser;
            }

            // No mid-match join: only users in the server-determined playing list may start
            // while WaitingForLoad/Playing. Late downloads wait for the next match.
            var room = multiplayerClient.Room;

            if (room is { State: MultiplayerRoomState.WaitingForLoad or MultiplayerRoomState.Playing }
                && !multiplayerClient.CurrentMatchPlayingUserIds.Contains(userId))
            {
                Logger.Log($"Skipping spectate start for user {userId}: not in current playing list.", LoggingTarget.Runtime, LogLevel.Verbose);
                return;
            }

            // Local availability gate, matching the official isPlayable intent.
            var roomUser = room?.Users.SingleOrDefault(u => u.UserID == userId);

            if (roomUser != null && !isPlayable(roomUser))
            {
                Logger.Log($"Skipping spectate start for user {userId}: beatmap not available locally.", LoggingTarget.Runtime, LogLevel.Verbose);
                return;
            }

            // Skip only when gameplay is already attached. A stored score without an area
            // (e.g. states arrived before cells were set) must retry so the area gets built.
            if (scoresByUser.ContainsKey(userId)
                && cellsByUser.TryGetValue(userId, out SpectateCell? existingCell) && existingCell.HasPlay)
                return;

            var ruleset = rulesets.AvailableRulesets.FirstOrDefault(r => r.OnlineID == state.RulesetID);

            if (ruleset == null)
            {
                Logger.Log($"Skipping spectate start for user {userId}: unknown ruleset {state.RulesetID}.", LoggingTarget.Runtime, LogLevel.Verbose);
                return;
            }

            var rulesetInstance = ruleset.CreateInstance();

            var beatmapInfo = beatmaps.QueryBeatmap(b => b.OnlineID == state.BeatmapID);

            if (beatmapInfo == null)
            {
                Logger.Log($"Skipping spectate start for user {userId}: beatmap {state.BeatmapID} not available locally.", LoggingTarget.Runtime, LogLevel.Verbose);
                return;
            }

            var score = new Score
            {
                ScoreInfo = new ScoreInfo(beatmapInfo, ruleset, null)
                {
                    User = apiUser,
                    Mods = state.Mods.Select(m => m.ToMod(rulesetInstance)).ToArray(),
                },
                Replay = new Replay { HasReceivedAllFrames = false },
            };

            scoresByUser[userId] = score;
            SpectatedItem ??= multiplayerClient.Room?.CurrentPlaylistItem.Clone();
            updateSpectatedScore();

            Schedule(() =>
            {
                if (syncManager == null)
                    return;

                if (!cellsByUser.TryGetValue(userId, out SpectateCell? cell) || cell.HasPlay)
                    return;

                if (!scoresByUser.TryGetValue(userId, out Score? playScore))
                    return;

                var clock = syncManager.CreateManagedClock();
                var area = new TournamentPlayerArea(userId, clock);

                clocksByUser[userId] = clock;
                cell.AttachPlayArea(area);

                // Pushes the inner MultiSpectatorPlayerLoader -> MultiSpectatorPlayer.
                // Without this the area stays on its loading spinner forever.
                area.LoadScore(playScore);

                if (leaderboardLoaded && leaderboardProvider != null)
                {
                    try
                    {
                        leaderboardProvider.AddClock(userId, clock);
                    }
                    catch (ArgumentException)
                    {
                        // Reassigned since; the current provider flush covers it or drops it.
                    }
                }
            });
        }

        /// <summary>
        /// Whether the room's current playlist item map is available locally.
        /// </summary>
        private bool itemMapAvailable()
        {
            var room = multiplayerClient.Room;
            var item = room?.Playlist.FirstOrDefault(i => i.ID == room.Settings.PlaylistItemId);
            return item != null && beatmaps.QueryBeatmap(b => b.OnlineID == item.BeatmapID) != null;
        }

        private void retryPendingMaps()
        {
            // Matches SpectatorScreen.beatmapUpdated: a newly imported set may unblock pending starts.
            // Mid-match joins stay gated by CurrentMatchPlayingUserIds inside startGameplay.
            var room = multiplayerClient.Room;

            // Rebuild missing clocks (e.g. previous build skipped for a missing map), or upgrade
            // a fallback-built master now that the room item map is available. Runs even while
            // frozen: spectators may finish downloading mid-match.
            if (room != null && (masterClock == null || syncManager == null || (!clockFromItem && itemMapAvailable())))
            {
                if (setupClocks())
                    clockItemId = currentItemId();
            }

            foreach (int userId in watchedUsers.ToList())
            {
                if (scoresByUser.ContainsKey(userId)
                    && cellsByUser.TryGetValue(userId, out SpectateCell? pendingCell) && pendingCell.HasPlay)
                    continue;

                if (!userMap.ContainsKey(userId))
                    continue;

                if (spectatorClient.WatchedUserStates.TryGetValue(userId, out SpectatorState state) && state.State == SpectatedUserState.Playing)
                    startGameplay(userId, state);
            }
        }

        private void onBeatmapAvailabilityChanged(ValueChangedEvent<BeatmapAvailability> e)
        {
            if (multiplayerClient.Room == null || multiplayerClient.LocalUser == null)
                return;

            multiplayerClient.ChangeBeatmapAvailability(e.NewValue).FireAndForget();

            // Optimistically enter spectator if the match is in progress while spectating,
            // matching MultiplayerMatchSubScreen.onBeatmapAvailabilityChanged.
            if (e.NewValue.State == DownloadState.LocallyAvailable
                && multiplayerClient.LocalUser.State == MultiplayerUserState.Spectating
                && multiplayerClient.Room.State is MultiplayerRoomState.WaitingForLoad or MultiplayerRoomState.Playing)
            {
                retryPendingMaps();
            }
        }

        #endregion

        #region Clocks, seek, audio and scores

        private long? currentItemId()
        {
            var room = multiplayerClient.Room;
            return room?.Playlist.FirstOrDefault(i => i.ID == room.Settings.PlaylistItemId)?.ID;
        }

        /// <summary>
        /// (Re)builds the master/sync clocks for the room's current playlist item.
        /// Returns whether clocks are ready: false when the selected beatmap has no content
        /// to build from (e.g. not downloaded and only an empty fallback available), in which
        /// case existing clocks are kept and a later Assign retries.
        /// </summary>
        private bool setupClocks()
        {
            // Prefer the global instance when it already points at the room item: it is the
            // same object the preview and MusicController manage, so the spectator master clock
            // takes over the playing track instead of starting a second one (mirrors
            // MultiSpectatorScreen, which builds its clock from Beatmap.Value).
            // Falls back to a fresh lookup when the global points elsewhere.
            WorkingBeatmap masterBeatmap = beatmap.Value;
            var currentItem = multiplayerClient.Room?.CurrentPlaylistItem;

            if (currentItem != null && beatmap.Value?.BeatmapInfo?.OnlineID != currentItem.BeatmapID)
            {
                var localInfo = beatmaps.QueryBeatmap(b => b.OnlineID == currentItem.BeatmapID);

                if (localInfo != null)
                    masterBeatmap = beatmaps.GetWorkingBeatmap(localInfo);
                else
                    Logger.Log($"Master clock using fallback beatmap; item beatmap {currentItem.BeatmapID} not available locally.", LoggingTarget.Runtime, LogLevel.Verbose);
            }

            // A beatmap without hit objects (e.g. the empty default beatmap used as fallback
            // when the room map is not downloaded) cannot back a master clock:
            // MasterGameplayClockContainer reads the first hit object in its constructor.
            if (masterBeatmap.Beatmap.HitObjects.Count == 0)
            {
                Logger.Log("Skipping master clock build: selected beatmap has no hit objects.", LoggingTarget.Runtime, LogLevel.Verbose);
                return masterClock != null && syncManager != null;
            }

            masterClock?.Expire();

            syncManager?.Expire();

            masterWorkingBeatmap = masterBeatmap;
            clockFromItem = currentItem == null || masterBeatmap.BeatmapInfo?.OnlineID == currentItem.BeatmapID;

            // MasterGameplayClockContainer reads WorkingBeatmap.Track in its constructor,
            // which throws when the track was never loaded (e.g. a fresh instance that no
            // preview ever prepared, or globals assigned while music control is suspended).
            // Load it synchronously like TournamentPlayerArea.LoadScore does for player areas.
            // The track is intentionally never disposed here: working beatmaps are shared
            // cache instances, and disposing would poison every future reuse because
            // TrackLoaded stays true while pointing at the disposed track. Lifetime belongs
            // to the WorkingBeatmap cache and MusicController, matching official usage.
            // A cached track may still reference a disposed object (released by an older
            // lifecycle); LoadTrack always installs a fresh one (TrackStore never reuses
            // instances), so probing disposal state heals such cases too.
            if (!TournamentMusicController.IsTrackUsable(masterBeatmap))
                masterBeatmap.LoadTrack();

            AddInternal(masterClock = new MasterGameplayClockContainer(masterBeatmap, 0));
            AddInternal(syncManager = new SpectatorSyncManager(masterClock) { ReadyToStart = performInitialSeek });
            return true;
        }

        private void performInitialSeek()
        {
            var startTimes = new List<double>();

            foreach (var score in scoresByUser.Values)
            {
                var firstFrame = score.Replay.Frames.MinBy(f => f.Time);

                if (firstFrame != null)
                    startTimes.Add(firstFrame.Time);
            }

            if (startTimes.Count == 0 || masterClock == null)
                return;

            // Drop late outliers (same rule as lazer: further than a second past the mean).
            double mean = startTimes.Average();
            startTimes.RemoveAll(t => mean - t > 1000);

            masterClock.Reset(startTimes.Min(), true);
        }

        private void removeClock(int userId)
        {
            if (clocksByUser.TryGetValue(userId, out SpectatorPlayerClock? clock))
            {
                clocksByUser.Remove(userId);
                syncManager?.RemoveManagedClock(clock);
            }
        }

        protected override void Update()
        {
            base.Update();
            pumpPlayerClocks();
            checkAudioSource();
        }

        /// <summary>
        /// Advances player clocks even while the gameplay visuals are hidden.
        /// Hidden subtrees skip updates (see CompositeDrawable.UpdateSubTree), which would freeze
        /// player clocks against the real-time master track and force a 2x catch-up on return.
        /// SpectatorPlayerClock.ProcessFrame consumes each master advance exactly once, so pumping
        /// here is safe alongside the official containers while visible.
        /// </summary>
        private void pumpPlayerClocks()
        {
            foreach (var clock in clocksByUser.Values)
                clock.ProcessFrame();
        }

        private void checkAudioSource()
        {
            if (syncManager == null || masterClock == null)
                return;

            if (currentAudioUserId != null
                && clocksByUser.TryGetValue(currentAudioUserId.Value, out SpectatorPlayerClock? current)
                && isCandidate(current))
                return;

            int? best = null;
            double bestDelta = double.MaxValue;

            foreach (var (userId, clock) in clocksByUser)
            {
                if (!isCandidate(clock))
                    continue;

                double delta = Math.Abs(clock.CurrentTime - syncManager.CurrentMasterTime);

                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = userId;
                }
            }

            currentAudioUserId = best;

            if (best != null && cellsByUser.TryGetValue(best.Value, out SpectateCell? bestCell) && bestCell.PlayArea != null)
                bindAudioAdjustments(bestCell.PlayArea);

            foreach (var (userId, cell) in cellsByUser)
            {
                cell.PlayArea?.Mute = best == null || userId != best;
            }

            static bool isCandidate(SpectatorPlayerClock? clock)
                => clock != null && clock.IsRunning && !clock.IsCatchingUp && !clock.WaitingOnFrames;
        }

        private void bindAudioAdjustments(TournamentPlayerArea area)
        {
            if (boundAdjustments != null)
                masterClock?.AdjustmentsFromMods.UnbindAdjustments(boundAdjustments);

            boundAdjustments = area.ClockAdjustmentsFromMods;
            masterClock?.AdjustmentsFromMods.BindAdjustments(boundAdjustments);
        }

        private void recreateScoreProvider(MultiplayerRoom room, List<int> assignedIds)
        {
            var assignedSet = assignedIds.ToHashSet();

            if (leaderboardProvider != null && providerUserIds.SetEquals(assignedSet))
                return;

            providerUserIds = assignedSet;

            if (leaderboardProvider != null)
            {
                leaderboardProvider.Expire();
                leaderboardProvider = null;
                leaderboardLoaded = false;
            }

            foreach (var sub in comboSubscriptions)
                sub.bindable.ValueChanged -= sub.handler;

            comboSubscriptions.Clear();
            comboSubscribed.Clear();
            seenBreak.Clear();

            var users = room.Users.Where(u => assignedSet.Contains(u.UserID)).ToArray();

            LoadComponentAsync(leaderboardProvider = new TournamentLeaderboardProvider(users), loaded =>
            {
                AddInternal(loaded);
                leaderboardLoaded = true;

                foreach (var (userId, clock) in clocksByUser)
                {
                    try
                    {
                        loaded.AddClock(userId, clock);
                    }
                    catch (ArgumentException)
                    {
                        // Clock for a user outside the provider's roster (reassigned since).
                    }
                }

                foreach (int userId in watchedUsers)
                    subscribeComboBreak(userId);

                updateScoreBindings();
            });
        }

        private void subscribeComboBreak(int userId)
        {
            if (leaderboardProvider == null || !leaderboardLoaded)
                return;

            if (comboSubscribed.Contains(userId))
                return;

            if (!leaderboardProvider.TryGetCombo(userId, out BindableInt? combo) || combo == null)
                return;

            comboSubscribed.Add(userId);

            void handler(ValueChangedEvent<int> c)
            {
                // Same rule as in-game ComboEffects: break past 20, or first break with the config enabled.
                if (c.NewValue == 0 && (c.OldValue > 20 || (alwaysPlayFirstBreak.Value && seenBreak.Add(userId))))
                {
                    breakSound?.Play();

                    if (cellsByUser.TryGetValue(userId, out SpectateCell? cell))
                        cell.NotifyComboBreak();
                }
            }

            combo.BindValueChanged(handler);
            comboSubscriptions.Add((combo, handler));
        }

        private void updateScoreBindings()
        {
            bool hasTeams = leaderboardLoaded
                            && leaderboardProvider != null
                            && leaderboardProvider.TeamScores.Count == 2
                            && multiplayerClient.Room?.MatchState is TeamVersusRoomState;

            HasTeamScores.Value = hasTeams;

            if (ScoreDisplay == null || !hasTeams)
                return;

            // Rebinding an already-bound bindable throws, and the provider instance is replaced
            // on every rebuild (reset, new match). Unbind first so switching providers is safe.
            // UnbindBindings only removes BindTo links; display ValueChanged handlers are kept.
            ScoreDisplay.Team1Score.UnbindBindings();
            ScoreDisplay.Team2Score.UnbindBindings();
            ScoreDisplay.Team1Score.BindTarget = leaderboardProvider!.TeamScores.First().Value;
            ScoreDisplay.Team2Score.BindTarget = leaderboardProvider!.TeamScores.Last().Value;
        }

        #endregion

        #region Teardown

        private void teardownVisuals()
        {

            foreach (var cell in cellsByUser.Values)
                cell.ClearPlay();

            cellsByUser.Clear();
            clocksByUser.Clear();
            scoresByUser.Clear();
            SpectatedItem = null;
            updateSpectatedScore();
            userMap.Clear();
            seenBreak.Clear();
            currentAudioUserId = null;
            boundAdjustments = null;
            clockItemId = null;
            clockFromItem = false;

            if (masterClock != null)
            {
                // Detach the dying clock from the shared live track so straggler operations
                // land on a virtual track. Matches Player behaviour on gameplay end.
                masterClock.StopUsingBeatmapClock();
                masterClock.Expire();
                masterClock = null;
            }

            syncManager?.Expire();
            syncManager = null;

            if (leaderboardProvider != null)
            {
                leaderboardProvider.Expire();
                leaderboardProvider = null;
                leaderboardLoaded = false;
            }

            foreach (var sub in comboSubscriptions)
                sub.bindable.ValueChanged -= sub.handler;

            comboSubscriptions.Clear();
            comboSubscribed.Clear();
            providerUserIds.Clear();

            // Release the score display so it never points at an expired provider,
            // and the next bind starts clean.
            ScoreDisplay?.Team1Score.UnbindBindings();
            ScoreDisplay?.Team2Score.UnbindBindings();

            HasTeamScores.Value = false;
        }

        private void fullTeardown()
        {
            teardownVisuals();
            stopAllWatches();
            updateMusicSuspend();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (multiplayerClient != null)
            {
                multiplayerClient.RoomUpdated -= onRoomUpdated;
                multiplayerClient.LoadRequested -= onLoadRequested;
            }

            realmSubscription?.Dispose();
            userWatchToken?.Dispose();

            // Matches SpectatorScreen.Dispose: stop every watch.
            fullTeardown();
        }

        /// <summary>
        /// Leaderboard provider with combo-break observability for tournament spectating.
        /// </summary>
        private partial class TournamentLeaderboardProvider : MultiSpectatorLeaderboardProvider
        {
            public TournamentLeaderboardProvider(MultiplayerRoomUser[] users)
                : base(users)
            {
            }

            public bool TryGetCombo(int userId, out BindableInt? combo)
            {
                if (UserScores.TryGetValue(userId, out TrackedUserData data))
                {
                    combo = data.ScoreProcessor.Combo;
                    return true;
                }

                combo = null;
                return false;
            }
        }

        #endregion
    }
}
