// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24,
// via the GooGuTeam/g0v0 fork.

using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.UI.Results.Legacy
{
    /// <summary>
    /// Font provider for the legacy result screen chrome. Numbers in the score panel use the
    /// skin's legacy score font via <see cref="osu.Game.Skinning.LegacySpriteText"/> instead.
    /// </summary>
    internal static class LegacyFonts
    {
        public static FontUsage Get(float size, FontWeight weight = FontWeight.Regular)
            => OsuFont.GetFont(size: size, weight: weight);
    }
}
