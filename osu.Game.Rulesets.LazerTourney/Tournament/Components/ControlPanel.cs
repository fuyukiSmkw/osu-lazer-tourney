// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// An element anchored to the right-hand area of a screen that provides streamer level controls.
    /// Should be off-screen.
    /// </summary>
    public partial class ControlPanel : Container
    {
        private readonly FillFlowContainer buttons;
        private readonly FillFlowContainer scrollFlow;
        private readonly FillFlowContainer bottomBarFlow;

        protected override Container<Drawable> Content => buttons;

        /// <summary>
        /// Content placed below the main <see cref="Content"/>.
        /// Hosted in a vertical scroll container filling the space between <see cref="Content"/> and <see cref="BottomBarContent"/>.
        /// </summary>
        public FillFlowContainer ScrollContent => scrollFlow;

        /// <summary>
        /// Content pinned to the bottom of the panel (bottom bar).
        /// </summary>
        public FillFlowContainer BottomBarContent => bottomBarFlow;

        /// <summary>
        /// Convenience setter for object-initialiser usage (matches the existing <c>Children = ...</c> style).
        /// </summary>
        public Drawable[] ScrollChildren
        {
            get => scrollFlow.Children.ToArray();
            set => scrollFlow.Children = value;
        }

        /// <summary>
        /// Convenience setter for object-initialiser usage (matches the existing <c>Children = ...</c> style).
        /// </summary>
        public Drawable[] BottomBarChildren
        {
            get => bottomBarFlow.Children.ToArray();
            set => bottomBarFlow.Children = value;
        }

        public ControlPanel()
        {
            RelativeSizeAxes = Axes.Y;
            AlwaysPresent = true;
            Width = TournamentSceneManager.CONTROL_PANEL_WIDTH;
            Anchor = Anchor.TopRight;

            // Popovers opened from panel buttons (e.g. the countdown button) are hosted
            // by this container, keeping them inside the panel and off the stream area.
            // Sizing is mirrored to the inner content, so layout is unchanged.
            InternalChild = new PopoverContainer
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(54, 54, 54, 255)
                },
                new TournamentSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Text = "Control Panel",
                    Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 22)
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = 35 },
                    Child = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                        },
                        RowDimensions = new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new Drawable[][]
                        {
                            new Drawable[]
                            {
                                buttons = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Padding = new MarginPadding(5),
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 5f),
                                },
                            },
                            new Drawable[]
                            {
                                new OsuScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Child = scrollFlow = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Padding = new MarginPadding(5),
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 5f),
                                    },
                                },
                            },
                            new Drawable[]
                            {
                                bottomBarFlow = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Padding = new MarginPadding(5),
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 5f),
                                },
                            },
                        },
                    },
                },
                },
            };
        }

        public partial class Spacer : CompositeDrawable
        {
            public Spacer(float height = 20)
            {
                RelativeSizeAxes = Axes.X;
                Height = height;
                AlwaysPresent = true;
            }
        }
    }
}
