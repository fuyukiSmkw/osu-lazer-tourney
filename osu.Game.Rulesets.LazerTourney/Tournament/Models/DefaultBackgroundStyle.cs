// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Models
{
    /// <summary>
    /// The background shown when the main tournament video is unavailable.
    /// </summary>
    public enum DefaultBackgroundStyle
    {
        [Description("Default gray gradient")]
        DefaultGrayGradient,

        [Description("Green screen")]
        GreenScreen,

        [Description("Custom colors")]
        CustomColors,
    }

    /// <summary>
    /// Default and preset colours for <see cref="DefaultBackgroundStyle"/>.
    /// </summary>
    public static class DefaultBackgroundColours
    {
        public const string GRAY_TOP = "#4d4d4d";
        public const string GRAY_BOTTOM = "#999999";
        public const string GREEN_SCREEN = "#00ff00";
    }
}
