// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Models
{
    /// <summary>
    /// Which result screen implementation is shown after a play or replay finishes.
    /// Implementations live under <c>Tournament/UI/Results/&lt;name&gt;</c>.
    /// </summary>
    public enum ResultScreenStyle
    {
        /// <summary>
        /// lazer's built-in result screen for the current player type
        /// (<c>MultiSpectatorResultsScreen</c> for spectated cells, <c>SoloResultsScreen</c> for replays).
        /// </summary>
        [Description("lazer default")]
        LazerDefault,

        /// <summary>
        /// Static single-score layout without leaderboard or bottom bar, by fuyukiS.
        /// </summary>
        [Description("Simple result (by fuyukiS)")]
        Simple,

        /// <summary>
        /// The V2 overview design originally developed in the g0v0 client fork.
        /// </summary>
        [Description("V2 result (by GooGuTeam)")]
        V2,
    }
}
