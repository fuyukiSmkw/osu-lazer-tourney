// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;

namespace osu.Game.Rulesets.LazerTourney.Tournament.UI.Results.HSC
{
    /// <summary>
    /// Hello! Shanden Cup (by [SHK]T_T) tournament results screen, shared by the showcase
    /// replay results and the gameplay spectate results.
    /// </summary>
    /// <remarks>
    /// Keeps inheriting <see cref="ResultsScreen"/> so <c>Player.CreateResults</c> overrides
    /// keep working and upstream behaviour (applause, background blur) is kept. The inherited
    /// leaderboard list, statistics overlay and bottom bar are expired on load and replaced
    /// with the HSC card. The parent results screen owns the background; the card itself is
    /// transparent and letterboxes its fixed 4:3 design canvas inside the cell.
    /// </remarks>
    public partial class HSCResultsScreen : ResultsScreen
    {
        private readonly TeamColour teamColour;
        private readonly int? placement;

        public HSCResultsScreen(ScoreInfo score, TeamColour teamColour = TeamColour.Red, int? placement = null)
            : base(score)
        {
            this.teamColour = teamColour;
            this.placement = placement;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Expire the inherited leaderboard list, statistics overlay and bottom bar.
            // The list/statistics are protected; the bottom bar is private and located by shape
            // (see ResultsScreen.load(): full-width strip of TwoLayerButton.SIZE_EXTENDED height).
            ScorePanelList.Expire();
            StatisticsPanel.Expire();

            var bottomBar = this.ChildrenOfType<Container>()
                .FirstOrDefault(c => c.Anchor == Anchor.BottomLeft && c.Height == TwoLayerButton.SIZE_EXTENDED.Y);
            bottomBar?.Expire();

            AddInternal(new HSCResultsContent(Score, teamColour, placement));
        }

        public override bool OnBackButton() => true;

        /// <summary>
        /// Hosts the HSC card and swallows Select/QuickExit keys so the expired base UI can
        /// never be toggled back.
        /// </summary>
        private partial class HSCResultsContent : Container, IKeyBindingHandler<GlobalAction>
        {
            public HSCResultsContent(ScoreInfo? score, TeamColour teamColour, int? placement)
            {
                RelativeSizeAxes = Axes.Both;

                Children = new Drawable[]
                {
                    score == null
                        ? Empty()
                        : new HSCScorePanel(score, teamColour, placement)
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                };
            }

            public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
            {
                if (e.Repeat)
                    return false;

                return e.Action is GlobalAction.Select or GlobalAction.QuickExit;
            }

            public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
            {
            }
        }
    }
}
