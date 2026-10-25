// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Audio.Effects;
using osu.Game.Scoring;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay.Components
{
    /// <summary>
    /// Tournament copy of <see cref="Game.Screens.OnlinePlay.Multiplayer.Spectate.MultiSpectatorPlayerLoader"/> for use inside <see cref="TournamentPlayerArea"/>.
    /// </summary>
    /// <remarks>
    /// Forked because <see cref="PlayerLoader"/> mutates shared audio on entering and suspending. That is
    /// harmless on standalone screens, which stop the old music into silence right before taking over,
    /// but destructive in tournament spectate: the track manipulated via <c>Beatmap.Value.Track</c> is
    /// the very track the running master clock uses as its audio source, shared by every
    /// <see cref="SpectateCell"/>. Any mid-match cell load exposes it:
    /// <list type="bullet">
    /// <item>On entering, cosmetic low/high-pass filters (an "entrance blur" transition) are attached
    /// to the global <c>AudioManager.TrackMixer</c>, muffling the music all other cells are hearing.</item>
    /// <item>On push, the track volume is faded to zero (the dim adjustment added on entering) and the
    /// loaded player suspends the loader, stopping the track outright and silencing the master clock
    /// for the rest of the match (observed after per-player resets and late chart downloads).</item>
    /// </list>
    /// Both side effects are neutralised here; everything else mirrors official behaviour.
    /// </remarks>
    public partial class TournamentSpectatorPlayerLoader : SpectatorPlayerLoader
    {
        public TournamentSpectatorPlayerLoader(Score score, Func<TournamentSpectatorPlayer> createPlayer)
            : base(score, createPlayer)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            PlayerSettings.Expire();
        }

        protected override void LogoArriving(OsuLogo logo, bool resuming)
        {
        }

        protected override void LogoExiting(OsuLogo logo)
        {
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            // PlayerLoader attached its music-dimming adjustment to the shared track right before
            // this. Detach it again so the fade-to-zero in the push sequence and PlayerLoader.OnSuspending
            // cannot duck (or, combined with the suspend-time stop, permanently kill) the master clock's
            // live audio. Everything below runs before the next update frame, so nothing is audible.
            if (getLoaderVolumeAdjustment() is BindableDouble volumeAdjustment)
                Beatmap.Value.Track.RemoveAdjustment(AdjustableProperty.Volume, volumeAdjustment);

            // PlayerLoader.contentIn() adds low/high-pass filters to the global track mixer with cutoffs
            // animated in from their initial (passthrough) values. The filters only attach to the mixer
            // once their cutoff transforms start processing, so removing the not-yet-updating filters
            // here prevents them from ever affecting audio. The formal cleanup in PlayerLoader
            // (ContentOut/OnSuspending) then acts on already-disposed drawables, which is a safe no-op
            // (Drawable.Dispose is idempotent).
            foreach (var filter in InternalChildren.OfType<AudioFilter>().ToArray())
                filter.RemoveAndDisposeImmediately();
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            // PlayerLoader.OnSuspending stops the shared beatmap track ahead of pushing the player
            // ("we're moving to player, so a period of silence is upcoming"). In gameplay the stopped
            // track is about to be replaced, but in tournament spectate it remains the audio source of
            // the running master clock. Track.Stop() is a channel pause, so restarting here continues
            // from the exact same position; Stop and Start execute within one update block.
            bool trackWasRunning = Beatmap.Value.Track.IsRunning;

            base.OnSuspending(e);

            if (trackWasRunning)
                Beatmap.Value.Track.Start();
        }

        /// <summary>
        /// Resolves <see cref="PlayerLoader"/>'s private track volume adjustment field once.
        /// Nothing exposes it and a caller cannot remove it otherwise; a reflect miss (upstream rename)
        /// degrades to the official dimming behaviour.
        /// </summary>
        private static readonly FieldInfo? volumeAdjustmentField = resolveVolumeAdjustmentField();

        private static FieldInfo? resolveVolumeAdjustmentField()
        {
            var field = typeof(PlayerLoader).GetField("volumeAdjustment", BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null)
                Logger.Log("PlayerLoader.volumeAdjustment not found via reflection; the spectate cell loader will fall back to the official music dimming behaviour.", LoggingTarget.Runtime, LogLevel.Debug);

            return field;
        }

        private BindableDouble? getLoaderVolumeAdjustment()
        {
            if (volumeAdjustmentField == null)
                return null;

            return (BindableDouble?)volumeAdjustmentField.GetValue(this);
        }
    }
}
