// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Rulesets.LazerTourney.Tournament.Components;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Editors.Components;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Editors
{
    public abstract partial class TournamentEditorScreen<TDrawable, TModel> : TournamentScreen
        where TDrawable : Drawable, IModelBacked<TModel>
        where TModel : class, new()
    {
        protected abstract BindableList<TModel> Storage { get; }

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved]
        private TournamentSceneManager? sceneManager { get; set; }

        protected ControlPanel ControlPanel = null!;

        private readonly TournamentScreen? parentScreen;

        private BackButton backButton = null!;

        protected TournamentEditorScreen(TournamentScreen? parentScreen = null)
        {
            this.parentScreen = parentScreen;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            var content = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding(20),
            };

            var reorderableList = CreateReorderableList();

            if (reorderableList != null)
            {
                // The list keeps Storage in sync by itself (dragging moves entries around in it).
                content.Child = reorderableList;
            }
            else
            {
                FillFlowContainer<TDrawable> defaultFlow;

                content.Child = defaultFlow = new FillFlowContainer<TDrawable>
                {
                    Direction = FillDirection.Vertical,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Spacing = new Vector2(20),
                };

                Storage.CollectionChanged += (_, args) =>
                {
                    switch (args.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            Debug.Assert(args.NewItems != null);

                            args.NewItems.Cast<TModel>().ForEach(i => defaultFlow.Add(CreateDrawable(i)));
                            break;

                        case NotifyCollectionChangedAction.Remove:
                            Debug.Assert(args.OldItems != null);

                            args.OldItems.Cast<TModel>().ForEach(i => defaultFlow.RemoveAll(d => d.Model == i, true));
                            break;
                    }
                };

                foreach (var model in Storage)
                    defaultFlow.Add(CreateDrawable(model));
            }

            AddRangeInternal(new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = OsuColour.Gray(0.2f),
                },
                new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Child = content,
                },
                ControlPanel = new ControlPanel
                {
                    Children = new Drawable[]
                    {
                        new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Add new",
                            Action = () => Storage.Add(new TModel())
                        },
                        new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            BackgroundColour = colours.DangerousButtonColour,
                            Text = "Clear all",
                            Action = () =>
                            {
                                dialogOverlay?.Push(new TournamentClearAllDialog(() => Storage.Clear()));
                            }
                        },
                    }
                }
            });

            if (parentScreen != null)
            {
                AddInternal(backButton = new BackButton
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    State = { Value = Visibility.Visible },
                    Action = () => sceneManager?.SetScreen(parentScreen.GetType())
                });

                content.Padding = new MarginPadding { Bottom = backButton.Height * 2 };
            }
        }

        protected abstract TDrawable CreateDrawable(TModel model);

        /// <summary>
        /// Creates a reorderable list to display the rows in place of the default flow, or <c>null</c> to use the flow.
        /// </summary>
        /// <remarks>
        /// The returned list is responsible for keeping <see cref="Storage"/> in sync with its item order
        /// (bind <see cref="RearrangeableListContainer{TModel}.Items"/> to it).
        /// </remarks>
        protected virtual OsuRearrangeableListContainer<TModel>? CreateReorderableList() => null;
    }
}
