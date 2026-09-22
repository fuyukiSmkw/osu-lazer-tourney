// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.LazerTourney.Tournament.Components;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Editors.Components;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.OnlinePlay;
using osu.Game.Screens.Play.HUD;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Editors
{
    public partial class RoundEditorScreen : TournamentEditorScreen<RoundEditorScreen.RoundRow, TournamentRound>
    {
        protected override BindableList<TournamentRound> Storage => LadderInfo.Rounds;

        [Resolved]
        private LadderInfo ladder { get; set; } = null!;

        // Created eagerly (not in load()): the base editor screen constructs all rows inside ITS load(),
        // which runs before this class's load(). Rows created there already need non-null overlays.
        // The required picker is a UserModSelectOverlay (same as lazer song select):
        // newly added mods evict incompatible ones, so required mods can never conflict internally.
        private UserModSelectOverlay requiredOverlay = new UserModSelectOverlay();
        private FreeModSelectOverlay allowedOverlay = new FreeModSelectOverlay();

        [BackgroundDependencyLoader]
        private void load()
        {
            // Shared mod pickers (same as lazer room creation). Bound per edit session by RoundBeatmapModEditor.
            AddInternal(requiredOverlay);
            AddInternal(allowedOverlay);

            LadderInfo.Ruleset.BindValueChanged(ruleset =>
            {
                requiredOverlay.Ruleset.Value = ruleset.NewValue;
                allowedOverlay.Ruleset.Value = ruleset.NewValue;
            }, true);
        }

        public partial class RoundRow : CompositeDrawable, IModelBacked<TournamentRound>
        {
            public TournamentRound Model { get; }

            [Resolved]
            private LadderInfo ladderInfo { get; set; } = null!;

            [Resolved]
            private IDialogOverlay? dialogOverlay { get; set; }

            public RoundRow(TournamentRound round, UserModSelectOverlay requiredOverlay, FreeModSelectOverlay allowedOverlay)
            {
                Model = round;

                Masking = true;
                CornerRadius = 10;

                RoundBeatmapEditor beatmapEditor = new RoundBeatmapEditor(round, requiredOverlay, allowedOverlay)
                {
                    Width = 0.95f
                };

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        Colour = OsuColour.Gray(0.1f),
                        RelativeSizeAxes = Axes.Both,
                    },
                    new FillFlowContainer
                    {
                        Margin = new MarginPadding(5),
                        Padding = new MarginPadding { Right = 160 },
                        Spacing = new Vector2(5),
                        Direction = FillDirection.Full,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            new SettingsTextBox
                            {
                                LabelText = "Name",
                                Width = 0.33f,
                                Current = Model.Name
                            },
                            new SettingsTextBox
                            {
                                LabelText = "Description",
                                Width = 0.33f,
                                Current = Model.Description
                            },
                            new DateTextBox
                            {
                                LabelText = "Start Time",
                                Width = 0.33f,
                                Current = Model.StartDate
                            },
                            new SettingsSlider<int>
                            {
                                LabelText = "# of Bans",
                                Width = 0.33f,
                                Current = Model.BanCount
                            },
                            new SettingsSlider<int>
                            {
                                LabelText = "Best of",
                                Width = 0.33f,
                                Current = Model.BestOf
                            },
                            new SettingsButton
                            {
                                Width = 0.2f,
                                Margin = new MarginPadding(10),
                                Text = "Add beatmap",
                                Action = beatmapEditor.CreateNew
                            },
                            beatmapEditor
                        }
                    },
                    new DangerousSettingsButton
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        RelativeSizeAxes = Axes.None,
                        Width = 150,
                        Text = "Delete Round",
                        Action = () => dialogOverlay?.Push(new DeleteRoundDialog(Model, () =>
                        {
                            Expire();
                            ladderInfo.Rounds.Remove(Model);
                        }))
                    }
                };

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            public partial class RoundBeatmapEditor : CompositeDrawable
            {
                private readonly TournamentRound round;
                private readonly FillFlowContainer flow;
                private readonly UserModSelectOverlay requiredOverlay;
                private readonly FreeModSelectOverlay allowedOverlay;

                [Resolved]
                private LadderInfo ladder { get; set; } = null!;

                public RoundBeatmapEditor(TournamentRound round, UserModSelectOverlay requiredOverlay, FreeModSelectOverlay allowedOverlay)
                {
                    this.round = round;
                    this.requiredOverlay = requiredOverlay;
                    this.allowedOverlay = allowedOverlay;

                    RelativeSizeAxes = Axes.X;
                    AutoSizeAxes = Axes.Y;

                    InternalChild = flow = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        ChildrenEnumerable = round.Beatmaps.Select(p => new RoundBeatmapRow(round, p, requiredOverlay, allowedOverlay))
                    };
                }

                public void CreateNew()
                {
                    // New maps get the same defaults as old-bracket migration:
                    // freestyle off, no required mods, all free mods filled in.
                    var ruleset = ladder.Ruleset.Value?.CreateInstance();

                    var b = new RoundBeatmap
                    {
                        Freestyle = false,
                        RequiredMods = Array.Empty<APIMod>(),
                        AllowedMods = ruleset == null ? Array.Empty<APIMod>() : RoundBeatmap.GetAllFreeMods(ruleset),
                    };

                    round.Beatmaps.Add(b);

                    flow.Add(new RoundBeatmapRow(round, b, requiredOverlay, allowedOverlay));
                }

                public partial class RoundBeatmapRow : CompositeDrawable
                {
                    public RoundBeatmap Model { get; }

                    [Resolved]
                    protected IAPIProvider API { get; private set; } = null!;

                    [Resolved]
                    private LadderInfo ladder { get; set; } = null!;

                    [Resolved]
                    private BeatmapManager beatmaps { get; set; } = null!;

                    [Resolved]
                    private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

                    private readonly Bindable<int?> beatmapId = new Bindable<int?>();

                    private readonly Bindable<string> mods = new Bindable<string>(string.Empty);

                    private readonly Container drawableContainer;
                    private readonly RoundBeatmapModEditor modEditor;
                    private readonly OsuSpriteText freestyleBadge;
                    private readonly ModDisplay requiredInfoDisplay;
                    private readonly ModDisplay allowedInfoDisplay;
                    private readonly OsuSpriteText statsText;

                    private CancellationTokenSource? difficultyCts;

                    public RoundBeatmapRow(TournamentRound team, RoundBeatmap beatmap, UserModSelectOverlay requiredOverlay, FreeModSelectOverlay allowedOverlay)
                    {
                        Model = beatmap;

                        Margin = new MarginPadding(10);

                        RelativeSizeAxes = Axes.X;
                        AutoSizeAxes = Axes.Y;

                        Masking = true;
                        CornerRadius = 5;

                        InternalChildren = new Drawable[]
                        {
                            new Box
                            {
                                Colour = OsuColour.Gray(0.2f),
                                RelativeSizeAxes = Axes.Both,
                            },
                            new FillFlowContainer
                            {
                                Margin = new MarginPadding(5),
                                Padding = new MarginPadding { Right = 160 },
                                Spacing = new Vector2(5),
                                Direction = FillDirection.Vertical,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Children = new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        Spacing = new Vector2(5),
                                        Direction = FillDirection.Horizontal,
                                        AutoSizeAxes = Axes.Both,
                                        Children = new Drawable[]
                                        {
                                            new SettingsNumberBox
                                            {
                                                LabelText = "Beatmap ID",
                                                RelativeSizeAxes = Axes.None,
                                                Width = 200,
                                                Current = beatmapId,
                                            },
                                            new SettingsTextBox
                                            {
                                                LabelText = "Mods",
                                                RelativeSizeAxes = Axes.None,
                                                Width = 200,
                                                Current = mods,
                                            },
                                            new SettingsButton
                                            {
                                                RelativeSizeAxes = Axes.None,
                                                Width = 120,
                                                Margin = new MarginPadding(10),
                                                Text = "Edit mods",
                                                Action = () => modEditor.FadeTo(1 - modEditor.Alpha, 200),
                                            },
                                            drawableContainer = new Container
                                            {
                                                Size = new Vector2(100, 70),
                                            },
                                        }
                                    },
                                    modEditor = new RoundBeatmapModEditor(beatmap, requiredOverlay, allowedOverlay)
                                    {
                                        Alpha = 0,
                                    },
                                    new FillFlowContainer
                                    {
                                        Spacing = new Vector2(8),
                                        Direction = FillDirection.Horizontal,
                                        AutoSizeAxes = Axes.Both,
                                        Children = new Drawable[]
                                        {
                                            freestyleBadge = new OsuSpriteText
                                            {
                                                Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 14),
                                            },
                                            new OsuSpriteText
                                            {
                                                Font = OsuFont.GetFont(size: 14),
                                                Text = "Required:",
                                            },
                                            requiredInfoDisplay = new ModDisplay
                                            {
                                                Scale = new Vector2(0.4f),
                                                ExpansionMode = ExpansionMode.AlwaysContracted,
                                            },
                                            new OsuSpriteText
                                            {
                                                Font = OsuFont.GetFont(size: 14),
                                                Text = "Allowed:",
                                            },
                                            allowedInfoDisplay = new ModDisplay
                                            {
                                                Scale = new Vector2(0.4f),
                                                ExpansionMode = ExpansionMode.AlwaysContracted,
                                            },
                                            statsText = new OsuSpriteText
                                            {
                                                Font = OsuFont.GetFont(size: 14),
                                            },
                                        }
                                    },
                                }
                            },
                            new DangerousSettingsButton
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                RelativeSizeAxes = Axes.None,
                                Width = 150,
                                Text = "Delete Beatmap",
                                Action = () =>
                                {
                                    Expire();
                                    team.Beatmaps.Remove(beatmap);
                                },
                            }
                        };

                        modEditor.ModsChanged += () => Schedule(refreshInfo);
                    }

                    protected override void LoadComplete()
                    {
                        base.LoadComplete();
                        refreshInfo();
                    }

                    [BackgroundDependencyLoader]
                    private void load()
                    {
                        beatmapId.Value = Model.ID;
                        beatmapId.BindValueChanged(id =>
                        {
                            Model.ID = id.NewValue ?? 0;

                            if (id.NewValue != id.OldValue)
                                Model.Beatmap = null;

                            if (Model.Beatmap != null)
                            {
                                updatePanel();
                                refreshInfo();
                                return;
                            }

                            var req = new GetBeatmapRequest(new APIBeatmap { OnlineID = Model.ID });

                            req.Success += res =>
                            {
                                Model.Beatmap = new TournamentBeatmap(res);
                                updatePanel();
                                refreshInfo();
                            };

                            req.Failure += _ =>
                            {
                                Model.Beatmap = null;
                                updatePanel();
                                refreshInfo();
                            };

                            API.Queue(req);
                        }, true);

                        mods.Value = Model.Mods;
                        mods.BindValueChanged(modString => Model.Mods = modString.NewValue);
                    }

                    private void updatePanel() => Schedule(() =>
                    {
                        drawableContainer.Clear();

                        if (Model.Beatmap != null)
                        {
                            drawableContainer.Child = new TournamentBeatmapPanel(Model.Beatmap, Model.Mods)
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Width = 300
                            };
                        }
                    });

                    /// <summary>
                    /// Refreshes the gameplay-mod displays and recomputes adjusted stats.
                    /// </summary>
                    private void refreshInfo() => Schedule(() =>
                    {
                        if (!IsLoaded)
                            return;

                        var ruleset = ladder.Ruleset.Value?.CreateInstance();

                        Mod[] required = ruleset == null ? Array.Empty<Mod>() : RoundBeatmap.InstantiateMods(Model.SafeRequiredMods, ruleset);
                        Mod[] allowed = ruleset == null ? Array.Empty<Mod>() : RoundBeatmap.InstantiateMods(Model.SafeAllowedMods, ruleset);

                        freestyleBadge.Text = Model.Freestyle ? "FREESTYLE" : "LOCKED";
                        requiredInfoDisplay.Current.Value = required;
                        allowedInfoDisplay.Current.Value = Model.Freestyle ? Array.Empty<Mod>() : allowed;

                        recomputeStats(ruleset, required);
                    });

                    /// <summary>
                    /// Recomputes star rating (background thread via difficulty cache) and
                    /// AR/CS/OD/HP display values for the current required mods.
                    /// Falls back to online values when the beatmap is not available locally.
                    /// </summary>
                    private void recomputeStats(Ruleset? ruleset, Mod[] required)
                    {
                        difficultyCts?.Cancel();

                        var beatmapInfo = Model.Beatmap;
                        var rulesetInfo = ladder.Ruleset.Value;

                        if (ruleset == null || beatmapInfo == null || rulesetInfo == null)
                        {
                            statsText.Text = beatmapInfo == null ? "No beatmap data." : $"{beatmapInfo.StarRating:F2}* (base)";
                            return;
                        }

                        BeatmapDifficulty adjusted = ruleset.GetAdjustedDisplayDifficulty(beatmapInfo, required);
                        string modString = required.Length == 0 ? "NM" : string.Join(",", required.Select(m => m.Acronym));

                        string attributes = $"AR {adjusted.ApproachRate:F1} CS {adjusted.CircleSize:F1} OD {adjusted.OverallDifficulty:F1} HP {adjusted.DrainRate:F1}";

                        var local = beatmaps.QueryBeatmap(b => b.OnlineID == Model.ID);

                        if (local == null)
                        {
                            statsText.Text = $"{beatmapInfo.StarRating:F2}* ({modString}, online) | {attributes}";
                            return;
                        }

                        var cts = difficultyCts = new CancellationTokenSource();
                        int beatmapIdSnapshot = Model.ID;

                        difficultyCache.GetDifficultyAsync(local, rulesetInfo, required, cts.Token).ContinueWith(t => Schedule(() =>
                        {
                            if (cts.IsCancellationRequested || Model.ID != beatmapIdSnapshot)
                                return;

                            double stars = t.Status == TaskStatus.RanToCompletion && t.Result != null
                                ? t.Result.Value.Stars
                                : beatmapInfo.StarRating;

                            statsText.Text = $"{stars:F2}* ({modString}) | {attributes}";
                        }), TaskScheduler.Default);
                    }

                    protected override void Dispose(bool isDisposing)
                    {
                        base.Dispose(isDisposing);
                        difficultyCts?.Cancel();
                        difficultyCts?.Dispose();
                    }
                }
            }
        }

        protected override RoundRow CreateDrawable(TournamentRound model) => new RoundRow(model, requiredOverlay, allowedOverlay);
    }
}
