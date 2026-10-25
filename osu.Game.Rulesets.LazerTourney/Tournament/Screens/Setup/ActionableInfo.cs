// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Setup
{
    /// <summary>
    /// A settings-style row showing a value and an action button. It intentionally mirrors
    /// the layout of lazer's form controls so the setup screen uses one visual language.
    /// </summary>
    internal partial class ActionableInfo : CompositeDrawable
    {
        public const float BUTTON_SIZE = 120;

        public Action? Action;

        private readonly FormFieldCaption caption;
        private readonly OsuTextFlowContainer valueText;
        private readonly RoundedButton button;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            caption.Colour = colourProvider.Content2;
        }

        public ActionableInfo()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = SettingsPanel.CONTENT_PADDING,
                Child = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        new FormControlBackground(),
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding { Horizontal = 9, Vertical = 9 },
                            Child = new GridContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                ColumnDimensions = new[]
                                {
                                    new Dimension(),
                                    new Dimension(GridSizeMode.AutoSize),
                                },
                                RowDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.AutoSize),
                                },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 3),
                                            Padding = new MarginPadding { Right = 10 },
                                            Children = new Drawable[]
                                            {
                                                caption = new FormFieldCaption(),
                                                valueText = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 16))
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                },
                                            }
                                        },
                                        button = new RoundedButton
                                        {
                                            Anchor = Anchor.CentreRight,
                                            Origin = Anchor.CentreRight,
                                            Size = new Vector2(BUTTON_SIZE, 40),
                                            Action = () => Action?.Invoke(),
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        public LocalisableString Label
        {
            set
            {
                caption.Caption = value;
                caption.Alpha = string.IsNullOrEmpty(value.ToString()) ? 0 : 1;
            }
        }

        public LocalisableString Description
        {
            set => caption.TooltipText = value;
        }

        public LocalisableString ButtonText
        {
            set => button.Text = value;
        }

        public bool ButtonEnabled
        {
            set => button.Enabled.Value = value;
        }

        public Color4 ButtonColour
        {
            set => button.BackgroundColour = value;
        }

        public LocalisableString Value
        {
            set => valueText.Text = value;
        }

        public bool Failing
        {
            set => valueText.Colour = value ? Color4.Red : Color4.White;
        }
    }
}
