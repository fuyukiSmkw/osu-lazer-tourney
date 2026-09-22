// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
    /// Keeps global beatmap/ruleset/mods and background music in sync with the multiplayer room,
    /// mirroring MultiplayerMatchSubScreen (updateGameplayState plus begin/endHandlingTrack):
    /// while in a room the preview track of the room's current playlist item plays (looped);
    /// while spectating, the spectator master clock takes over that same track;
    /// while out of a room the default menu behaviour applies.
    /// </summary>
    public partial class TournamentMusicController : Component, IPreviewTrackOwner
    {
        /// <summary>
        /// While set, preview handling is released so spectate playback owns the audio,
        /// mirroring the room screen suspending when gameplay is pushed.
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

            onlineState.RoomJoined.BindValueChanged(_ => Scheduler.AddOnce(updateMusic));
            onlineState.CurrentPlaylistItem.BindValueChanged(_ => Scheduler.AddOnce(updateMusic));

            MusicControlSuspended.BindValueChanged(v => Schedule(() =>
            {
                if (v.NewValue)
                    endHandlingTrack();
                else
                {
                    updateMusic();
                    beginHandlingTrack();
                }
            }));

            // Downloads completing change local availability without changing the playlist item,
            // so refresh from storage inserts as well (mirrors SpectatorScreen.beatmapsChanged).
            realmSubscription = realm.RegisterForNotifications<BeatmapSetInfo>(
                r => r.All<BeatmapSetInfo>().Where(s => !s.DeletePending),
                (sender, changes) =>
                {
                    if (changes?.InsertedIndices == null)
                        return;

                    Schedule(updateMusic);
                });

            updateMusic();
            beginHandlingTrack();
        }

        /// <summary>
        /// Points global beatmap/ruleset/mods at the room's current playlist item.
        /// Mirrors MultiplayerMatchSubScreen.updateGameplayState (runs even mid-spectate).
        /// </summary>
        private void updateMusic()
        {
            if (!IsLoaded)
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
                // Mirror official: fall back to the default beatmap so menu music continues normally.
                // Resumes automatically once the room map is downloaded (see realm subscription above).
                game.Beatmap.Value = beatmaps.GetWorkingBeatmap(null);
                hasRoomTrack = false;
                stopRoomPreview();
                music.EnsurePlayingSomething();
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
