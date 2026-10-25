// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Models.ModMultiplier
{
    /// <summary>
    /// A compiled custom mod-multiplier rule ("ModMult").
    /// <para>
    /// Language overview (loosely C/JavaScript-like): a rule is a single expression over the enabled mods
    /// of one score and their setting values, evaluating to a non-negative number. A bare mod acronym tests
    /// presence (<c>EZ ? 2</c> means "x2 with EZ, x1 otherwise" — a missing ternary else defaults to 1).
    /// <c>MOD.setting</c> reads a setting value (null when absent; use <c>??</c> for a fallback).
    /// <c>has/all/only</c> test mod combinations (<c>only</c> matches the exact set, ignoring settings).
    /// Numeric helpers: <c>count, min, max, clamp, abs, floor, ceil, round</c>.
    /// </para>
    /// </summary>
    public sealed class ModMultRule
    {
        /// <summary>
        /// The identity rule (always x1), used for empty sources.
        /// </summary>
        public static readonly ModMultRule Empty = new ModMultRule(string.Empty, new ModMultLiteral(ModMultValue.FromNumber(1)));

        /// <summary>
        /// The original source text of this rule.
        /// </summary>
        public string Source { get; }

        private readonly ModMultExpression expression;

        private ModMultRule(string source, ModMultExpression expression)
        {
            Source = source;
            this.expression = expression;
        }

        /// <summary>
        /// Parses a rule without validating mod/setting names. Empty sources yield <see cref="Empty"/>.
        /// </summary>
        /// <returns>False with a user-facing error message when the source is syntactically invalid.</returns>
        public static bool TryParse(string? source, out ModMultRule? rule, out string? error)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                rule = Empty;
                error = null;
                return true;
            }

            try
            {
                rule = new ModMultRule(source, ModMultParser.Parse(source));
                error = null;
                return true;
            }
            catch (ModMultParseException ex)
            {
                rule = null;
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Validates referenced mod acronyms and setting names against the given ruleset mods.
        /// </summary>
        /// <returns>Null when valid, otherwise a user-facing error message.</returns>
        public string? Validate(IReadOnlyList<Mod> allMods)
        {
            var mods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var settings = new List<(string mod, string setting)>();
            expression.CollectReferences(mods, settings);

            var knownMods = allMods
                            .Where(m => m is not UnknownMod && !string.IsNullOrEmpty(m.Acronym))
                            .GroupBy(m => m.Acronym, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (string acronym in mods)
            {
                if (!knownMods.ContainsKey(acronym))
                    return $"Unknown mod \"{acronym}\".";
            }

            foreach ((string mod, string setting) in settings)
            {
                if (!knownMods.TryGetValue(mod, out var modInstance))
                    continue;

                var available = modInstance.GetSettingsSourceProperties()
                                           .Select(p => p.Item2.Name)
                                           .ToList();
                string normalised = ModMultContext.NormaliseKey(setting);

                if (available.All(name => !string.Equals(ModMultContext.NormaliseKey(name), normalised, StringComparison.Ordinal)))
                {
                    string known = available.Count == 0
                        ? $"\"{mod}\" has no customisable settings"
                        : $"available settings for \"{mod}\": {string.Join(", ", available.Select(name => name.ToSnakeCase()))}";
                    return $"Unknown setting \"{setting}\" on mod \"{mod}\" ({known}).";
                }
            }

            return null;
        }

        /// <summary>
        /// Evaluates this rule for one score's mods. Never throws: any failure falls back to x1.
        /// </summary>
        public double Evaluate(IReadOnlyList<Mod> mods)
        {
            try
            {
                var result = expression.Evaluate(ModMultContext.FromMods(mods));

                if (!result.IsNumber || double.IsNaN(result.Number) || double.IsInfinity(result.Number))
                {
                    Logger.Log($"ModMult rule did not produce a number (\"{Source}\"). Falling back to x1.", LoggingTarget.Runtime, LogLevel.Debug);
                    return 1;
                }

                if (result.Number < 0)
                {
                    Logger.Log($"ModMult rule produced a negative multiplier ({result.Number}, \"{Source}\"). Falling back to x1.", LoggingTarget.Runtime, LogLevel.Debug);
                    return 1;
                }

                return result.Number;
            }
            catch (ModMultRuntimeException ex)
            {
                Logger.Log($"ModMult rule failed ({ex.Message} \"{Source}\"). Falling back to x1.", LoggingTarget.Runtime, LogLevel.Debug);
                return 1;
            }
            catch (Exception ex)
            {
                Logger.Log($"ModMult rule failed unexpectedly ({ex.Message} \"{Source}\"). Falling back to x1.", LoggingTarget.Runtime, LogLevel.Debug);
                return 1;
            }
        }

        /// <summary>
        /// Resolves the effective rule source for a beatmap: its own rule when non-empty, otherwise the round default.
        /// </summary>
        public static string ResolveSource(string? beatmapRule, string? roundRule)
            => string.IsNullOrWhiteSpace(beatmapRule) ? roundRule ?? string.Empty : beatmapRule;
    }
}
