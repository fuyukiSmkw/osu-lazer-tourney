// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Models
{
    /// <summary>
    /// Detects mappool entries that the currently selected ruleset cannot load or process.
    /// This happens when a bracket is reused with another ruleset: stored beatmaps still
    /// belong to the old ruleset (or carry no usable ruleset info at all, e.g. old
    /// <c>APIRuleset</c> entries with <c>OnlineID = -1</c>), which makes ruleset-specific
    /// difficulty processing throw during startup.
    /// </summary>
    public static class RoundBeatmapValidator
    {
        /// <summary>
        /// Checks whether a round beatmap can be processed by the given ruleset.
        /// </summary>
        /// <param name="beatmap">The mappool entry to check.</param>
        /// <param name="ruleset">The currently selected ruleset instance.</param>
        /// <param name="reason">A human-readable reason when the entry is invalid.</param>
        /// <returns>Whether the entry is invalid for the ruleset.</returns>
        public static bool TryGetInvalidReason(RoundBeatmap beatmap, Ruleset ruleset, out string reason)
        {
            reason = string.Empty;

            // Empty slots are legitimate (newly added or cleared by this validator).
            if (beatmap.Beatmap == null)
                return false;

            string? beatmapRuleset = tryGetRulesetShortName(beatmap.Beatmap);

            if (beatmapRuleset == null)
            {
                // The stored ruleset info is unusable. Some rulesets still process such beatmaps
                // (osu! ignores the source ruleset), while others (mania conversions) throw, so
                // probe the actual processing path instead of rejecting the entry outright.
                if (!canProcessDifficulty(beatmap.Beatmap, ruleset))
                {
                    reason = $@"its stored ruleset is unavailable and {ruleset.ShortName} cannot process its difficulty data";
                    return true;
                }
            }
            else if (!string.Equals(beatmapRuleset, ruleset.ShortName, StringComparison.Ordinal))
            {
                reason = $@"it belongs to {beatmapRuleset}, not {ruleset.ShortName}";
                return true;
            }

            if (!canProcessDifficulty(beatmap.Beatmap, ruleset))
            {
                reason = $@"{ruleset.ShortName} cannot process its difficulty data";
                return true;
            }

            if (hasUnknownMods(beatmap.SafeRequiredMods, ruleset))
            {
                reason = $@"it requires mods that do not exist in {ruleset.ShortName}";
                return true;
            }

            if (hasUnknownMods(beatmap.SafeAllowedMods, ruleset))
            {
                reason = $@"it allows mods that do not exist in {ruleset.ShortName}";
                return true;
            }

            return false;
        }

        /// <summary>
        /// Clears a round beatmap's beatmap data while keeping its slot in the round,
        /// so the round editor keeps its layout and the user can refill the entry.
        /// </summary>
        public static void ClearBeatmapData(RoundBeatmap beatmap)
        {
            // The ID must be cleared as well, otherwise the next load would repopulate the
            // invalid beatmap from the API and prompt again.
            beatmap.ID = 0;
            beatmap.Beatmap = null;
            beatmap.Freestyle = false;
            beatmap.RequiredMods = Array.Empty<APIMod>();
            beatmap.AllowedMods = Array.Empty<APIMod>();
        }

        /// <summary>
        /// Formats a round beatmap as <c>Artist - Title [Difficulty]</c> for user-facing messages.
        /// </summary>
        public static string Describe(RoundBeatmap beatmap)
        {
            TournamentBeatmap? info = beatmap.Beatmap;

            if (info == null)
                return beatmap.ID > 0 ? $@"beatmap #{beatmap.ID}" : "empty beatmap slot";

            string artist = info.Metadata?.Artist ?? string.Empty;
            string title = info.Metadata?.Title ?? string.Empty;
            string description = $@"{artist} - {title}".Trim(' ', '-');

            if (!string.IsNullOrEmpty(info.DifficultyName))
                description = $@"{description} [{info.DifficultyName}]";

            return description.Length > 0 ? description : $@"beatmap #{info.OnlineID}";
        }

        private static string? tryGetRulesetShortName(TournamentBeatmap beatmap)
        {
            try
            {
                string? shortName = beatmap.Ruleset?.ShortName;
                return string.IsNullOrEmpty(shortName) ? null : shortName;
            }
            catch (Exception)
            {
                // Older APIRuleset entries can carry OnlineID values outside 0..3,
                // which makes ShortName throw instead of returning null.
                return null;
            }
        }

        private static bool canProcessDifficulty(TournamentBeatmap beatmap, Ruleset ruleset)
        {
            try
            {
                ruleset.GetAdjustedDisplayDifficulty(beatmap, Array.Empty<Mod>());
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool hasUnknownMods(APIMod[] mods, Ruleset ruleset)
        {
            foreach (APIMod? mod in mods)
            {
                if (mod == null || string.IsNullOrEmpty(mod.Acronym) || ruleset.CreateModFromAcronym(mod.Acronym) == null)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// One mappool entry discovered as invalid for the selected ruleset during bracket load.
    /// </summary>
    public sealed class InvalidRoundBeatmap
    {
        /// <summary>
        /// The round containing the invalid entry.
        /// </summary>
        public readonly TournamentRound Round;

        /// <summary>
        /// The invalid entry itself. Clearing it (see <see cref="RoundBeatmapValidator.ClearBeatmapData"/>)
        /// keeps the slot inside <see cref="Round"/>.
        /// </summary>
        public readonly RoundBeatmap Beatmap;

        /// <summary>
        /// The beatmap formatted as <c>Artist - Title [Difficulty]</c> for display.
        /// </summary>
        public readonly string DisplayName;

        /// <summary>
        /// Why the entry cannot be used with the current ruleset.
        /// </summary>
        public readonly string Reason;

        public InvalidRoundBeatmap(TournamentRound round, RoundBeatmap beatmap, string displayName, string reason)
        {
            Round = round;
            Beatmap = beatmap;
            DisplayName = displayName;
            Reason = reason;
        }
    }
}
