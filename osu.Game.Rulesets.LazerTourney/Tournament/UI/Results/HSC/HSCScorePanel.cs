// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Ranking.Expanded;
using osu.Game.Screens.Ranking.Expanded.Accuracy;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.UI.Results.HSC
{
    /// <summary>
    /// Hello! Shanden Cup (by [SHK]T_T) score panel.
    /// A single-player card laid out on a fixed 480x360 design canvas, mirrorable for red (left)
    /// and blue (right) spectate cells. The card deliberately draws no background; the parent
    /// <see cref="osu.Game.Screens.Ranking.ResultsScreen"/> is responsible for that.
    /// </summary>
    public partial class HSCScorePanel : CompositeDrawable
    {
        // All layout below is in this fixed design space. The whole canvas is uniformly scaled
        // and centred inside the cell, so 4:3 cells fit exactly and other aspect ratios letterbox
        // without overflowing.
        private const float design_width = 480;
        private const float design_height = 360;

        private static readonly Color4 card_colour = Color4Extensions.FromHex("#383838");
        private static readonly Color4 secondary_text_colour = Color4Extensions.FromHex("#AAAAAA");
        private static readonly Color4 dim_text_colour = Color4Extensions.FromHex("#5A5A5A");
        private static readonly ColourInfo perfect_colour = ColourInfo.GradientVertical(Color4Extensions.FromHex("#66FFCC"), Color4Extensions.FromHex("#FF9AD7"));
        private static readonly Color4 difficulty_text_colour = Color4Extensions.FromHex("#D0D0D0");
        private static readonly Color4 ring_background_colour = Color4Extensions.FromHex("#212121");

        private const float header_avatar_size = 67;
        private const float header_avatar_y = 10;
        private const float ribbon_y = 38;
        private const float ribbon_height = 39;
        private const float ribbon_outer_x = 84;
        private const float ribbon_width = 246;

        private const float stats_card_x = 27;
        private const float stats_card_y = 115;
        private const float stats_card_width = 201;
        private const float stats_card_height = 97;

        private const float combo_card_x = 27;
        private const float combo_card_y = 228;
        private const float combo_card_width = 198;
        private const float combo_card_height = 44;

        private const float backplate_x = 350;
        private const float backplate_y = 115;
        private const float backplate_width = 104;
        private const float backplate_height = 157;

        private const float ring_size = 210;
        private const float ring_x = 197;
        private const float ring_y = 88;

        private const float pill_y = 281;
        private const float pill_height = 15;
        private const float perfect_pill_x = 172;
        private const float perfect_pill_width = 72;
        private const float placement_pill_x = 354;
        private const float placement_pill_width = 52;

        private const float star_pill_width = 44;
        private const float star_difficulty_overlap = 9;
        private const float bottom_row_y = 334;
        private const float bottom_row_height = 15;

        // The bottom star + difficulty group may never be wider than the middle section
        // (the stats card through to the ring backplate).
        private const float bottom_group_max_width = 420;
        private const float difficulty_pill_padding = 8;

        private const float mods_outer_margin = 7;

        private readonly ScoreInfo score;
        private readonly TeamColour teamColour;
        private readonly int? placement;
        private readonly DateTimeOffset playedAt;

        private Container canvas = null!;
        private Container starPill = null!;
        private StarRatingDisplay starRatingDisplay = null!;
        private Container difficultyPill = null!;
        private Container difficultyTextHost = null!;
        private MarqueeContainer difficultyMarquee = null!;
        private OsuSpriteText difficultyMeasure = null!;

        private bool isBlue => teamColour == TeamColour.Blue;

        /// <summary>
        /// The current score always comes first. This is intentionally not mirrored.
        /// </summary>
        private int maximumAchievableCombo => score.GetMaximumAchievableCombo();

        public HSCScorePanel(ScoreInfo score, TeamColour teamColour = TeamColour.Red, int? placement = null)
        {
            this.score = score;
            this.teamColour = teamColour;
            this.placement = placement;

            // Spectated scores are constructed in-memory when gameplay starts and may not carry
            // a timestamp; fall back to the moment the results are shown instead of year 1.
            playedAt = score.Date == default ? DateTimeOffset.Now : score.Date;

            RelativeSizeAxes = Axes.Both;
        }

        [Resolved(canBeNull: true)]
        private LadderInfo? ladderInfo { get; set; }

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(RealmAccess realmAccess, BeatmapDifficultyCache beatmapDifficultyCache, OsuColour colours)
        {
            var beatmap = score.BeatmapInfo!;
            var metadata = beatmap.BeatmapSet?.Metadata ?? beatmap.Metadata;
            string mapper = metadata.Author.Username;

            StarDifficulty starDifficulty = new StarDifficulty(beatmap.StarRating, 0);

            // Prefer the locally-computed mod-adjusted difficulty when the beatmap is available.
            if (realmAccess.Run(r => r.Find<BeatmapInfo>(beatmap.ID)) != null)
                starDifficulty = beatmapDifficultyCache.GetDifficultyAsync(beatmap, score.Ruleset, score.Mods).GetResultSafely() ?? starDifficulty;

            Color4 ribbonColour = TournamentColours.GetTeamColour(teamColour, ladderInfo).TryExtractSingleColour(out SRGBColour single)
                ? single
                : Color4.White;

            List<HitResultDisplayStatistic> statistics = score.GetStatisticsForDisplay().Where(s => s.Result <= HitResult.Perfect).ToList();

            InternalChild = canvas = new Container
            {
                Name = "HSC design canvas",
                Size = new Vector2(design_width, design_height),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Children = new Drawable[]
                {
                    // Grey backplate behind the ring. The ring is drawn on top, leaving a crescent
                    // visible on the inner side of the cell.
                    createRoundedBox(backplate_x - 20, backplate_y, backplate_width + 20, backplate_height, 50, card_colour),

                    // Judgement and combo/PP cards. Both are rounded trapezoids in the reference:
                    // the judgement card's outer edge slopes top-inward/bottom-outward, while the
                    // combo/PP card's outer edge slopes top-outward/bottom-inward.
                    createTrapezoidCard(stats_card_x + 8, stats_card_y, stats_card_width + 30, stats_card_height, 12, 0.13f, card_colour),
                    createTrapezoidCard(combo_card_x - 3, combo_card_y, combo_card_width + 20, combo_card_height, 6, -0.28f, card_colour),

                    createStatistics(statistics, colours),
                    createComboAndPerformanceCard(),

                    // Ring and mods.
                    createRing(),
                    createMods(),

                    // Header (ribbon below the avatar and title).
                    createRibbon(ribbonColour),
                    createAvatar(),
                    createHeaderText(),

                    // Pills.
                    createSectionPill(perfect_pill_x, perfect_pill_width, "PERFECT", score.IsPerfect() ? perfect_colour : dim_text_colour),
                    createPlacementPill(),

                    // Bottom beatmap information (never mirrored).
                    createBeatmapTitle(metadata),
                    createMapperName(mapper),
                    // The difficulty pill sits below the star pill, which overlaps its left end.
                    createDifficultyPill(beatmap.DifficultyName),
                    createStarPill(starDifficulty),
                    createDifficultyMeasurement(beatmap.DifficultyName),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // The difficulty pill grows with its text, up to the middle-section width. Beyond
            // that the text scrolls inside the pill instead of overflowing the canvas.
            ScheduleAfterChildren(updateDifficultyPillLayout);
        }

        protected override void Update()
        {
            base.Update();

            if (canvas == null || DrawWidth <= 0 || DrawHeight <= 0)
                return;

            float scale = Math.Min(DrawWidth / design_width, DrawHeight / design_height);

            if (!Precision.AlmostEquals(canvas.Scale.X, scale))
                canvas.Scale = new Vector2(scale);
        }

        private float mirrorX(float x, float width) => isBlue ? design_width - x - width : x;

        private Container createRoundedBox(float x, float y, float width, float height, float radius, Color4 colour)
        {
            return new Container
            {
                Position = new Vector2(mirrorX(x, width), y),
                Size = new Vector2(width, height),
                Masking = true,
                CornerRadius = radius,
                CornerExponent = 2,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colour,
                },
            };
        }

        /// <summary>
        /// Creates a card whose outer edge is slanted. Shearing around the top-left corner keeps
        /// the top edge at <paramref name="x"/> and moves the bottom edge by <c>shear * height</c>;
        /// the sign is flipped on the blue side so the mirrored card keeps the same silhouette.
        /// </summary>
        private Container createTrapezoidCard(float x, float y, float width, float height, float radius, float shear, Color4 colour)
        {
            return new Container
            {
                Position = new Vector2(mirrorX(x, width), y),
                Size = new Vector2(width, height),
                Shear = new Vector2(isBlue ? -shear : shear, 0),
                Masking = true,
                CornerRadius = radius,
                CornerExponent = 2,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colour,
                },
            };
        }

        private Drawable createStatistics(List<HitResultDisplayStatistic> statistics, OsuColour colours)
        {
            int count = Math.Max(statistics.Count, 1);
            int columns = (count + 1) / 2;

            // Keep the judgement content clear of the ring, which overlaps the inner end of the card.
            const float content_width = 165;
            float contentX = isBlue ? mirrorX(stats_card_x, stats_card_width) + 34 : stats_card_x + 5;

            float labelSize = columns >= 3 ? 9 : 11;
            float valueSize = columns >= 3 ? 18 : 22;

            Drawable createCell(int index)
            {
                if (index >= statistics.Count)
                    return new Container();

                var stat = statistics[index];
                Color4 colour = colours.ForHitResult(stat.Result);

                return new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = stat.DisplayName.ToString().ToUpperInvariant(),
                            Font = OsuFont.Torus.With(size: labelSize, weight: FontWeight.Bold),
                            Colour = colour,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = stat.Count.ToString("N0", CultureInfo.InvariantCulture),
                            Font = OsuFont.Torus.With(size: valueSize, weight: FontWeight.Bold),
                            Colour = colour.Lighten(0.15f),
                        },
                    },
                };
            }

            return new GridContainer
            {
                Position = new Vector2(contentX, stats_card_y),
                Size = new Vector2(content_width, stats_card_height),
                ColumnDimensions = Enumerable.Range(0, columns)
                                           .Select(_ => new Dimension(GridSizeMode.Relative, 1f / columns))
                                           .ToArray(),
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.Relative, 0.5f),
                    new Dimension(GridSizeMode.Relative, 0.5f),
                },
                Content = new[]
                {
                    Enumerable.Range(0, columns).Select(createCell).ToArray(),
                    Enumerable.Range(columns, columns).Select(createCell).ToArray(),
                },
            };
        }

        private Drawable createComboAndPerformanceCard()
        {
            // PP always hugs the ring; Max Combo stays on the outer side.
            float comboCentre = isBlue ? 390 : 90;
            float performanceCentre = isBlue ? 300 : 180;

            return new Container
            {
                Children = new Drawable[]
                {
                    createSmallCardEntry(comboCentre, "Max Combo", createComboValue()),
                    createSmallCardEntry(performanceCentre, "PP", createPerformanceValue()),
                },
            };
        }

        private Drawable createSmallCardEntry(float centreX, string label, Drawable value)
        {
            return new Container
            {
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopCentre,
                        Position = new Vector2(centreX, combo_card_y + 7),
                        Text = label,
                        Font = OsuFont.Torus.With(size: 10, weight: FontWeight.Bold),
                        Colour = secondary_text_colour,
                    },
                    value.With(d =>
                    {
                        d.Anchor = Anchor.TopLeft;
                        d.Origin = Anchor.TopCentre;
                        d.Position = new Vector2(centreX, combo_card_y + 18);
                    }),
                },
            };
        }

        private Drawable createComboValue()
        {
            return new OsuTextFlowContainer(t => t.Font = OsuFont.Torus.With(size: 18, weight: FontWeight.Bold))
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
            }.With(t =>
            {
                t.AddText($"{score.MaxCombo}x");
                t.AddText($" / {maximumAchievableCombo}x", s =>
                {
                    s.Font = s.Font.With(size: 9);
                    s.Colour = secondary_text_colour;
                });
            });
        }

        private Drawable createPerformanceValue()
        {
            // return new OsuSpriteText
            // {
            //     Text = score.PP.HasValue ? score.PP.Value.ToString("0", CultureInfo.InvariantCulture) : "-",
            //     Font = OsuFont.Torus.With(size: 14, weight: FontWeight.Bold),
            //     Colour = Color4.White,
            // };
            return new PerformanceText(score);
        }

        private partial class PerformanceText(ScoreInfo score) : OsuSpriteText
        {
            private void setPPValue(double? pp)
            {
                if (pp.HasValue)
                    Text = ((int)Math.Round(pp.Value, MidpointRounding.AwayFromZero)).ToString("0", CultureInfo.InvariantCulture);
            }

            [BackgroundDependencyLoader]
            private void load(BeatmapDifficultyCache difficultyCache, CancellationToken? cancellationToken)
            {
                Text = "-";
                Font = OsuFont.Torus.With(size: 18, weight: FontWeight.Bold);
                Colour = Color4.White;

                if (score.PP.HasValue)
                {
                    setPPValue(score.PP.Value);
                }
                else
                {
                    Task.Run(async () =>
                    {
                        var attributes = await difficultyCache.GetDifficultyAsync(score.BeatmapInfo!, score.Ruleset, score.Mods, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
                        var performanceCalculator = score.Ruleset.CreateInstance().CreatePerformanceCalculator();

                        // Performance calculation requires the beatmap and ruleset to be locally available. If not, return a default value.
                        if (attributes?.DifficultyAttributes == null || performanceCalculator == null)
                            return;

                        var result = await performanceCalculator.CalculateAsync(score, attributes.Value.DifficultyAttributes, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);

                        Schedule(() => setPPValue(result.Total));
                    }, cancellationToken ?? CancellationToken.None);
                }
            }
        }

        private void updatePerformanceValue()
        {

        }

        private Drawable createRing()
        {
            return new Container
            {
                Position = new Vector2(mirrorX(ring_x, ring_size), ring_y),
                Size = new Vector2(ring_size),
                Children = new Drawable[]
                {
                    // Stock AccuracyCircle leaves its centre transparent on the results screen.
                    // The tournament card needs an opaque dark disc there so the card/backplate
                    // behind it does not show through. The disc stops short of the inner graded
                    // ring (radius ~77 at this size) so it never covers the rank colours.
                    new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(150),
                        Colour = ring_background_colour,
                    },
                    new HSCAccuracyCircle(score, true)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                },
            };
        }

        public partial class HSCAccuracyCircle(ScoreInfo score, bool withFlair = false) : AccuracyCircle(score, withFlair)
        {
            protected override void LoadComplete()
            {
                base.LoadComplete();
                // remove rank badges
                this.ChildrenOfType<Container<RankBadge>>().FirstOrDefault()?.Expire();
            }
        }

        private Drawable createMods()
        {
            if (score.Mods.Length == 0)
                return new Container();

            // Outside the ring circle but inside the grey backplate, mirrored to the inner side.
            float modsCentreX = isBlue
                ? mirrorX(ring_x, ring_size) - mods_outer_margin - 17
                : ring_x + ring_size + mods_outer_margin + 17;

            return new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(modsCentreX, ring_y + ring_size / 2),
                AutoSizeAxes = Axes.Both,
                Child = new ModDisplay(showExtendedInformation: false)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Scale = new Vector2(0.6f),
                    FillDirection = FillDirection.Vertical,
                    ExpansionMode = ExpansionMode.AlwaysExpanded,
                    Current = { Value = score.Mods },
                },
            };
        }

        private Drawable createRibbon(Color4 ribbonColour)
        {
            return new Container
            {
                Position = new Vector2(mirrorX(ribbon_outer_x, ribbon_width), ribbon_y),
                Size = new Vector2(ribbon_width, ribbon_height),
                Masking = true,
                CornerRadius = 4,
                CornerExponent = 2,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ribbonColour,
                    },
                    new OsuSpriteText
                    {
                        Anchor = isBlue ? Anchor.TopRight : Anchor.TopLeft,
                        Origin = isBlue ? Anchor.TopRight : Anchor.TopLeft,
                        // Position = new Vector2(isBlue ? ribbon_width - 14 : 16, 6),
                        Position = new Vector2(isBlue ? -16 : 16, 6),
                        Text = "Accuracy",
                        Font = OsuFont.Torus.With(size: 10, weight: FontWeight.Bold),
                        Colour = Color4.White,
                    },
                    new OsuSpriteText
                    {
                        Anchor = isBlue ? Anchor.TopRight : Anchor.TopLeft,
                        Origin = isBlue ? Anchor.TopRight : Anchor.TopLeft,
                        // Position = new Vector2(isBlue ? ribbon_width - 14 : 16, 16),
                        Position = new Vector2(isBlue ? -16 : 16, 16),
                        Text = score.Accuracy.ToString("P2", CultureInfo.InvariantCulture),
                        Font = OsuFont.Torus.With(size: 18, weight: FontWeight.Bold),
                        Colour = Color4.White,
                    },
                    new Container
                    {
                        Anchor = isBlue ? Anchor.CentreLeft : Anchor.CentreRight,
                        Origin = isBlue ? Anchor.CentreLeft : Anchor.CentreRight,
                        RelativeSizeAxes = Axes.Y,
                        Margin = new MarginPadding { Horizontal = 4, Top = -4 },
                        Width = 174,
                        Child = new HSCTotalScoreCounter(isBlue)
                        {
                            Anchor = isBlue ? Anchor.CentreLeft : Anchor.CentreRight,
                            Origin = isBlue ? Anchor.CentreLeft : Anchor.CentreRight,
                            AlwaysPresent = true,
                            Current = scoreManager.GetBindableTotalScore(score),
                        }
                    },
                    /*new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        // Centred on the design canvas (not the ribbon), so the accuracy block
                        // gets the outer end and the score stays visually central on both sides.
                        Position = new Vector2(isBlue ? ribbon_width - 156 : 156, 2),
                        Text = score.TotalScore.ToString("N0", CultureInfo.InvariantCulture),
                        Font = OsuFont.Torus.With(size: 30, weight: FontWeight.Bold),
                        Colour = Color4.White,
                        Shadow = true,
                        ShadowColour = Color4.Black.Opacity(0.45f),
                    },*/
                },
            };
        }

        public partial class HSCTotalScoreCounter(bool isBlue, bool playSamples = false) : TotalScoreCounter(playSamples)
        {
            protected override OsuSpriteText CreateSpriteText() => base.CreateSpriteText().With(s =>
            {
                s.Anchor = isBlue ? Anchor.CentreLeft : Anchor.CentreRight;
                s.Origin = isBlue ? Anchor.CentreLeft : Anchor.CentreRight;
                s.Font = OsuFont.TorusAlternate.With(size: 38, weight: FontWeight.Bold, fixedWidth: true);
                s.Spacing = new Vector2(-2, 0);
                s.Colour = Color4.White;
                s.Shadow = true;
                s.ShadowColour = Color4.Black.Opacity(0.45f);
            });
        }

        private Drawable createAvatar()
        {
            return new Container
            {
                Position = new Vector2(mirrorX(design_width - header_avatar_size - 82, header_avatar_size), header_avatar_y),
                Size = new Vector2(header_avatar_size),
                Masking = true,
                CornerRadius = 8,
                CornerExponent = 2,
                BorderThickness = 1,
                BorderColour = Color4Extensions.FromHex("#E6E6E6"),
                Padding = new MarginPadding(1),
                Child = new UpdateableAvatar(score.User)
                {
                    RelativeSizeAxes = Axes.Both,
                },
            };
        }

        private Drawable createHeaderText()
        {
            const float region_width = 240;
            float avatarX = mirrorX(design_width - header_avatar_size - 82, header_avatar_size);
            float titleRight = avatarX - 3;

            return new Container
            {
                Children = new Drawable[]
                {
                    new TruncatingSpriteText
                    {
                        Anchor = isBlue ? Anchor.TopLeft : Anchor.TopRight,
                        Origin = isBlue ? Anchor.TopLeft : Anchor.TopRight,
                        Position = new Vector2(isBlue ? avatarX + header_avatar_size + 3 : titleRight, 11),
                        MaxWidth = region_width,
                        Text = $"{score.User.Username}'s Ranking",
                        Font = OsuFont.Torus.With(size: 15, weight: FontWeight.Bold),
                        Colour = Color4.White,
                    },
                    new OsuSpriteText
                    {
                        Anchor = isBlue ? Anchor.TopLeft : Anchor.TopRight,
                        Origin = isBlue ? Anchor.TopLeft : Anchor.TopRight,
                        Position = new Vector2(isBlue ? avatarX + header_avatar_size + 3 : titleRight, 27),
                        Text = $"Played on {playedAt.ToString("yyyy/M/d HH:mm:ss zzz", CultureInfo.InvariantCulture)}",
                        Font = OsuFont.Torus.With(size: 8, weight: FontWeight.Regular),
                        Colour = Color4.White,
                    },
                },
            };
        }

        private Drawable createSectionPill(float x, float width, string text, ColourInfo textColour)
        {
            return new Container
            {
                Position = new Vector2(mirrorX(x, width), pill_y),
                Size = new Vector2(width, pill_height),
                Masking = true,
                CornerRadius = pill_height / 2,
                CornerExponent = 2,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = card_colour,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = text,
                        Font = OsuFont.Torus.With(size: 10, weight: FontWeight.Bold),
                        Colour = textColour,
                    },
                },
            };
        }

        private Drawable createPlacementPill()
        {
            if (placement == null)
                return new Container();

            return createSectionPill(placement_pill_x, placement_pill_width, ordinal(placement.Value), Color4.White);
        }

        private static string ordinal(int value)
        {
            int lastTwo = value % 100;

            if (lastTwo is >= 11 and <= 13)
                return $"{value}th";

            return (value % 10) switch
            {
                1 => $"{value}st",
                2 => $"{value}nd",
                3 => $"{value}rd",
                _ => $"{value}th",
            };
        }

        private Drawable createBeatmapTitle(IBeatmapMetadataInfo metadata)
        {
            // The marquee itself does not clip; the wrapping container keeps long titles from
            // spilling out of the fixed design canvas while they scroll.
            return new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 300),
                Size = new Vector2(440, 20),
                Masking = true,
                Child = new MarqueeContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    InitialMoveDelay = 1200,
                    NonOverflowingContentAnchor = Anchor.TopCentre,
                    CreateContent = () => new OsuSpriteText
                    {
                        Text = new RomanisableString(metadata.TitleUnicode, metadata.Title),
                        Font = OsuFont.Torus.With(size: 13, weight: FontWeight.Bold),
                        Colour = Color4.White,
                        Shadow = true,
                        ShadowColour = Color4.Black.Opacity(0.45f),
                    },
                },
            };
        }

        private Drawable createMapperName(string mapper)
        {
            return new TruncatingSpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 320),
                MaxWidth = 440,
                Text = mapper,
                Font = OsuFont.Torus.With(size: 9, weight: FontWeight.Regular),
                Colour = secondary_text_colour,
                ShowTooltip = false,
            };
        }

        private Drawable createStarPill(StarDifficulty starDifficulty)
        {
            // StarRatingDisplay draws the pill, star icon, rating text and difficulty colour
            // itself. The wrapper only reserves the layout slot; the display is scaled to fit
            // it once its auto-size is known (see updateDifficultyPillLayout).
            return starPill = new Container
            {
                Position = new Vector2(0, bottom_row_y),
                Size = new Vector2(star_pill_width, bottom_row_height),
                Child = starRatingDisplay = new StarRatingDisplay(starDifficulty, StarRatingDisplaySize.Small)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
            };
        }

        private Drawable createDifficultyMeasurement(string difficultyName)
        {
            return difficultyMeasure = new OsuSpriteText
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Position = new Vector2(-1000, 0),
                Text = difficultyName,
                Font = OsuFont.Torus.With(size: 10, weight: FontWeight.Bold),
                AlwaysPresent = true,
                Alpha = 0,
            };
        }

        private Drawable createDifficultyPill(string difficultyName)
        {
            difficultyPill = new Container
            {
                Position = new Vector2(0, bottom_row_y),
                Size = new Vector2(0, bottom_row_height),
                Masking = true,
                CornerRadius = bottom_row_height / 2,
                CornerExponent = 2,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = card_colour,
                    },
                    difficultyTextHost = new Container
                    {
                        Position = new Vector2(difficulty_pill_padding / 2, 0), // centre
                        Size = new Vector2(0, bottom_row_height),
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Child = difficultyMarquee = new MarqueeContainer
                        {
                            RelativeSizeAxes = Axes.None,
                            Width = 0,
                            InitialMoveDelay = 1200,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            NonOverflowingContentAnchor = Anchor.Centre,
                            CreateContent = () => new OsuSpriteText
                            {
                                Text = difficultyName,
                                Font = OsuFont.Torus.With(size: 10, weight: FontWeight.Bold),
                                Colour = difficulty_text_colour,
                            },
                        },
                    },
                },
            };

            return difficultyPill;
        }

        private void updateDifficultyPillLayout()
        {
            if (difficultyPill == null || !difficultyPill.IsLoaded)
                return;

            // Fit the stock StarRatingDisplay into the fixed star pill slot.
            if (starRatingDisplay.Size.X > 0 && starRatingDisplay.Size.Y > 0)
            {
                float starScale = Math.Min(star_pill_width / starRatingDisplay.Size.X, bottom_row_height / starRatingDisplay.Size.Y);
                starRatingDisplay.Scale = new Vector2(starScale);
            }

            float textWidth = difficultyMeasure.DrawWidth;
            float maxPillWidth = bottom_group_max_width - star_pill_width + star_difficulty_overlap;
            float pillWidth = Math.Min(textWidth + difficulty_pill_padding * 2 + 2, maxPillWidth); // +2 (?)
            float totalWidth = star_pill_width + pillWidth - star_difficulty_overlap;
            float startX = design_width / 2 - totalWidth / 2;

            starPill.Position = new Vector2(startX, bottom_row_y);
            difficultyPill.Position = new Vector2(startX + star_pill_width - star_difficulty_overlap, bottom_row_y);
            difficultyPill.Width = pillWidth;
            difficultyTextHost.Width = Math.Max(0, pillWidth - difficulty_pill_padding * 2);
            difficultyMarquee.Width = Math.Max(0, pillWidth - difficulty_pill_padding * 2);
        }
    }
}
