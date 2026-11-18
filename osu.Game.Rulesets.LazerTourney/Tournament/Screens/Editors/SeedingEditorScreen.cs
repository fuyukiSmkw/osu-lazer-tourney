// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.LazerTourney.Tournament.Components;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Editors.Components;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Editors
{
    public partial class SeedingEditorScreen : TournamentEditorScreen<SeedingEditorScreen.SeedingResultRow, SeedingResult>
    {
        private readonly TournamentTeam team;

        protected override BindableList<SeedingResult> Storage => team.SeedingResults;

        public SeedingEditorScreen(TournamentTeam team, TournamentScreen parentScreen)
            : base(parentScreen)
        {
            this.team = team;
        }

        public partial class SeedingResultRow : OsuRearrangeableListItem<SeedingResult>, IModelBacked<SeedingResult>
        {
            // The base item exposes the model as a field; the editor screen expects an IModelBacked property.
            SeedingResult IModelBacked<SeedingResult>.Model => Model;

            private readonly TournamentTeam team;

            public SeedingResultRow(TournamentTeam team, SeedingResult round)
                : base(round)
            {
                this.team = team;

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
                SeedingBeatmapEditor beatmapEditor = new SeedingBeatmapEditor(Model)
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
                                    LabelText = "Mod",
                                    Width = 0.33f,
                                    Current = Model.Mod
                                },
                                new SettingsSlider<int>
                                {
                                    LabelText = "Seed",
                                    Width = 0.33f,
                                    Current = Model.Seed
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
                            TooltipText = "Delete result",
                            // The list container removes (and disposes) the row once the model entry is gone.
                            Action = () => team.SeedingResults.Remove(Model),
                        },
                    }
                };
            }

            public partial class SeedingBeatmapEditor : ExpandedRearrangeableListContainer<SeedingBeatmap>
            {
                private readonly SeedingResult round;

                public SeedingBeatmapEditor(SeedingResult round)
                {
                    this.round = round;

                    // Dragging rows around updates the seeding model directly (and keeps external model changes in sync).
                    Items.BindTarget = round.Beatmaps;
                }

                public void CreateNew()
                {
                    round.Beatmaps.Add(new SeedingBeatmap());
                }

                protected override OsuRearrangeableListItem<SeedingBeatmap> CreateOsuDrawable(SeedingBeatmap item)
                    => new SeedingBeatmapRow(round, item);

                public partial class SeedingBeatmapRow : OsuRearrangeableListItem<SeedingBeatmap>
                {
                    private readonly SeedingResult result;

                    [Resolved]
                    protected IAPIProvider API { get; private set; } = null!;

                    private readonly Bindable<int?> beatmapId = new Bindable<int?>();

                    private readonly Bindable<string> score = new Bindable<string>(string.Empty);

                    private Container drawableContainer = null!;

                    public SeedingBeatmapRow(SeedingResult result, SeedingBeatmap beatmap)
                        : base(beatmap)
                    {
                        this.result = result;

                        Margin = new MarginPadding(10);

                        Masking = true;
                        CornerRadius = 5;
                    }

                    [BackgroundDependencyLoader]
                    private void load()
                    {
                        // The item draws the row background (so it also covers the drag handle); added after the base
                        // dependency loader so it is not overwritten by the base's children setup.
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

                            if (Model.Beatmap != null)
                            {
                                updatePanel();
                                return;
                            }

                            var req = new GetBeatmapRequest(new APIBeatmap { OnlineID = Model.ID });

                            req.Success += res => Schedule(() =>
                            {
                                Model.Beatmap = new TournamentBeatmap(res);
                                updatePanel();
                            });

                            req.Failure += _ => Schedule(() =>
                            {
                                Model.Beatmap = null;
                                updatePanel();
                            });

                            API.Queue(req);
                        }, true);

                        score.Value = Model.Score.ToString();
                        score.BindValueChanged(str => long.TryParse(str.NewValue, out Model.Score));
                    }

                    protected override Drawable CreateContent()
                    {
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
                                        new SettingsSlider<int>
                                        {
                                            LabelText = "Seed",
                                            RelativeSizeAxes = Axes.None,
                                            Width = 200,
                                            Current = Model.Seed
                                        },
                                        new SettingsTextBox
                                        {
                                            LabelText = "Score",
                                            RelativeSizeAxes = Axes.None,
                                            Width = 200,
                                            Current = score,
                                        },
                                        drawableContainer = new Container
                                        {
                                            Size = new Vector2(100, 70),
                                        },
                                    }
                                },
                                new DeleteIconButton
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    TooltipText = "Delete Beatmap",
                                    // The list container removes (and disposes) the row once the model entry is gone.
                                    Action = () => result.Beatmaps.Remove(Model),
                                }
                            }
                        };
                    }

                    private void updatePanel()
                    {
                        drawableContainer.Clear();

                        if (Model.Beatmap != null)
                        {
                            drawableContainer.Child = new TournamentBeatmapPanel(Model.Beatmap, result.Mod.Value)
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Width = 300
                            };
                        }
                    }
                }
            }
        }

        protected override SeedingResultRow CreateDrawable(SeedingResult model) => new SeedingResultRow(team, model);

        protected override OsuRearrangeableListContainer<SeedingResult> CreateReorderableList()
        {
            var list = new SeedingResultList(CreateDrawable);

            // Dragging results around updates the bracket model directly (and keeps external model changes in sync).
            list.Items.BindTarget = team.SeedingResults;

            return list;
        }

        /// <summary>
        /// Reorderable list of <see cref="SeedingResultRow"/>s, shown in place of the editor's default flow.
        /// </summary>
        public partial class SeedingResultList : ExpandedRearrangeableListContainer<SeedingResult>
        {
            private readonly Func<SeedingResult, SeedingResultRow> createRow;

            public SeedingResultList(Func<SeedingResult, SeedingResultRow> createRow)
            {
                this.createRow = createRow;
            }

            protected override OsuRearrangeableListItem<SeedingResult> CreateOsuDrawable(SeedingResult item) => createRow(item);

            // Matches the spacing the default editor flow uses between rows.
            protected override FillFlowContainer<RearrangeableListItem<SeedingResult>> CreateListFillFlowContainer()
                => base.CreateListFillFlowContainer().With(list => list.Spacing = new Vector2(0, 20));
        }
    }
}
