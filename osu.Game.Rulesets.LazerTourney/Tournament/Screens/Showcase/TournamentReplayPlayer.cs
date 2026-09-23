// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Screens;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Showcase
{
    /// <summary>
    /// Tournament copy of <see cref="ReplayPlayer"/> for the showcase screen.
    /// Identical playback and results flow; differences:
    /// the player HUD overlay (<see cref="ReplayOverlay"/>) is removed so nothing leaks
    /// onto the stream (transport lives in the control panel instead),
    /// and the player registers itself with the shared <see cref="ShowcaseReplayController"/>.
    /// </summary>
    public partial class TournamentReplayPlayer : ReplayPlayer
    {
        private readonly ShowcaseReplayController controller;
        private MasterGameplayClockContainer? masterClock;

        public TournamentReplayPlayer(Score score, ShowcaseReplayController controller)
            : base(score)
        {
            this.controller = controller;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Force-hide the replay HUD overlay (settings gear, watcher message).
            // Fail handling uses the separate fail indicator and is unaffected.
            ReplayOverlay.Expire();

            // Same wiring as PlaybackSettings: panel rate slider <-> master clock rate.
            masterClock = GameplayClockContainer as MasterGameplayClockContainer;

            if (masterClock != null)
                controller.Rate.BindTo(masterClock.UserPlaybackRate);

            controller.Attach(this);
        }

        /// <summary>
        /// Toggles pause, reading the live clock state so keyboard-driven pauses can't desync it.
        /// </summary>
        public void TogglePause()
        {
            if (GameplayClockContainer.IsRunning)
                GameplayClockContainer.Stop();
            else
                GameplayClockContainer.Start();
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            base.OnSuspending(e);

            // Results were pushed over the player (a return trip is impossible:
            // ValidForResume is cleared). Transport controls go limp with it.
            controller.Detach(this);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (masterClock != null)
                controller.Rate.UnbindFrom(masterClock.UserPlaybackRate);

            controller.Detach(this);
        }
    }
}
