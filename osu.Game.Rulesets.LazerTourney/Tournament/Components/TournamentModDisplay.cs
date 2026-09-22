// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.Containers;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    public partial class TournamentModDisplay : ReverseChildIDFillFlowContainer<ModIcon>, IHasCurrentValue<IReadOnlyList<Mod>>
    {
        public const float MOD_ICON_SCALE = 0.5f;

        private readonly BindableWithCurrent<IReadOnlyList<Mod>> current = new BindableWithCurrent<IReadOnlyList<Mod>>(Array.Empty<Mod>());

        public Bindable<IReadOnlyList<Mod>> Current
        {
            get => current.Current;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                current.Current = value;
            }
        }

        public FillDirection FillDirection
        {
            get => Direction;
            set => Direction = value;
        }

        public TournamentModDisplay()
        {
            AutoSizeAxes = Axes.Both;
            Direction = FillDirection.Horizontal;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindValueChanged(updateDisplay, true);
            updateExpansionMode(0);
        }

        private void updateDisplay(ValueChangedEvent<IReadOnlyList<Mod>> mods)
        {
            Clear();

            foreach (Mod mod in mods.NewValue.AsOrdered())
                Add(new ModIcon(mod, showExtendedInformation: true)
                {
                    Scale = new Vector2(MOD_ICON_SCALE),
                });
        }

        private void updateExpansionMode(double duration = 500) => ((FillFlowContainer<ModIcon>)this).TransformSpacingTo(new Vector2(-25), duration, Easing.OutQuint); // contract
    }
}
