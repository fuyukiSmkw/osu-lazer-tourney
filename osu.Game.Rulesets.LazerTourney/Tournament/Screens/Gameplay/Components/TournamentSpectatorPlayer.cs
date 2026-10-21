// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.UI.Results.Simple;
using osu.Game.Rulesets.LazerTourney.Tournament.UI.Results.V2;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.OnlinePlay.Multiplayer.Spectate;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay.Components
{
    /// <summary>
    /// Tournament spectator player. Identical to <see cref="MultiSpectatorPlayer"/> except the
    /// official white username label is removed: tournament cells show their own team-coloured
    /// username label instead.
    /// A custom mod multiplier can additionally be applied via <see cref="ScoreWeight"/>.
    /// </summary>
    public partial class TournamentSpectatorPlayer : MultiSpectatorPlayer
    {
        /// <summary>
        /// Per-score custom multiplier applied on top of the official score.
        /// 1 means no adjustment (no subscription, zero behaviour change).
        /// Set before load; mods are locked once a match starts so one weight lasts the whole play.
        /// </summary>
        public double ScoreWeight { get; set; } = 1;

        /// <summary>
        /// Public access to the score processor for external consumers.
        /// </summary>
        public ScoreProcessor ScoreProcessorPublic => ScoreProcessor;

        /// <summary>
        /// Public access to the health processor for external consumers.
        /// </summary>
        public HealthProcessor HealthProcessorPublic => HealthProcessor;

        /// <summary>
        /// Public access to the gameplay state (the parsed playable beatmap, live mods and
        /// ruleset) once gameplay has started. Lets the tournament data server reuse the
        /// in-memory beatmap for difficulty/performance calculations instead of re-parsing.
        /// </summary>
        public GameplayState GameplayStatePublic => GameplayState;

        public TournamentSpectatorPlayer(Score score, SpectatorPlayerClock clock)
            : base(score, clock)
        {
        }

        [Resolved]
        private LadderInfo ladderInfo { get; set; } = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // The base class adds a white username label at the top centre of the gameplay clock container.
            // Remove it (rather than hiding) so no layout space is reserved for it.
            foreach (var label in GameplayClockContainer.ChildrenOfType<OsuTextFlowContainer>().ToList())
            {
                if (label.Anchor == Anchor.TopCentre && label.Origin == Anchor.TopCentre)
                    label.RemoveAndDisposeImmediately();
            }

            // Scale the locally-replayed total for display only. HUD counters bind a copy of
            // TotalScore and results read ScoreInfo.TotalScore (populated from here), so both
            // follow with this single hook. Accuracy/combo/rank are untouched, and the
            // leaderboard provider tracks its own separate processor, so nothing double-counts.
            // The processor always recomputes from the raw subtotal, so overwriting here
            // never accumulates error across updates.
            if (ScoreWeight != 1)
                ScoreProcessor.TotalScore.BindValueChanged(onTotalScoreChanged);
        }

        private bool adjustingScore;

        private void onTotalScoreChanged(ValueChangedEvent<long> total)
        {
            if (adjustingScore)
                return;

            adjustingScore = true;
            ScoreProcessor.TotalScore.Value = (long)System.Math.Round(total.NewValue * ScoreWeight);
            adjustingScore = false;
        }

        protected override ResultsScreen CreateResults(ScoreInfo score)
        {
            // No re-watch/retry from the embedded cell results for any style: the watch button
            // would push onto the real screen stack and hijack the tournament UI.
            return ladderInfo.ResultScreen.Value switch
            {
                ResultScreenStyle.Simple => new SimpleResultsScreen(score)
                {
                    AllowWatchingReplay = false,
                    AllowRetry = false,
                },
                ResultScreenStyle.V2 => new V2ResultsScreen(score)
                {
                    AllowWatchingReplay = false,
                    AllowRetry = false,
                },
                _ => new MultiSpectatorResultsScreen(score)
                {
                    AllowWatchingReplay = false,
                    AllowRetry = false,
                },
            };
        }
    }
}
