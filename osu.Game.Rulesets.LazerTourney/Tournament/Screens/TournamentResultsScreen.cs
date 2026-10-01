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
using osu.Game.Rulesets.LazerTourney.Tournament.Components;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens
{
    /// <summary>
    /// Tournament results screen, shared by the showcase replay results
    /// and the gameplay spectate results. Shows only the current score:
    /// no leaderboard, no bottom bar.
    /// </summary>
    /// <remarks>
    /// Keeps inheriting <see cref="ResultsScreen"/> so <c>Player.CreateResults</c>
    /// overrides keep working and upstream behaviour (applause, background blur) is kept.
    /// The inherited leaderboard list, statistics overlay and bottom bar are expired on load
    /// and replaced with a static custom layout (basic info left, details right, no popups).
    /// </remarks>
    public partial class TournamentResultsScreen : ResultsScreen
    {
        public TournamentResultsScreen(ScoreInfo score)
            : base(score)
        {
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

            AddInternal(new ResultsContent(Score));
        }

        public override bool OnBackButton() => true;

        /// <summary>
        /// Static custom layout: basic info on the left, details directly on the right.
        /// Also swallows Select/QuickExit keys so the expired base UI can never be toggled back.
        /// </summary>
        private partial class ResultsContent : Container, IKeyBindingHandler<GlobalAction>
        {
            public ResultsContent(ScoreInfo? score)
            {
                RelativeSizeAxes = Axes.Both;

                Children = new Drawable[]
                {
                    /*new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                        Alpha = 0.85f,
                    },*/
                    new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(0.7f, 0.7f),
                        RelativeSizeAxes = Axes.Both,
                        Child = score == null
                            ? Empty()
                            : new TournamentScorePanel(score)
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
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
