// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Screens;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Showcase
{
    /// <summary>
    /// Tournament copy of <see cref="ReplayPlayerLoader"/> that creates
    /// <see cref="TournamentReplayPlayer"/> instead of <see cref="ReplayPlayer"/>.
    /// Everything else mirrors official.
    /// </summary>
    public partial class TournamentReplayPlayerLoader : PlayerLoader
    {
        public readonly ScoreInfo Score;

        private readonly ShowcaseReplayController controller;

        public TournamentReplayPlayerLoader(Score score, ShowcaseReplayController controller)
            : base(() => new TournamentReplayPlayer(score, controller))
        {
            if (score.Replay == null)
                throw new ArgumentException($"{nameof(score)} must have a non-null {nameof(score.Replay)}.", nameof(score));

            Score = score.ScoreInfo;
            this.controller = controller;
            WindowShouldBeActiveForGameplayStart = false;
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            // these will be reverted thanks to PlayerLoader's lease.
            Mods.Value = Score.Mods;
            Ruleset.Value = Score.Ruleset;

            base.OnEntering(e);
        }
    }
}
