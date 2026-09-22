// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Models
{
    public class RoundBeatmap
    {
        public int ID;

        /// <summary>
        /// Category label of this beatmap (e.g. "HD", "DT"). Used to look up custom <c>Mods/{label}</c> images.
        /// This is NOT related to gameplay mods below.
        /// </summary>
        public string Mods = string.Empty;

        [JsonProperty("BeatmapInfo")]
        public TournamentBeatmap? Beatmap;

        /// <summary>
        /// Whether players may pick their own mods on this beatmap.
        /// Mirrors <see cref="osu.Game.Online.Rooms.PlaylistItem.Freestyle"/>.
        /// </summary>
        public bool Freestyle;

        /// <summary>
        /// Mods every player must use on this beatmap.
        /// Mirrors <see cref="osu.Game.Online.Rooms.PlaylistItem.RequiredMods"/>.
        /// Null when loaded from an old bracket without these fields (normalised on load).
        /// </summary>
        public APIMod[]? RequiredMods;

        /// <summary>
        /// Mods players may additionally choose from on this beatmap (free mods).
        /// Mirrors <see cref="osu.Game.Online.Rooms.PlaylistItem.AllowedMods"/>.
        /// Null when loaded from an old bracket without these fields (normalised on load).
        /// An explicitly empty array with <see cref="Freestyle"/> disabled means no free mods.
        /// </summary>
        public APIMod[]? AllowedMods;

        /// <summary>
        /// Null-safe accessor for <see cref="RequiredMods"/>.
        /// </summary>
        [JsonIgnore]
        public APIMod[] SafeRequiredMods => RequiredMods ?? Array.Empty<APIMod>();

        /// <summary>
        /// Null-safe accessor for <see cref="AllowedMods"/>.
        /// </summary>
        [JsonIgnore]
        public APIMod[] SafeAllowedMods => AllowedMods ?? Array.Empty<APIMod>();

        /// <summary>
        /// Whether a live room playlist item represents this round map.
        /// Requires full equality (beatmap, ruleset, freestyle and mods).
        /// </summary>
        public static bool Matches(MultiplayerPlaylistItem item, RoundBeatmap map, int? rulesetId)
            => item.BeatmapID == map.ID
               && (rulesetId == null || item.RulesetID == rulesetId)
               && item.Freestyle == map.Freestyle
               && item.RequiredMods.SequenceEqual(map.SafeRequiredMods)
               && item.AllowedMods.SequenceEqual(map.SafeAllowedMods);

        /// <summary>
        /// Lists every mod valid as a free mod for the given ruleset.
        /// Same validity gate as lazer room creation, used as the default free-mod list.
        /// </summary>
        public static APIMod[] GetAllFreeMods(Ruleset ruleset)
            => ruleset.AllMods
                      .OfType<Mod>()
                      .Where(m => ModUtils.IsValidModForMatch(m, false, MatchType.HeadToHead, false))
                      .Select(m => new APIMod(m))
                      .ToArray();

        /// <summary>
        /// Converts stored <see cref="APIMod"/>s to live mods, dropping unknown ones.
        /// </summary>
        public static Mod[] InstantiateMods(APIMod[] apiMods, Ruleset ruleset)
            => ModUtils.InstantiateValidModsForRuleset(ruleset, apiMods, out List<Mod> valid)
                ? valid.ToArray()
                : Array.Empty<Mod>();

        /// <summary>
        /// Drops required mods that may not stay required in freestyle
        /// (e.g. HD, which fails <c>ValidForFreestyleAsRequiredMod</c>).
        /// </summary>
        public static APIMod[] FilterFreestyleRequiredMods(APIMod[] apiMods, Ruleset ruleset)
            => InstantiateMods(apiMods, ruleset)
                    .Where(m => ModUtils.IsValidModForMatch(m, true, MatchType.HeadToHead, true))
                .Select(m => new APIMod(m))
                .ToArray();
    }
}
