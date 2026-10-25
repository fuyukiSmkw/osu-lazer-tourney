// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Localisation;

namespace osu.Game.Rulesets.LazerTourney.Tournament.UI.Results.V2
{
    /// <summary>
    /// Strings used by the V2 result screen. Kept local to the tournament plugin so the
    /// V2 implementation does not depend on unreleased upstream localisation additions.
    /// </summary>
    public static class V2ResultsStrings
    {
        private const string prefix = @"osu.Game.Rulesets.LazerTourney.Resources.Localisation.V2Results";

        public static LocalisableString LeaderboardAndDetails => new TranslatableString(getKey(@"leaderboard_and_details"), @"Leaderboard / More info");

        public static LocalisableString ScoreOverview => new TranslatableString(getKey(@"score_overview"), @"Score overview");

        public static LocalisableString MoreInfo => new TranslatableString(getKey(@"more_info"), @"More info");

        public static LocalisableString PlayAgain => new TranslatableString(getKey(@"play_again"), @"Play again");

        public static LocalisableString Screenshot => new TranslatableString(getKey(@"screenshot"), @"Screenshot");

        public static LocalisableString UploadScreenshotShort => new TranslatableString(getKey(@"upload_screenshot_short"), @"Upload SS");

        public static LocalisableString MappedBy(string mapper) => new TranslatableString(getKey(@"mapped_by"), @"by {0}", mapper);

        public static LocalisableString AchievedOn(string date) => new TranslatableString(getKey(@"achieved_on"), @"Achieved on {0}", date);

        public static LocalisableString Mods => new TranslatableString(getKey(@"mods"), @"MODS");

        public static LocalisableString NoMods => new TranslatableString(getKey(@"no_mods"), @"No mods");

        public static LocalisableString NoLeaderboardScores => new TranslatableString(getKey(@"no_leaderboard_scores"), @"Set a score on this beatmap to appear here.");

        public static LocalisableString ReplayUnavailable => new TranslatableString(getKey(@"replay_unavailable"), @"Replay unavailable");

        public static LocalisableString DownloadingReplay => new TranslatableString(getKey(@"downloading_replay"), @"Downloading replay…");

        public static LocalisableString RetryUnavailable => new TranslatableString(getKey(@"retry_unavailable"), @"Retry is available after playing this beatmap.");

        public static LocalisableString PersonalBest => new TranslatableString(getKey(@"personal_best"), @"Personal best");

        public static LocalisableString Combo => new TranslatableString(getKey(@"combo"), @"Combo");

        public static LocalisableString FeaturedArtist => new TranslatableString(getKey(@"featured_artist"), @"FEATURED ARTIST");

        public static LocalisableString IfFullCombo(double pp) => new TranslatableString(getKey(@"if_full_combo"), @"if FC {0}pp", pp);

        public static LocalisableString IfPerfect(double pp) => new TranslatableString(getKey(@"if_ss"), @"if SS {0}pp", pp);

        public static LocalisableString IfPerfectHidden(double pp) => new TranslatableString(getKey(@"if_ssh"), @"if SSH {0}pp", pp);

        public static LocalisableString IfFullComboTooltip => new TranslatableString(getKey(@"if_full_combo_tooltip"), @"PP you can gain if you achieve FC. Click to switch SS case.");

        public static LocalisableString IfPerfectTooltip => new TranslatableString(getKey(@"if_ss_tooltip"), @"PP you can gain if you achieve SS.");

        public static LocalisableString IfPerfectHiddenTooltip => new TranslatableString(getKey(@"if_ssh_tooltip"), @"PP you can gain if you achieve SSH.");

        public static LocalisableString ClickToFullComboTooltip => new TranslatableString(getKey(@"click_to_full_combo_tooltip"), @"Click to switch to FC case.");

        public static LocalisableString NoPPForUnrankedBeatmaps => new TranslatableString(getKey(@"no_pp_for_unranked_beatmaps"), @"Performance points are not granted for this score because the beatmap is not ranked.");

        public static LocalisableString NoPPForUnrankedMods => new TranslatableString(getKey(@"no_pp_for_unranked_mods"), @"Performance points are not granted for this score because of unranked mods.");

        public static LocalisableString NoPPForFailedScores => new TranslatableString(getKey(@"no_pp_for_failed_scores"), @"Performance points are not granted for failed scores.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
