// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Expanded;
using osu.Game.Screens.Ranking.Expanded.Accuracy;
using osu.Game.Screens.Ranking.Expanded.Statistics;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;
using osu.Game.Screens.Play.HUD;
using osu.Framework.Testing;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Rulesets.UI;
using Microsoft.Toolkit.HighPerformance;
using osu.Framework.Graphics.Shapes;
using osu.Game.Users;
using osu.Framework.Graphics.Colour;
using osu.Framework.Extensions.Color4Extensions;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// Tournament score panel: static two-row layout, no popups and no animation.
    /// Top row shows the player and the song; bottom row shows the accuracy circle
    /// next to the detailed results. Content pieces are the official
    /// contracted/expanded panel contents, reused verbatim.
    /// </summary>
    public partial class TournamentScorePanel : CompositeDrawable
    {
        private static readonly ColourInfo expanded_top_layer_colour = ColourInfo.GradientVertical(Color4Extensions.FromHex("#444"), Color4Extensions.FromHex("#333"));
        private static readonly ColourInfo expanded_middle_layer_colour = ColourInfo.GradientVertical(Color4Extensions.FromHex("#555"), Color4Extensions.FromHex("#333"));
        public const double RESIZE_DURATION = 200;

        private static readonly MarginPadding card_padding = new MarginPadding()
        {
            Vertical = 10,
            Horizontal = 40,
        };


        public readonly ScoreInfo Score;

        private readonly bool withFlair;

        private readonly List<StatisticDisplay> statisticDisplays = [];
        private List<StatisticDisplay> topStatistics = null!;
        private List<HitResultStatistic> bottomStatistics = null!;

        private RollingCounter<long> scoreCounter = null!;

        private ModDisplay modDisplay = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        public TournamentScorePanel(ScoreInfo score)
        {
            Score = score;
            withFlair = !score.User.IsBot;

            RelativeSizeAxes = Axes.Both;
        }

        private Drawable topLayerBackground = null!;

        private Drawable middleLayerBackground = null!;

        [BackgroundDependencyLoader]
        private void load(RealmAccess realmAccess, BeatmapDifficultyCache beatmapDifficultyCache)
        {
            var beatmap = Score.BeatmapInfo!;
            var metadata = beatmap.BeatmapSet?.Metadata ?? beatmap.Metadata;
            string creator = metadata.Author.Username;

            StarDifficulty starDifficulty = new StarDifficulty(beatmap.StarRating, 0);

            // In some cases, the beatmap ferried through ScoreInfo actually represents an online beatmap.
            // If it isn't, we may be able to compute a more accurate difficulty from the ruleset and mods.
            if (realmAccess.Run(r => r.Find<BeatmapInfo>(Score.BeatmapInfo!.ID)) != null)
                starDifficulty = beatmapDifficultyCache.GetDifficultyAsync(Score.BeatmapInfo!, Score.Ruleset, Score.Mods).GetResultSafely() ?? starDifficulty;

            topStatistics = new List<StatisticDisplay>
            {
                new AccuracyStatistic(Score.Accuracy),
                new ComboStatistic(Score.MaxCombo, Score.GetMaximumAchievableCombo()),
                new PerformanceStatistic(Score),
            };

            bottomStatistics = new List<HitResultStatistic>();

            foreach (var result in Score.GetStatisticsForDisplay())
                bottomStatistics.Add(new HitResultStatistic(result));

            statisticDisplays.AddRange(topStatistics);
            statisticDisplays.AddRange(bottomStatistics);

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, 140),
                    new Dimension(),
                },
                Content = new[]
                {
                    // Top: player (left) and song (right).
                    new Drawable[]
                    {
                        new Container
                        {
                            Name = "Top layer",
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    CornerRadius = 20,
                                    CornerExponent = 2.5f,
                                    Masking = true,
                                    Child = topLayerBackground = new Box { RelativeSizeAxes = Axes.Both }
                                },
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Padding = card_padding,
                                    ColumnDimensions = new[]
                                    {
                                        new Dimension(GridSizeMode.Absolute, 220),
                                        new Dimension(),
                                    },
                                    Content = new[]
                                    {
                                        new Drawable[]
                                        {
                                            new Container
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Child = new FillFlowContainer
                                                {
                                                    AutoSizeAxes = Axes.Both,
                                                    Direction = FillDirection.Vertical,
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    Padding = new MarginPadding(10),
                                                    Children = new Drawable[]
                                                    {
                                                        new UpdateableAvatar(Score.User)
                                                        {
                                                            Anchor = Anchor.TopCentre,
                                                            Origin = Anchor.TopCentre,
                                                            Size = new Vector2(80),
                                                            Masking = true,
                                                            CornerRadius = 20,
                                                            CornerExponent = 2.5f,
                                                        },
                                                        new OsuSpriteText
                                                        {
                                                            Anchor = Anchor.TopCentre,
                                                            Origin = Anchor.TopCentre,
                                                            Text = Score.User.Username,
                                                            Font = OsuFont.Torus.With(size: 28, weight: FontWeight.SemiBold),
                                                        },
                                                    },
                                                },
                                            },
                                            new Container
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Child = new FillFlowContainer
                                                {
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    AutoSizeAxes = Axes.Both,
                                                    Direction = FillDirection.Horizontal,
                                                    Spacing = new Vector2(30, 0),
                                                    Children = new Drawable[]
                                                    {
                                                        new FillFlowContainer
                                                        {
                                                            Anchor = Anchor.Centre,
                                                            Origin = Anchor.Centre,
                                                            AutoSizeAxes = Axes.Both,
                                                            Direction = FillDirection.Vertical,
                                                            Children = new Drawable[]
                                                            {
                                                                new TruncatingSpriteText
                                                                {
                                                                    Anchor = Anchor.TopCentre,
                                                                    Origin = Anchor.TopCentre,
                                                                    Text = new RomanisableString(metadata.TitleUnicode, metadata.Title),
                                                                    Font = OsuFont.Torus.With(size: 28, weight: FontWeight.SemiBold),
                                                                    MaxWidth = 300,
                                                                },
                                                                new TruncatingSpriteText
                                                                {
                                                                    Anchor = Anchor.TopCentre,
                                                                    Origin = Anchor.TopCentre,
                                                                    Text = new RomanisableString(metadata.ArtistUnicode, metadata.Artist),
                                                                    Font = OsuFont.Torus.With(size: 22, weight: FontWeight.SemiBold),
                                                                    MaxWidth = 300,
                                                                },
                                                            },
                                                        },
                                                        new FillFlowContainer
                                                        {
                                                            Anchor = Anchor.Centre,
                                                            Origin = Anchor.Centre,
                                                            AutoSizeAxes = Axes.Both,
                                                            Direction = FillDirection.Vertical,
                                                            Spacing = new Vector2(0, 5),
                                                            Children = new Drawable[]
                                                            {
                                                                new FillFlowContainer
                                                                {
                                                                    Anchor = Anchor.TopCentre,
                                                                    Origin = Anchor.TopCentre,
                                                                    AutoSizeAxes = Axes.Both,
                                                                    Direction = FillDirection.Horizontal,
                                                                    Spacing = new Vector2(5, 0),
                                                                    Children = new Drawable[]
                                                                    {
                                                                        new StarRatingDisplay(starDifficulty)
                                                                        {
                                                                            Anchor = Anchor.CentreLeft,
                                                                            Origin = Anchor.CentreLeft,
                                                                        },
                                                                        new DifficultyIcon(beatmap, Score.Ruleset)
                                                                        {
                                                                            Anchor = Anchor.CentreLeft,
                                                                            Origin = Anchor.CentreLeft,
                                                                            Size = new Vector2(20),
                                                                            TooltipType = DifficultyIconTooltipType.Extended,
                                                                        },
                                                                        new TruncatingSpriteText
                                                                        {
                                                                            Anchor = Anchor.CentreLeft,
                                                                            Origin = Anchor.CentreLeft,
                                                                            Text = beatmap.DifficultyName,
                                                                            Font = OsuFont.Torus.With(size: 26, weight: FontWeight.SemiBold),
                                                                            MaxWidth = 150,
                                                                        },
                                                                        new OsuTextFlowContainer(s => s.Font = OsuFont.Torus.With(size: 14))
                                                                        {
                                                                            Anchor = Anchor.CentreLeft,
                                                                            Origin = Anchor.CentreLeft,
                                                                            AutoSizeAxes = Axes.Both,
                                                                            Direction = FillDirection.Horizontal,
                                                                        }.With(t =>
                                                                        {
                                                                            if (!string.IsNullOrEmpty(creator))
                                                                            {
                                                                                t.AddText(" by ");
                                                                                t.AddText(creator, s => s.Font = s.Font.With(size: 22,weight: FontWeight.SemiBold));
                                                                            }
                                                                        }),
                                                                    },
                                                                },
                                                                new PlayedOnText(Score.Date, true)
                                                                {
                                                                    Anchor = Anchor.TopCentre,
                                                                    Origin = Anchor.TopCentre,
                                                                    Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                                                                },
                                                            },
                                                        },
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },
                            }
                        },
                    },
                    // Middle: accuracy circle (left) and detailed results (right).
                    new Drawable[]
                    {
                        new Container
                        {
                            Name = "Middle layer",
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    CornerRadius = 20,
                                    CornerExponent = 2.5f,
                                    Masking = true,
                                    Children = new[]
                                    {
                                        middleLayerBackground = new Box { RelativeSizeAxes = Axes.Both },
                                        new UserCoverBackground
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            User = Score.User,
                                            Colour = ColourInfo.GradientVertical(Color4.White.Opacity(0.5f), Color4Extensions.FromHex("#444").Opacity(0))
                                        }
                                    }
                                },
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Padding = card_padding,
                                    ColumnDimensions = new[]
                                    {
                                        new Dimension(GridSizeMode.Absolute, 220),
                                        new Dimension(),
                                    },
                                    Content = new[]
                                    {
                                        new Drawable[]
                                        {
                                            new Container
                                            {
                                                Size = new Vector2(220),
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                Padding = new MarginPadding(10),
                                                Child = new AccuracyCircle(Score, withFlair)
                                                {
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    RelativeSizeAxes = Axes.Both,
                                                    FillMode = FillMode.Fit,
                                                },
                                            },
                                            new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                Direction = FillDirection.Vertical,
                                                Spacing = new Vector2(0, 8),
                                                Children = new Drawable[]
                                                {
                                                    new Container
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Child = new GridContainer
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 100,
                                                            Anchor = Anchor.CentreLeft,
                                                            Origin = Anchor.CentreLeft,
                                                            ColumnDimensions = new[]
                                                            {
                                                                new Dimension(GridSizeMode.Relative, 0.65f),
                                                                new Dimension(),
                                                            },
                                                            Content = new[]
                                                            {
                                                                new Drawable[]
                                                                {
                                                                    scoreCounter = new TournamentScoreCounter(!withFlair)
                                                                    {
                                                                        Anchor = Anchor.Centre,
                                                                        Origin = Anchor.Centre,
                                                                        Margin = new MarginPadding { Top = 0, Bottom = 5 },
                                                                        Current = { Value = 0 },
                                                                        Alpha = 0,
                                                                        AlwaysPresent = true
                                                                    },
                                                                    modDisplay = new ModDisplay
                                                                    {
                                                                        Anchor = Anchor.CentreRight,
                                                                        Origin = Anchor.CentreRight,
                                                                        Margin = new MarginPadding(5),
                                                                        ExpansionMode = ExpansionMode.AlwaysExpanded,
                                                                    },
                                                                }
                                                            }
                                                        }
                                                    },
                                                    new GridContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Content = new[] { topStatistics.Cast<Drawable>().ToArray() },
                                                        RowDimensions = new[]
                                                        {
                                                            new Dimension(GridSizeMode.AutoSize),
                                                        }
                                                    },
                                                    new GridContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Content = new[] { bottomStatistics.Where(s => s.Result <= HitResult.Perfect).ToArray() },
                                                        RowDimensions = new[]
                                                        {
                                                            new Dimension(GridSizeMode.AutoSize),
                                                        }
                                                    },
                                                    new GridContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Content = new[] { bottomStatistics.Where(s => s.Result > HitResult.Perfect).ToArray() },
                                                        RowDimensions = new[]
                                                        {
                                                            new Dimension(GridSizeMode.AutoSize),
                                                        }
                                                    }
                                                }
                                            },
                                        },
                                    },
                                },
                            }
                        }
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            topLayerBackground.FadeColour(expanded_top_layer_colour, RESIZE_DURATION, Easing.OutQuint);
            middleLayerBackground.FadeColour(expanded_middle_layer_colour, RESIZE_DURATION, Easing.OutQuint);

            // Score counter value setting must be scheduled so it isn't transferred instantaneously
            ScheduleAfterChildren(() =>
            {
                foreach (var s in statisticDisplays)
                {
                    // first OsuSpriteText: title
                    s.ChildrenOfType<CircularContainer>().FirstOrDefault()?.Height = 20;
                    s.ChildrenOfType<OsuSpriteText>().FirstOrDefault()?.Font = OsuFont.Torus.With(size: 20, weight: FontWeight.SemiBold);
                }
                var updateFontSize = (Drawable s, float size) =>
                {
                    bool first = true;
                    s.ChildrenOfType<OsuSpriteText>().ForEach(t =>
                    {
                        if (first)
                        {
                            first = false;
                            return;
                        }
                        if (t.Text == "PERFECT")
                            return;
                        t.Font = OsuFont.Torus.With(size: size, fixedWidth: true);
                        // t.Spacing = new Vector2(-4, 0);
                    });
                };
                foreach (var s in topStatistics)
                    updateFontSize(s, 36);
                foreach (var s in bottomStatistics)
                    updateFontSize(s, 24);

                modDisplay.ChildrenOfType<ModIcon>().ForEach(m =>
                {
                    m.Scale = Vector2.One;
                });

                using (BeginDelayedSequence(AccuracyCircle.ACCURACY_TRANSFORM_DELAY))
                {
                    scoreCounter.FadeIn();
                    scoreCounter.Current = scoreManager.GetBindableTotalScore(Score);

                    modDisplay.Current.Value = Score.Mods;

                    double delay = 0;

                    foreach (var stat in statisticDisplays)
                    {
                        using (BeginDelayedSequence(delay))
                            stat.Appear();

                        delay += 200;
                    }
                }

                if (!withFlair)
                    FinishTransforms(true);
            });
        }

        public partial class TournamentScoreCounter(bool playSamples = false) : TotalScoreCounter(playSamples)
        {
            protected override OsuSpriteText CreateSpriteText() => base.CreateSpriteText().With(s =>
            {
                s.Font = OsuFont.TorusAlternate.With(size: 90, weight: FontWeight.Bold, fixedWidth: true);
            });
        }
    }
}
