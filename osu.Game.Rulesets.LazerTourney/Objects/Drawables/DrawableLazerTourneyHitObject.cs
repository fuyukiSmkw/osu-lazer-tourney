// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Rulesets.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Objects.Drawables
{
    public partial class DrawableLazerTourneyHitObject : DrawableHitObject<LazerTourneyHitObject>
    {
        public DrawableLazerTourneyHitObject(LazerTourneyHitObject hitObject)
            : base(hitObject)
        {
            Size = new Vector2(40);
            Origin = Anchor.Centre;
            // todo: add visuals.
        }
    }
}
