// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using osu.Framework.Bindables;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Models
{
    /// <summary>
    /// Holds the complete data required to operate the tournament system.
    /// </summary>
    [Serializable]
    public class LadderInfo
    {
        public Bindable<RulesetInfo?> Ruleset = new Bindable<RulesetInfo?>();

        public BindableList<TournamentMatch> Matches = new BindableList<TournamentMatch>();
        public BindableList<TournamentRound> Rounds = new BindableList<TournamentRound>();
        public BindableList<TournamentTeam> Teams = new BindableList<TournamentTeam>();

        // only used for serialisation
        public List<TournamentProgression> Progressions = new List<TournamentProgression>();

        /// <summary>
        /// Visual and audio gameplay settings for the tournament. Null for brackets saved
        /// before this object existed; the built-in defaults apply in that case.
        /// </summary>
        public VisualAudioSettings? VisualAudioSettings;

        /// <summary>
        /// Per-ruleset gameplay settings for the tournament. Null for brackets saved
        /// before this object existed; the built-in defaults apply in that case.
        /// </summary>
        public RulesetSettings? RulesetSettings;

        [JsonIgnore] // updated manually in TournamentGameBase
        public Bindable<TournamentMatch?> CurrentMatch = new Bindable<TournamentMatch?>();

        public Bindable<int> ChromaKeyWidth = new BindableInt(1366)
        {
            MinValue = 640,
            MaxValue = 1366,
        };

        public Bindable<int> PlayersPerTeam = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 4,
        };

        public Bindable<bool> AutoProgressScreens = new BindableBool(true);

        public Bindable<bool> SplitMapPoolByMods = new BindableBool(true);

        public Bindable<bool> DisplayTeamSeeds = new BindableBool();

        /// <summary>
        /// Which result screen implementation is shown after a play or replay finishes.
        /// Defaults to <see cref="ResultScreenStyle.Simple"/> so existing tournaments keep
        /// their current results layout when this setting is missing from the bracket file.
        /// </summary>
        [DefaultValue(ResultScreenStyle.LazerDefault)]
        public Bindable<ResultScreenStyle> ResultScreen = new Bindable<ResultScreenStyle>(ResultScreenStyle.LazerDefault);

        /// <summary>
        /// The background shown when the main tournament video is unavailable.
        /// </summary>
        [DefaultValue(DefaultBackgroundStyle.DefaultGrayGradient)]
        public Bindable<DefaultBackgroundStyle> DefaultBackground = new Bindable<DefaultBackgroundStyle>(DefaultBackgroundStyle.DefaultGrayGradient);

        /// <summary>
        /// Top colour used by <see cref="DefaultBackgroundStyle.CustomColors"/>, in <c>#rrggbb</c> format.
        /// </summary>
        [DefaultValue(DefaultBackgroundColours.GRAY_TOP)]
        public Bindable<string> DefaultBackgroundTopColour = new Bindable<string>(DefaultBackgroundColours.GRAY_TOP);

        /// <summary>
        /// Bottom colour used by <see cref="DefaultBackgroundStyle.CustomColors"/>, in <c>#rrggbb</c> format.
        /// </summary>
        [DefaultValue(DefaultBackgroundColours.GRAY_BOTTOM)]
        public Bindable<string> DefaultBackgroundBottomColour = new Bindable<string>(DefaultBackgroundColours.GRAY_BOTTOM);

        /// <summary>
        /// Whether each gameplay spectate cell shows the player's username.
        /// </summary>
        [DefaultValue(true)]
        public BindableBool ShowPlayerUsernames = new BindableBool(true);

        /// <summary>
        /// Background colour for red team elements, in <c>#rrggbb</c> format.
        /// </summary>
        [DefaultValue(TournamentColours.DEFAULT_TEAM_COLOUR_RED)]
        public Bindable<string> TeamColourRed = new Bindable<string>(TournamentColours.DEFAULT_TEAM_COLOUR_RED);

        /// <summary>
        /// Username text colour for red team players, in <c>#rrggbb</c> format.
        /// </summary>
        [DefaultValue(TournamentColours.DEFAULT_TEAM_NAME_COLOUR_RED)]
        public Bindable<string> TeamNameColourRed = new Bindable<string>(TournamentColours.DEFAULT_TEAM_NAME_COLOUR_RED);

        /// <summary>
        /// Background colour for blue team elements, in <c>#rrggbb</c> format.
        /// </summary>
        [DefaultValue(TournamentColours.DEFAULT_TEAM_COLOUR_BLUE)]
        public Bindable<string> TeamColourBlue = new Bindable<string>(TournamentColours.DEFAULT_TEAM_COLOUR_BLUE);

        /// <summary>
        /// Username text colour for blue team players, in <c>#rrggbb</c> format.
        /// </summary>
        [DefaultValue(TournamentColours.DEFAULT_TEAM_NAME_COLOUR_BLUE)]
        public Bindable<string> TeamNameColourBlue = new Bindable<string>(TournamentColours.DEFAULT_TEAM_NAME_COLOUR_BLUE);

        /// <summary>
        /// Whether map pool panels show the category mod image (from RoundBeatmap.Mods).
        /// </summary>
        [DefaultValue(true)]
        public BindableBool DisplayCategoryModIcon = new BindableBool(true);

        /// <summary>
        /// Whether map pool panels show the required gameplay mods.
        /// </summary>
        public BindableBool DisplayRequiredMods = new BindableBool();
    }
}
