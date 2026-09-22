// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Menu;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    public partial class SongBar : CompositeDrawable
    {
        public const float HEIGHT = 145 / 2f;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        private IBeatmapInfo? beatmap;
        private RulesetInfo? ruleset;
        private IReadOnlyList<Mod> gameplayMods = Array.Empty<Mod>();

        private double starRating;
        private CancellationTokenSource? difficultyCancellation;

        private FillFlowContainer flow = null!;
        private FillFlowContainer statsLeft = null!;
        private FillFlowContainer statsRight = null!;
        private Container panelHolder = null!;

        /// <summary>
        /// Sets the displayed source. Star rating is recalculated live via
        /// <see cref="BeatmapDifficultyCache"/> (mirroring solo song select), and difficulty
        /// attributes are adjusted for <paramref name="mods"/>.
        /// </summary>
        /// <param name="beatmapInfo">The beatmap to display, or null for the placeholder.</param>
        /// <param name="rulesetInfo">The ruleset for attributes and mod conversion.</param>
        /// <param name="mods">Gameplay mods affecting star rating and attributes (room required mods).</param>
        /// <param name="requiredMods">Mods for the required-mods icon row (room required mods).</param>
        /// <param name="poolMatch">Full-equality mappool match for the category mod icon, if any.</param>
        /// <remarks>Both mod lists are room required mods per tournament decision.</remarks>
        public void SetSource(IBeatmapInfo? beatmapInfo, RulesetInfo? rulesetInfo, IReadOnlyList<Mod> mods, IReadOnlyList<Mod> requiredMods, RoundBeatmap? poolMatch)
        {
            beatmap = beatmapInfo;
            ruleset = rulesetInfo;
            gameplayMods = mods;

            if (panelHolder != null)
            {
                panelHolder.Child = new TournamentBeatmapPanel(beatmap, poolMatch?.Mods ?? string.Empty, poolMatch)
                {
                    RelativeSizeAxes = Axes.X,
                    Width = 1.0f,
                    Height = HEIGHT,
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                };

                if (panelHolder.Child is TournamentBeatmapPanel panel)
                    panel.SetRequiredMods(requiredMods.ToArray());
            }

            updateStarRating();
            updateStats();
        }

        public bool Expanded
        {
            get;
            set
            {
                field = value;
                flow.Direction = field ? FillDirection.Full : FillDirection.Vertical;
            }
        }

        // Todo: This is a hack for https://github.com/ppy/osu-framework/issues/3617 since this container is at the very edge of the screen and potentially initially masked away.
        protected override bool ComputeIsMaskedAway(RectangleF maskingBounds) => false;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            Masking = true;
            CornerRadius = 5;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    Colour = colours.Gray3,
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.4f,
                },
                flow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Full,
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = HEIGHT,
                            Width = 0.5f,
                            Anchor = Anchor.BottomRight,
                            Origin = Anchor.BottomRight,

                            Children = new Drawable[]
                            {
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.Both,

                                    Content = new[]
                                    {
                                        new Drawable[]
                                        {
                                            statsLeft = new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                Direction = FillDirection.Vertical,
                                            },
                                            statsRight = new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                Direction = FillDirection.Vertical,
                                            },
                                            new Container
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Children = new Drawable[]
                                                {
                                                    new Box
                                                    {
                                                        Colour = Color4.Black,
                                                        RelativeSizeAxes = Axes.Both,
                                                        Alpha = 0.1f,
                                                    },
                                                    new OsuLogo
                                                    {
                                                        Triangles = false,
                                                        Scale = new Vector2(0.08f),
                                                        Margin = new MarginPadding(50),
                                                        X = -10,
                                                        Anchor = Anchor.CentreRight,
                                                        Origin = Anchor.CentreRight,
                                                    },
                                                }
                                            },
                                        },
                                    }
                                }
                            }
                        },
                        panelHolder = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Width = 0.5f,
                            Height = HEIGHT,
                            Anchor = Anchor.BottomRight,
                            Origin = Anchor.BottomRight,
                        },
                    }
                }
            };

            Expanded = true;
            updateStats();
        }

        /// <summary>
        /// Recalculates the star rating for the current beatmap/ruleset/mods.
        /// Mirrors solo song select (BeatmapTitleWedge.DifficultyDisplay), which delegates
        /// to the difficulty cache and falls back to the stored rating when not downloaded.
        /// </summary>
        private void updateStarRating()
        {
            difficultyCancellation?.Cancel();
            var cancellation = difficultyCancellation = new CancellationTokenSource();

            var info = beatmap;
            var rulesetInfo = ruleset;
            Mod[] mods = gameplayMods.ToArray();

            if (info == null || rulesetInfo == null)
            {
                starRating = 0;
                updateStats();
                return;
            }

            difficultyCache.GetDifficultyAsync(info, rulesetInfo, mods).ContinueWith(task => Schedule(() =>
            {
                if (cancellation.IsCancellationRequested)
                    return;

                starRating = task.GetResultSafely()?.Stars ?? 0;
                updateStats();
            }));
        }

        private void updateStats()
        {
            if (flow == null)
                return;

            var info = beatmap ?? new BeatmapInfo
            {
                Metadata = new BeatmapMetadata
                {
                    Artist = "unknown",
                    Title = "no beatmap selected",
                    Author = new RealmUser { Username = "unknown" },
                },
                DifficultyName = "unknown",
                BeatmapSet = new BeatmapSetInfo(),
                StarRating = 0,
                Difficulty = new BeatmapDifficulty
                {
                    CircleSize = 0,
                    DrainRate = 0,
                    OverallDifficulty = 0,
                    ApproachRate = 0,
                },
            };

            var mods = gameplayMods.ToList();
            Ruleset? rulesetInstance = ruleset?.CreateInstance();
            var adjustedDifficulty = rulesetInstance == null ? info.Difficulty : rulesetInstance.GetAdjustedDisplayDifficulty(info, mods);

            double rate = ModUtils.CalculateRateWithMods(mods);
            double bpm = FormatUtils.RoundBPM(info.BPM, rate);
            double length = info.Length / rate;

            string srExtra = "";

            if (mods.Any(x => x is ModHardRock) || mods.Any(x => x is ModDoubleTime))
            {
                srExtra = "*";
            }

            (string heading, string content)[] stats;

            switch (ruleset?.OnlineID ?? 0)
            {
                default:
                    stats = new (string heading, string content)[]
                    {
                        ("CS", $"{adjustedDifficulty.CircleSize:0.#}"),
                        ("AR", $"{adjustedDifficulty.ApproachRate:0.#}"),
                        ("OD", $"{adjustedDifficulty.OverallDifficulty:0.#}"),
                    };
                    break;

                case 1:
                case 3:
                    stats = new (string heading, string content)[]
                    {
                        ("OD", $"{adjustedDifficulty.OverallDifficulty:0.#}"),
                        ("HP", $"{adjustedDifficulty.DrainRate:0.#}")
                    };
                    break;

                case 2:
                    stats = new (string heading, string content)[]
                    {
                        ("CS", $"{adjustedDifficulty.CircleSize:0.#}"),
                        ("AR", $"{adjustedDifficulty.ApproachRate:0.#}"),
                    };
                    break;
            }

            statsLeft.Children = new Drawable[]
            {
                new DiffPiece(stats),
                new DiffPiece(("Star Rating", $"{starRating.FormatStarRating()}{srExtra}"))
            };

            statsRight.Children = new Drawable[]
            {
                new DiffPiece(("Length", length.ToFormattedDuration().ToString())),
                new DiffPiece(("BPM", $"{bpm:0.#}")),
            };
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            difficultyCancellation?.Cancel();
        }

        public partial class DiffPiece : TextFlowContainer
        {
            public DiffPiece(params (string heading, string content)[] tuples)
            {
                Margin = new MarginPadding { Horizontal = 15, Vertical = 1 };
                AutoSizeAxes = Axes.Both;

                static void cp(SpriteText s, bool bold)
                {
                    s.Font = OsuFont.Torus.With(weight: bold ? FontWeight.Bold : FontWeight.Regular, size: 15);
                }

                for (int i = 0; i < tuples.Length; i++)
                {
                    (string heading, string content) = tuples[i];

                    if (i > 0)
                    {
                        AddText(" / ", s =>
                        {
                            cp(s, false);
                            s.Spacing = new Vector2(-2, 0);
                        });
                    }

                    AddText(new TournamentSpriteText { Text = heading }, s => cp(s, false));
                    AddText(" ", s => cp(s, false));
                    AddText(new TournamentSpriteText { Text = content }, s => cp(s, true));
                }
            }
        }
    }
}
