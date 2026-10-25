// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
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
        public const string DEFAULT_TEAM_COLOUR_RED = "#aa1414";
        public const string DEFAULT_TEAM_COLOUR_BLUE = "#1462aa";
        public const string DEFAULT_TEAM_NAME_COLOUR_RED = "#ed7787";
        public const string DEFAULT_TEAM_NAME_COLOUR_BLUE = "#99eeff";

        public static ColourInfo GetTeamColour(TeamColour teamColour, LadderInfo? ladderInfo = null)
            => parseColour(
                teamColour == TeamColour.Red ? ladderInfo?.TeamColourRed.Value : ladderInfo?.TeamColourBlue.Value,
                teamColour == TeamColour.Red ? COLOUR_RED : COLOUR_BLUE);

        public static ColourInfo GetTeamNameColour(TeamColour teamColour, LadderInfo? ladderInfo = null)
            => parseColour(
                teamColour == TeamColour.Red ? ladderInfo?.TeamNameColourRed.Value : ladderInfo?.TeamNameColourBlue.Value,
                teamColour == TeamColour.Red ? NAME_COLOUR_RED : NAME_COLOUR_BLUE);

        public static ColourInfo GetCombobreakNameColour() => NAME_COLOUR_COMBOBREAK;

        public static readonly Color4 COLOUR_RED = new OsuColour().TeamColourRed;
        public static readonly Color4 COLOUR_BLUE = new OsuColour().TeamColourBlue;

        public static readonly Color4 NAME_COLOUR_RED = new OsuColour().RedLight;
        public static readonly Color4 NAME_COLOUR_BLUE = new OsuColour().BlueLight;
        public static readonly Color4 NAME_COLOUR_COMBOBREAK = new OsuColour().Red2;

        private static Colour4 parseColour(string? value, Colour4 fallback)
        {
            if (value != null && value.Length == 7 && value[0] == '#' && Colour4.TryParseHex(value, out Colour4 colour))
                return colour;

            return fallback;
        }

        public static readonly Color4 ELEMENT_BACKGROUND_COLOUR = Color4Extensions.FromHex("#fff");
        public static readonly Color4 ELEMENT_FOREGROUND_COLOUR = Color4Extensions.FromHex("#000");

        public static readonly Color4 TEXT_COLOUR = Color4Extensions.FromHex("#fff");
    }
}
