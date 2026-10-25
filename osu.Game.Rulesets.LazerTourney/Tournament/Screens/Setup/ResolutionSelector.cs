// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Setup
{
    internal partial class ResolutionSelector : CompositeDrawable
    {
        private const int minimum_window_height = 480;
        private const int maximum_window_height = 2160;

        public Action<int>? Action;

        private readonly Bindable<string> height = new Bindable<string>("1080");
        private readonly FormNumberBox numberBox;

        public ResolutionSelector()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    new SettingsItemV2(numberBox = new FormNumberBox
                    {
                        Caption = "Window height",
                        HintText = "Sets the window height and the minimum required width for the tournament layout.",
                        Current = height,
                    }),
                    new SettingsButtonV2
                    {
                        Text = "Apply window height",
                        Action = apply,
                    },
                },
            };

            numberBox.OnCommit += (_, _) => apply();
        }

        private void apply()
        {
            if (string.IsNullOrEmpty(height.Value))
                return;

            if (!int.TryParse(height.Value, out int number))
            {
                // The only realistic failure here is an input too large to parse as an int.
                number = maximum_window_height;
            }
            else
            {
                number = Math.Clamp(number, minimum_window_height, maximum_window_height);
            }

            height.Value = number.ToString();
            Action?.Invoke(number);
        }
    }
}
