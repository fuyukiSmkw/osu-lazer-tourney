// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Setup
{
    /// <summary>
    /// A form control for a colour stored as a <c>#rrggbb</c> string. Clicking the colour
    /// preview opens lazer's colour picker; its suggestion swatches are intentionally empty.
    /// </summary>
    internal partial class FormColourPicker : CompositeDrawable, IFormControl
    {
        public LocalisableString Caption { get; init; }
        public LocalisableString HintText { get; init; }

        public event Action? ValueChanged;

        private readonly Bindable<string> current;
        private readonly Bindable<Colour4> colour = new Bindable<Colour4>();
        private bool updating;

        public FormColourPicker(Bindable<string> current)
        {
            // Store a bound copy rather than the model bindable itself. Drawables automatically call
            // UnbindAll() on their IUnbindable fields when disposed, which would otherwise clear every
            // other listener on the shared model bindable (for example TourneyVideo's fallback refresh).
            this.current = current.GetBoundCopy();

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            colour.Value = parseColour(this.current.Value, this.current.Default);

            this.current.ValueChanged += onCurrentChanged;
            colour.ValueChanged += onColourChanged;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new FormControlBackground(),
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(9),
                    Spacing = new Vector2(0, 4),
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        new FormFieldCaption
                        {
                            Anchor = Anchor.TopLeft,
                            Origin = Anchor.TopLeft,
                            Caption = Caption,
                            TooltipText = HintText,
                        },
                        new ColourControl
                        {
                            Current = colour,
                        },
                    },
                },
            };
        }

        private void onCurrentChanged(ValueChangedEvent<string> e)
        {
            if (updating)
                return;

            updating = true;
            colour.Value = parseColour(e.NewValue, current.Default);
            updating = false;

            ValueChanged?.Invoke();
        }

        private void onColourChanged(ValueChangedEvent<Colour4> e)
        {
            if (updating)
                return;

            updating = true;
            current.Value = e.NewValue.ToHex().ToLowerInvariant();
            updating = false;

            ValueChanged?.Invoke();
        }

        private static Colour4 parseColour(string? value, string? fallback)
        {
            if (tryParse(value, out Colour4 colour))
                return colour;

            if (tryParse(fallback, out colour))
                return colour;

            return Colour4.White;
        }

        private static bool tryParse(string? value, out Colour4 colour)
        {
            colour = default;

            return value != null
                   && value.Length == 7
                   && value[0] == '#'
                   && Colour4.TryParseHex(value, out colour);
        }

        public bool IsDefault => current.IsDefault;

        public void SetDefault() => current.SetDefault();

        public bool IsDisabled => current.Disabled;

        public IEnumerable<LocalisableString> FilterTerms => new[] { Caption, HintText };

        public float MainDrawHeight => DrawHeight;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                current.ValueChanged -= onCurrentChanged;
                colour.ValueChanged -= onColourChanged;
            }
        }

        private partial class ColourControl : OsuClickableContainer, IHasPopover, IHasCurrentValue<Colour4>
        {
            private readonly BindableWithCurrent<Colour4> current = new BindableWithCurrent<Colour4>();

            public Bindable<Colour4> Current
            {
                get => current.Current;
                set => current.Current = value;
            }

            private readonly Box background;
            private readonly Box preview;
            private readonly OsuSpriteText hexText;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public ColourControl()
            {
                RelativeSizeAxes = Axes.X;
                Height = 40;
                Masking = true;
                CornerRadius = 5;
                Action = this.ShowPopover;
                TooltipText = "Click to choose a colour";

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 5 },
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Absolute, 30),
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 16),
                        },
                        RowDimensions = new[] { new Dimension() },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new Container
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Size = new Vector2(30),
                                    Masking = true,
                                    CornerRadius = 4,
                                    BorderThickness = 1,
                                    BorderColour = new Colour4(1f, 1f, 1f, 0.25f),
                                    Child = preview = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                    },
                                },
                                hexText = new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Font = OsuFont.Default.With(size: 16),
                                    Margin = new MarginPadding { Left = 10 },
                                },
                                new SpriteIcon
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Size = new Vector2(14),
                                    Icon = FontAwesome.Solid.ChevronRight,
                                },
                            },
                        },
                    },
                };
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                background.Colour = colourProvider.Background3;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Current.BindValueChanged(_ => updateColour(), true);
            }

            private void updateColour()
            {
                preview.Colour = Current.Value;
                hexText.Text = Current.Value.ToHex();
            }

            public Popover GetPopover()
            {
                var picker = new OsuColourPicker
                {
                    Current = { BindTarget = Current },
                };

                // Tournament backgrounds do not need a row of preset swatches.
                picker.Suggestions.Clear();

                return new OsuPopover(false)
                {
                    Child = picker,
                };
            }
        }
    }
}
