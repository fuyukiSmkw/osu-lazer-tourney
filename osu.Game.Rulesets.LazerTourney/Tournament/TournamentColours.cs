// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics.Colour;
using osu.Game.Graphics;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament
{
    /// <summary>
    /// Shared colour definitions used by the tournament screens.
    /// </summary>
    public static class TournamentColours
    {
        public static ColourInfo GetTeamColour(TeamColour teamColour) => teamColour == TeamColour.Red ? COLOUR_RED : COLOUR_BLUE;

        public static ColourInfo GetTeamNameColour(TeamColour teamColour) => teamColour == TeamColour.Red ? NAME_COLOUR_RED : NAME_COLOUR_BLUE;

        public static ColourInfo GetCombobreakNameColour() => NAME_COLOUR_COMBOBREAK;

        public static readonly Color4 COLOUR_RED = new OsuColour().TeamColourRed;
        public static readonly Color4 COLOUR_BLUE = new OsuColour().TeamColourBlue;

        public static readonly Color4 NAME_COLOUR_RED = new OsuColour().RedLight;
        public static readonly Color4 NAME_COLOUR_BLUE = new OsuColour().BlueLight;
        public static readonly Color4 NAME_COLOUR_COMBOBREAK = new OsuColour().Red2;

        public static readonly Color4 ELEMENT_BACKGROUND_COLOUR = Color4Extensions.FromHex("#fff");
        public static readonly Color4 ELEMENT_FOREGROUND_COLOUR = Color4Extensions.FromHex("#000");

        public static readonly Color4 TEXT_COLOUR = Color4Extensions.FromHex("#fff");
    }
}
