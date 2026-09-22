// Copyright (c) 2026 fuyukiS <fuyukiS@outlook.jp>. Licensed under the MIT License.
// See the LICENCE file in the repository root for full licence text.
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Graphics;

public partial class LazerTourneyIcon : CompositeDrawable
{
    public LazerTourneyIcon()
    {
        InternalChildren =
        [
            new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(20),
                Icon = FontAwesome.Regular.Circle,
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(10),
                Icon = FontAwesome.Solid.FlagCheckered,
                Colour = Color4.White,
            },
        ];
    }
}
