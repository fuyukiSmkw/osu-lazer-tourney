// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// Shared standby countdown for the countdown screen.
    /// Hosted in <see cref="TournamentSceneManager"/> (always present) so the countdown
    /// keeps running while other screens are shown: hidden screens skip updates.
    /// Off by default; only runs while manually started.
    /// </summary>
    [Cached]
    public partial class CountdownTimerService : Component
    {
        public const double DEFAULT_SECONDS = 300; // 5 minutes

        /// <summary>
        /// Remaining time in seconds. Never negative.
        /// </summary>
        public readonly BindableDouble RemainingSeconds = new BindableDouble(DEFAULT_SECONDS);

        /// <summary>
        /// Whether the countdown is currently running.
        /// </summary>
        public readonly BindableBool Running = new BindableBool();

        protected override void Update()
        {
            base.Update();

            if (Running.Value && RemainingSeconds.Value > 0)
            {
                RemainingSeconds.Value = Math.Max(0, RemainingSeconds.Value - Clock.ElapsedFrameTime / 1000);

                if (RemainingSeconds.Value <= 0)
                    Running.Value = false;
            }
        }

        /// <summary>
        /// Toggles between running and paused. Starting at zero does nothing.
        /// </summary>
        public void Toggle()
        {
            if (!Running.Value && RemainingSeconds.Value <= 0)
                return;

            Running.Value = !Running.Value;
        }
    }
}
