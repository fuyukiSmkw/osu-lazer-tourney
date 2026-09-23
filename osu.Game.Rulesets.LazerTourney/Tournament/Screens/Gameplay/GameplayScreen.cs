// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.LazerTourney.Tournament.Components;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Online;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay.Components;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.MapPool;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.TeamWin;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay
{
    public partial class GameplayScreen : BeatmapInfoScreen
    {
        private readonly BindableBool warmup = new BindableBool();

        public readonly Bindable<TourneyState> State = new Bindable<TourneyState>();
        private TournamentOnlineState onlineState = null!;

        [Resolved]
        private TournamentSceneManager? sceneManager { get; set; }

        [Resolved]
        private TournamentMatchChatDisplay chat { get; set; } = null!;

        private Container playOuter = null!;
        private Container redSide = null!;
        private Container blueSide = null!;
        private List<SpectateCell> redCells = new List<SpectateCell>();
        private List<SpectateCell> blueCells = new List<SpectateCell>();

        [Resolved]
        private SpectateSession spectateSession { get; set; } = null!;

        private SeasonalBackgroundLoader backgroundLoader = null!;

        [BackgroundDependencyLoader]
        private void load(TournamentOnlineState onlineState)
        {
            this.onlineState = onlineState;

            LabelledSwitchButton chatToggle;

            AddRangeInternal(new Drawable[]
            {
                new TourneyVideo("gameplay")
                {
                    Loop = true,
                    RelativeSizeAxes = Axes.Both,
                },
                header = new MatchHeader
                {
                    ShowLogo = false,
                },
                playOuter = new Container
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 110,
                    Children = new Drawable[]
                    {
                        redSide = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Width = 0.5f,
                        },
                        blueSide = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Width = 0.5f,
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                        },
                    }
                },
                scoreDisplay = new TournamentMatchScoreDisplay
                {
                    Y = -147,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.TopCentre,
                },
                new SyncedControlPanel
                {
                    Children = new Drawable[]
                    {
                        new LabelledSwitchButton
                        {
                            Label = "Warmup",
                            Current = warmup,
                        },
                        chatToggle = new LabelledSwitchButton
                        {
                            Label = "Show chat",
                        },
                        new SettingsSlider<int>
                        {
                            LabelText = "Chroma width",
                            Current = LadderInfo.ChromaKeyWidth,
                            KeyboardStep = 1,
                        },
                        new SettingsSlider<int>
                        {
                            LabelText = "Players per team",
                            Current = LadderInfo.PlayersPerTeam,
                            KeyboardStep = 1,
                        },
                        new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Reset spectate",
                            Margin = new MarginPadding(3),
                            Action = () =>
                            {
                                // Rebuild the grid from the current players-per-team value first
                                // so layout changes apply immediately, then reset onto the new cells.
                                rebuildLayout(Math.Clamp(LadderInfo.PlayersPerTeam.Value, 1, 4));
                                spectateSession.ResetSpectate();
                            },
                        },
                    }
                },
                backgroundLoader = new SeasonalBackgroundLoader(),
            });

            spectateSession.ScoreDisplay = scoreDisplay;

            State.BindValueChanged(state => chatToggle.Current.Value = State.Value == TourneyState.Idle, true);
            chatToggle.Current.BindValueChanged(v => State.Value = v.NewValue ? TourneyState.Idle : TourneyState.Playing);

            LadderInfo.ChromaKeyWidth.BindValueChanged(width => playOuter.Width = width.NewValue, true);

            warmup.BindValueChanged(w => header.ShowScores = !w.NewValue, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            onlineState.RoomState.BindValueChanged(state =>
            {
                State.Value = state.NewValue.ToTourneyState();
            }, true);

            State.BindValueChanged(_ => updateState(), true);

            LadderInfo.PlayersPerTeam.BindValueChanged(count =>
            {
                // Frozen mid-match: layout and assignment stay untouched to preserve
                // results/disconnect frames. Applies on the next assignment instead.
                if (spectateSession.IsFrozen)
                    return;

                rebuildLayout(Math.Clamp(count.NewValue, 1, 4));
                spectateSession.Assign();
            }, true);

            spectateSession.HasTeamScores.BindValueChanged(_ => Schedule(() =>
            {
                if (State.Value == TourneyState.Playing && spectateSession.HasTeamScores.Value)
                    scoreDisplay.FadeIn(100);
                else
                    scoreDisplay.FadeOut(100);
            }));

            // Initial assignment in case we are already in a room.
            spectateSession.Assign();
        }

        /// <summary>
        /// Rebuilds the side grids for the given players-per-team count.
        /// Only called while unfrozen, so no live gameplay is ever destroyed.
        /// </summary>
        private void rebuildLayout(int count)
        {
            playOuter.Height = 512;
            playOuter.Width = LadderInfo.ChromaKeyWidth.Value;

            redCells = buildSide(redSide, count);
            blueCells = buildSide(blueSide, count);

            spectateSession.SetCells(redCells, blueCells);
        }

        private const double cell_fade_out_duration = 150;

        private List<SpectateCell> buildSide(Container side, int count)
        {
            // Fade out the previous layout quickly instead of removing it instantly.
            // Expire() waits for the fade to finish; the new content is added immediately,
            // so rebuilds crossfade instead of popping.
            for (int i = side.Children.Count - 1; i >= 0; i--)
            {
                var child = side.Children[i];
                child.FadeOut(cell_fade_out_duration, Easing.OutQuint);
                child.Expire();
            }

            var cells = new List<SpectateCell>();

            SpectateCell makeCell()
            {
                var cell = new SpectateCell(backgroundLoader);
                cells.Add(cell);
                return cell;
            }

            Drawable content;

            switch (count)
            {
                case 1:
                    // Single cell filling the whole side.
                    side.Add(makeCell());
                    return cells;

                case 3:
                    // Pyramid: each side has two equal rows; the bottom row holds two cells
                    // filling its width, the top row holds a single same-sized cell centred in the side.
                    // Note: equal splits use default Distributed dimensions.
                    // GridSizeMode.Relative would give every row/column 100% and push content off-screen.
                    var top = makeCell();
                    top.Width = 0.5f;
                    top.Anchor = Anchor.Centre;
                    top.Origin = Anchor.Centre;
                    var bottom = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        RowDimensions = new[] { new Dimension() },
                        ColumnDimensions = new[] { new Dimension(), new Dimension() },
                        Content = new[] { new Drawable[] { makeCell(), makeCell() } },
                    };

                    content = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        RowDimensions = new[] { new Dimension(), new Dimension() },
                        ColumnDimensions = new[] { new Dimension() },
                        Content = new Drawable[][]
                        {
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Child = top,
                                }
                            },
                            new Drawable[] { bottom },
                        },
                    };
                    break;

                case 2:
                    content = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        RowDimensions = new[] { new Dimension(), new Dimension() },
                        ColumnDimensions = new[] { new Dimension() },
                        Content = new Drawable[][]
                        {
                            new Drawable[] { makeCell() },
                            new Drawable[] { makeCell() },
                        },
                    };
                    break;

                default:
                    // 2x2 grid (also covers unexpected counts).
                    content = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        RowDimensions = new[] { new Dimension(), new Dimension() },
                        ColumnDimensions = new[] { new Dimension(), new Dimension() },
                        Content = new Drawable[][]
                        {
                            new Drawable[] { makeCell(), makeCell() },
                            new Drawable[] { makeCell(), makeCell() },
                        },
                    };
                    break;
            }

            side.Add(content);
            return cells;
        }

        protected override void CurrentMatchChanged(ValueChangedEvent<TournamentMatch?> match)
        {
            base.CurrentMatchChanged(match);

            if (match.NewValue == null)
                return;

            warmup.Value = match.NewValue.Team1Score.Value + match.NewValue.Team2Score.Value == 0;
            scheduledScreenChange?.Cancel();
        }

        private ScheduledDelegate? scheduledScreenChange;
        private ScheduledDelegate? scheduledContract;

        private TournamentMatchScoreDisplay scoreDisplay = null!;

        private TourneyState lastState;
        private MatchHeader header = null!;

        private void contract()
        {
            if (!IsLoaded)
                return;

            scheduledContract?.Cancel();

            SongBar.Expanded = false;
            scoreDisplay.FadeOut(100);
            using (chat.BeginDelayedSequence(500))
                chat.Expand();
        }

        private void expand()
        {
            if (!IsLoaded)
                return;

            scheduledContract?.Cancel();

            chat.Contract();

            using (BeginDelayedSequence(300))
            {
                // The live score bar only shows for team rooms (see HasTeamScores).
                if (spectateSession.HasTeamScores.Value)
                    scoreDisplay.FadeIn(100);
                else
                    scoreDisplay.FadeOut(100);

                SongBar.Expanded = true;
            }
        }

        private void updateState()
        {
            try
            {
                scheduledScreenChange?.Cancel();

                if (State.Value == TourneyState.Ranking)
                {
                    if (warmup.Value || CurrentMatch.Value == null) return;

                    // TODO: wire score increment from online match results (phase B)
                }

                switch (State.Value)
                {
                    case TourneyState.Idle:
                        contract();

                        if (LadderInfo.AutoProgressScreens.Value)
                        {
                            const float delay_before_progression = 4000;

                            // if we've returned to idle and the last screen was ranking
                            // we should automatically proceed after a short delay
                            if (lastState == TourneyState.Ranking && !warmup.Value)
                            {
                                if (CurrentMatch.Value?.Completed.Value == true)
                                    scheduledScreenChange = Scheduler.AddDelayed(() => { sceneManager?.SetScreen(typeof(TeamWinScreen)); }, delay_before_progression);
                                else if (CurrentMatch.Value?.Completed.Value == false)
                                    scheduledScreenChange = Scheduler.AddDelayed(() => { sceneManager?.SetScreen(typeof(MapPoolScreen)); }, delay_before_progression);
                            }
                        }

                        break;

                    case TourneyState.Ranking:
                        scheduledContract = Scheduler.AddDelayed(contract, 10000);
                        break;

                    default:
                        expand();
                        break;
                }
            }
            finally
            {
                lastState = State.Value;
            }
        }

        public override void Hide()
        {
            scheduledScreenChange?.Cancel();
            base.Hide();
        }

        public override void Show()
        {
            updateState();
            base.Show();
        }
    }
}
