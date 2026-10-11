// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Setup
{
    internal partial class LabelledTextBox : LabelledComponent<OsuTextBox, string>
    {
        public LabelledTextBox(bool padded)
            : base(padded)
        {
        }

        protected override OsuTextBox CreateComponent() => new OsuTextBox
        {
            RelativeSizeAxes = Axes.X,
            Height = 40,
            ReleaseFocusOnCommit = false,
        };
    }
}
