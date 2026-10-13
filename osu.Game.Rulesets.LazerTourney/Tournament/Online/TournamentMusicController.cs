// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Overlays;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Online
{
    /// <summary>
    /// Who currently owns the global beatmap/music.
    /// <list type="bullet">
    /// <item><c>Idle</c>: not in a room. Global beatmap is left alone; menu music applies.</item>
    /// <item><c>InRoom</c>: in a room, not spectating. Globals strictly follow <c>CurrentPlaylistItem</c>.</item>
    /// <item><c>Spectating</c>: inside (or just after) a spectated match. Globals are frozen to the spectated item.</item>
    /// <item><c>InShowcase</c>: a showcase replay owns the globals. Frozen like spectating.</item>
    /// </list>
    /// </summary>
    public enum TournamentMusicState
    {
        Idle,
        InRoom,
        Spectating,
        InShowcase,
    }

    /// <summary>
    /// Keeps global beatmap/ruleset/mods and background music in sync with the multiplayer room,
    /// mirroring MultiplayerMatchSubScreen (updateGameplayState plus begin/endHandlingTrack):
    /// while in a room the preview track of the room's current playlist item plays (looped);
    /// while the room map is missing locally playback stays silent until the download completes;
    /// while spectating, the spectator master clock takes over that same track;
    /// while out of a room the default menu behaviour applies.
    /// State machine (<see cref="State"/>): only <c>InRoom</c> follows live room changes.
    /// <c>Spectating</c>/<c>InShowcase</c> freeze globals on entry until an explicit exit.
    /// </summary>
    public partial class TournamentMusicController : Component, IPreviewTrackOwner
    {
        /// <summary>
        /// Current owner of the global beatmap/music.
        /// </summary>
        public readonly Bindable<TournamentMusicState> State = new Bindable<TournamentMusicState>(TournamentMusicState.Idle);

        /// <summary>
        /// While set, preview handling is released so spectate/showcase playback owns the audio,
        /// mirroring the room screen suspending when gameplay is pushed.
        /// Derived from <see cref="State"/>; do not set directly, use the Enter*/Exit* methods.
        /// </summary>
        public readonly BindableBool MusicControlSuspended = new BindableBool();

        public static bool IsTrackUsable(WorkingBeatmap? working)
            => working != null && working.TrackLoaded && !working.Track.IsDisposed;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private MusicController music { get; set; } = null!;

        [Resolved]
        private PreviewTrackManager previews { get; set; } = null!;

        [Resolved]
        private TournamentOnlineState onlineState { get; set; } = null!;

        [Resolved]
        private LadderInfo ladder { get; set; } = null!;

        [Resolved]
        private TournamentGameBase game { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private bool handlingTrack;
        private bool hasRoomTrack;
        private IDisposable? realmSubscription;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            onlineState.RoomJoined.BindValueChanged(_ => Scheduler.AddOnce(onRoomJoinStateChanged), true);
            onlineState.CurrentPlaylistItem.BindValueChanged(_ => Scheduler.AddOnce(onCurrentItemChanged));

            MusicControlSuspended.BindValueChanged(v => Schedule(() =>
            {
                if (v.NewValue)
                    endHandlingTrack();
                else
                {
                    // Only resume preview handling when following the room.
                    // Spectating/showcase exits route through EnterInRoom/EnterIdle explicitly.
                    if (State.Value == TournamentMusicState.InRoom)
                        updateMusic();

                    beginHandlingTrack();
                }
            }));

            // Downloads completing change local availability without changing the playlist item,
            // so refresh from storage inserts as well (mirrors SpectatorScreen.beatmapsChanged).
            // Only InRoom follows these; frozen states re-enter explicitly once ready.
            realmSubscription = realm.RegisterForNotifications<BeatmapSetInfo>(
                r => r.All<BeatmapSetInfo>().Where(s => !s.DeletePending),
                (sender, changes) =>
                {
                    if (changes?.InsertedIndices == null)
                        return;

                    Schedule(() =>
                    {
                        if (State.Value == TournamentMusicState.InRoom)
                            updateMusic();
                    });
                });

            // Initial state (RoomJoined handler runs immediately via BindValueChanged(true) above).
            beginHandlingTrack();
        }

        private void onRoomJoinStateChanged()
        {
            if (!IsLoaded)
                return;

            if (onlineState.RoomJoined.Value)
            {
                // Entering a room (even from showcase playback) hands ownership to the room.
                // ShowcaseScreen tears down its player on this path; the controller only repoints globals.
                if (State.Value == TournamentMusicState.Idle || State.Value == TournamentMusicState.InShowcase)
                    EnterInRoom();
            }
            else
            {
                // Leaving the room keeps the current beatmap playing (idle spec);
                // menu music takes over naturally once the track ends (EnsurePlayingSomething -> NextTrack).
                if (State.Value == TournamentMusicState.InRoom || State.Value == TournamentMusicState.Spectating)
                    EnterIdle();
                // InShowcase keeps playing its replay across room leaves.
            }
        }

        private void onCurrentItemChanged()
        {
            if (!IsLoaded)
                return;

            // Frozen states (spectating results, showcase playback) ignore live queue advances.
            if (State.Value != TournamentMusicState.InRoom)
                return;

            updateMusic();
        }

        private void setState(TournamentMusicState next)
        {
            if (State.Value == next)
                return;

            State.Value = next;
            MusicControlSuspended.Value = next is TournamentMusicState.Spectating or TournamentMusicState.InShowcase;
        }

        /// <summary>
        /// Enters room-following mode: globals immediately snap to <c>CurrentPlaylistItem</c>
        /// (silent when the map is missing locally) and track it from now on.
        /// Runs synchronously so match transitions (exit -> teardown -> re-enter) stay ordered.
        /// Callers from background threads must Schedule first (realm callbacks already do).
        /// </summary>
        public void EnterInRoom()
        {
            setState(TournamentMusicState.InRoom);

            if (!IsLoaded)
                return;

            updateMusic();
            beginHandlingTrack();
        }

        /// <summary>
        /// Enters idle mode: globals are left untouched so playback continues;
        /// once the track ends menu music takes over via <c>EnsurePlayingSomething</c>.
        /// Runs synchronously (see <see cref="EnterInRoom"/>).
        /// </summary>
        public void EnterIdle()
        {
            setState(TournamentMusicState.Idle);

            if (!IsLoaded)
                return;

            hasRoomTrack = false;
            stopRoomPreview();
            music.EnsurePlayingSomething();
        }

        /// <summary>
        /// Freezes globals onto the current room item for spectating.
        /// Only transitions out of <c>InRoom</c>; returns false when the map is missing
        /// locally (caller stays silent and retries after download).
        /// </summary>
        public bool EnterSpectating()
        {
            if (State.Value != TournamentMusicState.InRoom)
                return State.Value == TournamentMusicState.Spectating;

            var item = onlineState.RoomJoined.Value ? onlineState.CurrentPlaylistItem.Value : null;

            if (item == null)
                return false;

            var local = beatmaps.QueryBeatmap(b => b.OnlineID == item.BeatmapID);

            if (local == null)
                return false;

            RulesetInfo? rulesetInfo = rulesets.GetRuleset(item.RulesetID) ?? ladder.Ruleset.Value;

            if (rulesetInfo != null)
            {
                game.Ruleset.Value = rulesetInfo;
                game.Mods.Value = RoundBeatmap.InstantiateMods(item.RequiredMods.ToArray(), rulesetInfo.CreateInstance());
            }

            hasRoomTrack = true;
            game.Beatmap.Value = beatmaps.GetWorkingBeatmap(local);
            ensureUsable(game.Beatmap.Value);

            // Suspend releases preview looping so the spectator master clock owns the same track.
            // The track is left unlooped: it plays through once and stops at the end.
            setState(TournamentMusicState.Spectating);

            return true;
        }

        /// <summary>
        /// Leaves the spectating freeze and resumes room-following (snaps to the live item).
        /// No-op unless currently spectating. Used by reset and by the next match's start
        /// (which then immediately re-enters spectating for the new item).
        /// </summary>
        public void ExitSpectatingToRoom()
        {
            if (State.Value != TournamentMusicState.Spectating)
                return;

            setState(TournamentMusicState.InRoom);

            if (!IsLoaded)
                return;

            updateMusic();
            beginHandlingTrack();
        }

        /// <summary>
        /// Freezes globals onto a showcase replay. Only allowed from idle (or re-drop while showcasing);
        /// returns false when called from room-following/spectating states so the caller can reject the drop.
        /// </summary>
        public bool EnterShowcase(RulesetInfo? rulesetInfo, IBeatmapInfo? beatmapInfo, IReadOnlyList<osu.Game.Rulesets.Mods.Mod>? mods)
        {
            if (State.Value != TournamentMusicState.Idle && State.Value != TournamentMusicState.InShowcase)
                return false;

            if (rulesetInfo != null)
                game.Ruleset.Value = rulesetInfo;

            if (beatmapInfo != null)
            {
                var local = beatmaps.QueryBeatmap(b => b.MD5Hash == beatmapInfo.MD5Hash)
                            ?? beatmaps.QueryBeatmap(b => b.OnlineID == beatmapInfo.OnlineID);

                if (local != null)
                {
                    game.Beatmap.Value = beatmaps.GetWorkingBeatmap(local);
                    ensureUsable(game.Beatmap.Value);
                }
            }

            if (mods != null)
                game.Mods.Value = mods.ToArray();

            hasRoomTrack = false;
            setState(TournamentMusicState.InShowcase);

            return true;
        }

        /// <summary>
        /// Leaves showcase mode: back to room-following when joined, otherwise idle.
        /// </summary>
        public void ExitShowcase()
        {
            if (State.Value != TournamentMusicState.InShowcase)
                return;

            if (onlineState.RoomJoined.Value)
                EnterInRoom();
            else
                EnterIdle();
        }

        /// <summary>
        /// Points global beatmap/ruleset/mods at the room's current playlist item.
        /// Mirrors MultiplayerMatchSubScreen.updateGameplayState.
        /// Only runs in <c>InRoom</c>; frozen states keep their snapshot until an explicit exit.
        /// </summary>
        private void updateMusic()
        {
            if (!IsLoaded)
                return;

            if (State.Value != TournamentMusicState.InRoom)
                return;

            var item = onlineState.RoomJoined.Value ? onlineState.CurrentPlaylistItem.Value : null;

            if (item == null)
            {
                leaveRoomMusic();
                return;
            }

            RulesetInfo? rulesetInfo = rulesets.GetRuleset(item.RulesetID) ?? ladder.Ruleset.Value;

            if (rulesetInfo != null)
            {
                // NOTE: these must be the screen's leased bindables (writes propagate to the game).
                // Child copies from OsuScreenDependencies are one-way and would silently do nothing.
                game.Ruleset.Value = rulesetInfo;
                game.Mods.Value = RoundBeatmap.InstantiateMods(item.RequiredMods.ToArray(), rulesetInfo.CreateInstance());
            }

            var local = beatmaps.QueryBeatmap(b => b.OnlineID == item.BeatmapID);

            if (local == null)
            {
                // The room map is missing locally: stay silent instead of falling through
                // to menu music. The flag is cleared before switching the beatmap so the
                // change notification sees no usable room track and never starts a preview.
                // Resumes automatically once the room map is downloaded (see realm subscription above).
                var previous = game.Beatmap.Value;
                hasRoomTrack = false;

                if (previous != null && IsTrackUsable(previous))
                    previous.Track.Looping = false;

                game.Beatmap.Value = beatmaps.GetWorkingBeatmap(null);
                previews.StopAnyPlaying(this);

                // While suspended the spectator master clock owns the audio; leave it alone.
                if (!MusicControlSuspended.Value)
                    music.Stop();

                return;
            }

            hasRoomTrack = true;

            // Writes go to the screen's leased bindables so they propagate to the game
            // (child copies from OsuScreenDependencies are one-way and would silently do nothing).
            game.Beatmap.Value = beatmaps.GetWorkingBeatmap(local);
            ensureUsable(game.Beatmap.Value);
        }

        /// <summary>
        /// Handles changes in the track to keep it looping while active.
        /// Mirrors MultiplayerMatchSubScreen.beginHandlingTrack.
        /// </summary>
        private void beginHandlingTrack()
        {
            if (handlingTrack)
                return;

            handlingTrack = true;
            game.Beatmap.BindValueChanged(applyLoopingToTrack, true);
        }

        /// <summary>
        /// Stops looping the current track and stops handling further changes to the track.
        /// Mirrors MultiplayerMatchSubScreen.endHandlingTrack.
        /// </summary>
        private void endHandlingTrack()
        {
            if (!handlingTrack)
                return;

            handlingTrack = false;
            game.Beatmap.ValueChanged -= applyLoopingToTrack;
            stopRoomPreview();
            previews.StopAnyPlaying(this);
        }

        /// <summary>
        /// Invoked on changes to the beatmap to loop the track.
        /// Mirrors MultiplayerMatchSubScreen.applyLoopingToTrack.
        /// </summary>
        private void applyLoopingToTrack(ValueChangedEvent<WorkingBeatmap> e)
        {
            if (MusicControlSuspended.Value || !onlineState.RoomJoined.Value || !hasRoomTrack)
                return;

            var working = game.Beatmap.Value;

            if (working == null)
                return;

            ensureUsable(working);
            working.PrepareTrackForPreview(true);
            music.EnsurePlayingSomething();
        }

        private void stopRoomPreview()
        {
            var working = game.Beatmap.Value;

            if (working != null && IsTrackUsable(working))
                working.Track.Looping = false;
        }

        private void leaveRoomMusic()
        {
            hasRoomTrack = false;
            stopRoomPreview();
            music.EnsurePlayingSomething();
        }

        private static void ensureUsable(WorkingBeatmap? working)
        {
            // A shared cached track may have been released by an older lifecycle
            // while TrackLoaded stays true. Reload installs a fresh one.
            if (working != null && !IsTrackUsable(working))
                working.LoadTrack();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            realmSubscription?.Dispose();
            endHandlingTrack();
        }
    }
}
