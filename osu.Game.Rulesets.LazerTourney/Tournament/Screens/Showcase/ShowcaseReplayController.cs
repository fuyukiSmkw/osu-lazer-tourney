// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Showcase
{
    /// <summary>
    /// Shared state between the showcase replay player and the control panel transport controls.
    /// Owned by <see cref="ShowcaseScreen"/> and passed to both sides directly
    /// (the panel is not a descendant of the player, so dependency injection cannot link them).
    /// Bounds mirror <c>PlaybackSettings.UserPlaybackRate</c>.
    /// </summary>
    public class ShowcaseReplayController
    {
        public readonly BindableDouble Rate = new BindableDouble(1)
        {
            MinValue = 0.05,
            MaxValue = 2,
            Precision = 0.01,
        };

        public readonly BindableBool HasPlayer = new BindableBool();

        public TournamentReplayPlayer? Player { get; private set; }

        /// <summary>
        /// Live gameplay clock of the attached player, for dependency-providing containers.
        /// </summary>
        public IGameplayClock? Clock { get; set; }

        /// <summary>
        /// Live drawable ruleset of the attached player, for dependency-providing containers.
        /// </summary>
        public DrawableRuleset? DrawableRuleset { get; set; }

        public void Attach(TournamentReplayPlayer player)
        {
            Player = player;
            HasPlayer.Value = true;
        }

        public void Detach(TournamentReplayPlayer player)
        {
            if (Player == player)
                Player = null;

            Clock = null;
            DrawableRuleset = null;
            HasPlayer.Value = false;
        }

        public void SeekSeconds(float seconds) => Player?.SeekInDirection(seconds);

        public void StepFrame(int direction) => Player?.StepFrame(direction);

        public void TogglePause() => Player?.TogglePause();
    }
}
