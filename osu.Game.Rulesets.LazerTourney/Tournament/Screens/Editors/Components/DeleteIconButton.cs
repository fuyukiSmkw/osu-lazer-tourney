// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Editors.Components
{
    /// <summary>
    /// A compact minus-icon button for destructive actions, using the dangerous button colour instead of
    /// <see cref="GrayButton"/>'s grey.
    /// </summary>
    public partial class DeleteIconButton : GrayButton
    {
        public DeleteIconButton()
            : base(FontAwesome.Solid.MinusSquare)
        {
            Size = new Vector2(30, 30);
            Margin = new MarginPadding
            {
                Left = SettingsPanel.CONTENT_MARGINS,
                Right = SettingsPanel.CONTENT_MARGINS,
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Background.Colour = colours.DangerousButtonColour;
        }
    }
}
