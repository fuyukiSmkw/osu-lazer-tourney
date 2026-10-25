// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Models.ModMultiplier
{
    /// <summary>
    /// The evaluation input of a ModMult rule: the enabled mods of one score and their setting values.
    /// Acronyms are case-insensitive; setting names are case-insensitive with optional underscores
    /// (e.g. <c>speed_change</c>, <c>SpeedChange</c> and <c>speedchange</c> are equivalent).
    /// </summary>
    public sealed class ModMultContext
    {
        private readonly HashSet<string> enabledAcronyms;
        private readonly Dictionary<string, Dictionary<string, ModMultValue>> settings;

        private ModMultContext(HashSet<string> enabledAcronyms, Dictionary<string, Dictionary<string, ModMultValue>> settings)
        {
            this.enabledAcronyms = enabledAcronyms;
            this.settings = settings;
        }

        /// <summary>
        /// Builds a context from live mod instances. Setting values are read through the public
        /// <see cref="IConfiguredMod.Settings"/> (snake_case keys) so no internal API is required.
        /// </summary>
        public static ModMultContext FromMods(IReadOnlyList<Mod> mods)
        {
            var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var settings = new Dictionary<string, Dictionary<string, ModMultValue>>(StringComparer.OrdinalIgnoreCase);

            foreach (var mod in mods)
            {
                string acronym = mod is UnknownMod unknown ? unknown.OriginalAcronym : mod.Acronym;

                if (string.IsNullOrEmpty(acronym))
                    continue;

                acronym = acronym.ToUpperInvariant();
                enabled.Add(acronym);

                var modSettings = new Dictionary<string, ModMultValue>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    // Note: Mod itself does not implement IConfiguredMod (only APIMod does),
                    // so settings are read through the same public reflection path the APIMod constructor uses.
                    if (mod is IConfiguredMod configured)
                    {
                        foreach (var pair in configured.Settings)
                            modSettings[NormaliseKey(pair.Key)] = toValue(pair.Value);
                    }
                    else
                    {
                        foreach ((_, var property) in mod.GetSettingsSourceProperties())
                        {
                            if (property.GetValue(mod) is not IBindable bindable)
                                continue;

                            modSettings[NormaliseKey(property.Name.ToSnakeCase())] = toValue(bindable.GetUnderlyingSettingValue());
                        }
                    }
                }
                catch
                {
                    // A mod with unreadable settings still counts as enabled; its settings read as null.
                }

                settings[acronym] = modSettings;
            }

            return new ModMultContext(enabled, settings);
        }

        /// <summary>
        /// Normalises a setting name for lookup: lower-case with underscores removed.
        /// </summary>
        public static string NormaliseKey(string name)
            => name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

        public int ModCount => enabledAcronyms.Count;

        public IEnumerable<string> EnabledAcronyms => enabledAcronyms;

        public bool IsModEnabled(string acronym) => enabledAcronyms.Contains(acronym);

        public bool HasAny(IReadOnlyList<string> acronyms)
        {
            foreach (string acronym in acronyms)
            {
                if (enabledAcronyms.Contains(acronym))
                    return true;
            }

            return false;
        }

        public bool HasAll(IReadOnlyList<string> acronyms)
        {
            foreach (string acronym in acronyms)
            {
                if (!enabledAcronyms.Contains(acronym))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Whether the enabled mod set is exactly the given set, ignoring settings.
        /// </summary>
        public bool HasOnly(IReadOnlyList<string> acronyms)
        {
            if (enabledAcronyms.Count != acronyms.Count)
                return false;

            return HasAll(acronyms);
        }

        /// <summary>
        /// Reads a setting value. Returns null when the mod is absent or the setting is unset/unknown.
        /// </summary>
        public ModMultValue GetSetting(string acronym, string setting)
        {
            if (!settings.TryGetValue(acronym, out var modSettings))
                return ModMultValue.Null;

            if (!modSettings.TryGetValue(NormaliseKey(setting), out var value))
                return ModMultValue.Null;

            return value;
        }

        private static ModMultValue toValue(object? value) => value switch
        {
            null => ModMultValue.Null,
            bool b => ModMultValue.FromBoolean(b),
            string s => ModMultValue.FromText(s),
            Enum e => ModMultValue.FromText(e.ToString()),
            IConvertible => ModMultValue.FromNumber(convertNumber(value)),
            _ => ModMultValue.FromText(value.ToString() ?? string.Empty),
        };

        private static double convertNumber(object value)
        {
            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return double.NaN;
            }
        }
    }
}
