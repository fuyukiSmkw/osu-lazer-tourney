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
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online;
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

        public partial class RoundRow : OsuRearrangeableListItem<TournamentRound>, IModelBacked<TournamentRound>
        {
            // The base item exposes the model as a field; the editor screen expects an IModelBacked property.
            TournamentRound IModelBacked<TournamentRound>.Model => Model;

            private readonly UserModSelectOverlay requiredOverlay;
            private readonly FreeModSelectOverlay allowedOverlay;

            [Resolved]
            private LadderInfo ladderInfo { get; set; } = null!;

            [Resolved]
            private IDialogOverlay? dialogOverlay { get; set; }

            public RoundRow(TournamentRound round, UserModSelectOverlay requiredOverlay, FreeModSelectOverlay allowedOverlay)
                : base(round)
            {
                this.requiredOverlay = requiredOverlay;
                this.allowedOverlay = allowedOverlay;

                Masking = true;
                CornerRadius = 10;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                // The item draws the row background (so it also covers the drag handle); added after the base
                // dependency loader so it is not overwritten by the base's children setup.
                AddInternal(new Box
                {
                    Colour = OsuColour.Gray(0.1f),
                    RelativeSizeAxes = Axes.Both,
                    Depth = float.MaxValue,
                });
            }

            protected override Drawable CreateContent()
            {
                RoundBeatmapEditor beatmapEditor = new RoundBeatmapEditor(Model, requiredOverlay, allowedOverlay)
                {
                    Width = 0.95f
                };

                return new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            Margin = new MarginPadding(5),
                            Padding = new MarginPadding { Right = 40 },
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
                                    LabelText = "# of Protects",
                                    Width = 0.33f,
                                    Current = Model.ProtectCount
                                },
                                new SettingsSlider<int>
                                {
                                    LabelText = "Best of",
                                    Width = 0.33f,
                                    Current = Model.BestOf
                                },
                                new ModMultiplierRuleEditor
                                {
                                    Width = 0.66f,
                                    LabelText = "Default mod multiplier rule",
                                    EmptyHint = "Empty = x1 for all maps in this round.",
                                    Current = Model.DefaultModMultiplierRule,
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
                        new DeleteIconButton
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            TooltipText = "Delete Round",
                            // The list container removes (and disposes) the row once the model entry is gone.
                            Action = () => dialogOverlay?.Push(new DeleteRoundDialog(Model, () => ladderInfo.Rounds.Remove(Model)))
                        }
                    }
                };
            }

            public partial class RoundBeatmapEditor : ExpandedRearrangeableListContainer<RoundBeatmap>
            {
                private readonly TournamentRound round;
                private readonly UserModSelectOverlay requiredOverlay;
                private readonly FreeModSelectOverlay allowedOverlay;

                public RoundBeatmapEditor(TournamentRound round, UserModSelectOverlay requiredOverlay, FreeModSelectOverlay allowedOverlay)
                {
                    this.round = round;
                    this.requiredOverlay = requiredOverlay;
                    this.allowedOverlay = allowedOverlay;

                    // Dragging rows around updates the bracket model directly (and keeps external model changes in sync).
                    Items.BindTarget = round.Beatmaps;
                }

                public void CreateNew()
                {
                    // New maps get the same defaults as old-bracket migration:
                    // freestyle off, no required mods, no allowed mods.
                    round.Beatmaps.Add(new RoundBeatmap
                    {
                        Freestyle = false,
                        RequiredMods = Array.Empty<APIMod>(),
                        AllowedMods = Array.Empty<APIMod>(),
                    });
                }

                protected override OsuRearrangeableListItem<RoundBeatmap> CreateOsuDrawable(RoundBeatmap item)
                    => new RoundBeatmapRow(round, item, requiredOverlay, allowedOverlay, refreshPickLabelling);

                /// <summary>
                /// Recomputes the inferred pick labels of all rows after a map's classification label changes.
                /// </summary>
                private void refreshPickLabelling()
                {
                    foreach (var row in this.ChildrenOfType<RoundBeatmapRow>())
                        row.RefreshPickEditor();
                }

                public partial class RoundBeatmapRow : OsuRearrangeableListItem<RoundBeatmap>
                {
                    private readonly TournamentRound round;
                    private readonly UserModSelectOverlay requiredOverlay;
                    private readonly FreeModSelectOverlay allowedOverlay;

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

                    private readonly Action pickInferenceChanged;

                    private Container drawableContainer = null!;
                    private Container downloadButtonContainer = null!;
                    private RoundBeatmapModEditor modEditor = null!;
                    private RoundBeatmapPickEditor? pickEditor;
                    private OsuSpriteText freestyleBadge = null!;
                    private ModDisplay requiredInfoDisplay = null!;
                    private ModDisplay allowedInfoDisplay = null!;
                    private OsuSpriteText statsText = null!;

                    private CancellationTokenSource? difficultyCts;

                    private IBeatmapSetInfo? apiBeatmapSet;

                    public RoundBeatmapRow(TournamentRound round, RoundBeatmap beatmap, UserModSelectOverlay requiredOverlay, FreeModSelectOverlay allowedOverlay, Action pickInferenceChanged)
                        : base(beatmap)
                    {
                        this.round = round;
                        this.requiredOverlay = requiredOverlay;
                        this.allowedOverlay = allowedOverlay;
                        this.pickInferenceChanged = pickInferenceChanged;

                        Margin = new MarginPadding(10);

                        // The item draws the row background (so it also covers the drag handle) and clips its corners.
                        Masking = true;
                        CornerRadius = 5;
                    }

                    protected override Drawable CreateContent()
                    {
                        modEditor = new RoundBeatmapModEditor(Model, requiredOverlay, allowedOverlay)
                        {
                            Alpha = 0,
                        };

                        modEditor.ModsChanged += () => Schedule(refreshInfo);

                        pickEditor = new RoundBeatmapPickEditor(round, Model)
                        {
                            RelativeSizeAxes = Axes.None,
                            Width = 100,
                        };

                        return new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    Margin = new MarginPadding(5),
                                    Padding = new MarginPadding { Right = 100 },
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
                                                    Width = 140,
                                                    Current = mods,
                                                },
                                                pickEditor,
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
                                        modEditor,
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
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(5),
                                    Children = new Drawable[]
                                    {
                                        downloadButtonContainer = new Container
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            AutoSizeAxes = Axes.Both,
                                        },
                                        new DeleteIconButton
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            TooltipText = "Delete Beatmap",
                                            // The list container removes (and disposes) the row once the model entry is gone.
                                            Action = () => round.Beatmaps.Remove(Model),
                                        },
                                    }
                                },
                            },
                        };
                    }

                    protected override void LoadComplete()
                    {
                        base.LoadComplete();
                        refreshInfo();
                    }

                    [BackgroundDependencyLoader]
                    private void load()
                    {
                        AddInternal(new Box
                        {
                            Colour = OsuColour.Gray(0.2f),
                            RelativeSizeAxes = Axes.Both,
                            Depth = float.MaxValue,
                        });

                        beatmapId.Value = Model.ID;
                        beatmapId.BindValueChanged(id =>
                        {
                            Model.ID = id.NewValue ?? 0;

                            if (id.NewValue != id.OldValue)
                                Model.Beatmap = null;

                            apiBeatmapSet = null;

                            // Downloaded: show stored metadata, no fetch and no download button.
                            if (Model.Beatmap != null && beatmaps.QueryBeatmap(b => b.OnlineID == Model.ID) != null)
                            {
                                updatePanel();
                                refreshInfo();
                                return;
                            }

                            if (Model.Beatmap != null)
                            {
                                // Pre-populated metadata but not downloaded locally: show it right away.
                                // The fetch below refreshes the metadata and provides set info for the download button.
                                updatePanel();
                                refreshInfo();
                            }

                            var req = new GetBeatmapRequest(new APIBeatmap { OnlineID = Model.ID });

                            req.Success += res =>
                            {
                                Model.Beatmap = new TournamentBeatmap(res);
                                apiBeatmapSet = res.BeatmapSet ?? new APIBeatmapSet { OnlineID = res.OnlineBeatmapSetID };
                                updatePanel();
                                refreshInfo();
                            };

                            req.Failure += _ =>
                            {
                                Model.Beatmap = null;
                                apiBeatmapSet = null;
                                updatePanel();
                                refreshInfo();
                            };

                            API.Queue(req);
                        }, true);

                        mods.Value = Model.Mods;
                        mods.BindValueChanged(modString =>
                        {
                            Model.Mods = modString.NewValue;
                            pickInferenceChanged();
                        });
                    }

                    private void updatePanel() => Schedule(() =>
                    {
                        drawableContainer.Clear();
                        downloadButtonContainer.Clear();

                        if (Model.Beatmap == null)
                            return;

                        drawableContainer.Child = new TournamentBeatmapPanel(Model.Beatmap, Model.Mods)
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Width = 300
                        };

                        // On-demand download button: only when the map is missing locally
                        // and the set info (from the metadata fetch above) is known.
                        if (apiBeatmapSet != null && beatmaps.QueryBeatmap(b => b.OnlineID == Model.ID) == null)
                            downloadButtonContainer.Child = new RowDownloadButton(apiBeatmapSet, Model.ID);
                    });

                    /// <summary>
                    /// Download button that hides itself once the beatmap is available locally.
                    /// Mirrors <c>DrawableRoomPlaylistItem.PlaylistDownloadButton</c>.
                    /// </summary>
                    private sealed partial class RowDownloadButton : BeatmapDownloadButton
                    {
                        private readonly int onlineId;

                        [Resolved]
                        private BeatmapManager beatmapManager { get; set; } = null!;

                        // required for download tracking, as this button hides itself. can probably be removed with a bit of consideration.
                        public override bool IsPresent => true;

                        private const float width = 50;

                        public RowDownloadButton(IBeatmapSetInfo beatmapSet, int onlineId)
                            : base(beatmapSet)
                        {
                            this.onlineId = onlineId;

                            Size = new Vector2(width, 30);
                            Alpha = 0;
                        }

                        protected override void LoadComplete()
                        {
                            State.BindValueChanged(stateChanged, true);

                            // base implementation calls FinishTransforms, so should be run after the above state update.
                            base.LoadComplete();
                        }

                        private void stateChanged(ValueChangedEvent<DownloadState> state)
                        {
                            switch (state.NewValue)
                            {
                                case DownloadState.Unknown:
                                    // Ignore initial state to ensure the button doesn't briefly appear.
                                    break;

                                case DownloadState.LocallyAvailable:
                                    // Perform a local query of the beatmap by online ID, and reset the state if not matching.
                                    if (beatmapManager.QueryBeatmap(b => b.OnlineID == onlineId) == null)
                                        State.Value = DownloadState.NotDownloaded;
                                    else
                                    {
                                        this.FadeTo(0, 500)
                                            .ResizeWidthTo(0, 500, Easing.OutQuint);
                                    }

                                    break;

                                default:
                                    this.ResizeWidthTo(width, 500, Easing.OutQuint)
                                        .FadeTo(1, 500);
                                    break;
                            }
                        }
                    }

                    /// <summary>
                    /// Refreshes the inferred pick label (used when another map in the same round changes its Mods).
                    /// </summary>
                    public void RefreshPickEditor() => pickEditor?.Refresh();

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

        protected override OsuRearrangeableListContainer<TournamentRound> CreateReorderableList()
        {
            var list = new RoundList(CreateDrawable);

            // Dragging rounds around updates the bracket model directly (and keeps external model changes in sync).
            list.Items.BindTarget = Storage;

            return list;
        }

        /// <summary>
        /// Reorderable list of <see cref="RoundRow"/>s, shown in place of the editor's default flow.
        /// </summary>
        public partial class RoundList : ExpandedRearrangeableListContainer<TournamentRound>
        {
            private readonly Func<TournamentRound, RoundRow> createRow;

            public RoundList(Func<TournamentRound, RoundRow> createRow)
            {
                this.createRow = createRow;
            }

            protected override OsuRearrangeableListItem<TournamentRound> CreateOsuDrawable(TournamentRound item) => createRow(item);

            // Matches the spacing the default editor flow uses between rows.
            protected override FillFlowContainer<RearrangeableListItem<TournamentRound>> CreateListFillFlowContainer()
                => base.CreateListFillFlowContainer().With(list => list.Spacing = new Vector2(0, 20));
        }
    }
}
