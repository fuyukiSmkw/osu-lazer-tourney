// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Models
{
    /// <summary>
    /// The kind of value a ruleset setting holds. Decides which control is built for it.
    /// </summary>
    public enum RulesetSettingKind
    {
        Bool,
        Enum,
        Double,
    }

    /// <summary>
    /// Describes one per-ruleset gameplay setting: how to find it on a ruleset config
    /// manager, how to convert it to and from the bracket model, and how to label it.
    /// Everything here is strings, primitives and our own model types only, so touching
    /// this table never binds the official ruleset assemblies.
    /// </summary>
    public sealed class RulesetSettingDescriptor
    {
        /// <summary>
        /// Member name on the ruleset's lookup enum (e.g. SnakingInSliders).
        /// </summary>
        public string SettingName = string.Empty;

        public RulesetSettingKind Kind;

        public string Label = string.Empty;

        /// <summary>
        /// Keyboard step for double sliders. Unused by other kinds.
        /// </summary>
        public float SliderStep;

        /// <summary>
        /// Assembly-qualified value enum type name. Required for <see cref="RulesetSettingKind.Enum"/>,
        /// unused by other kinds (their value types are known statically).
        /// Resolved only when the ruleset's own assembly is already loaded.
        /// </summary>
        public string? ValueTypeName;

        public Func<RulesetSettings, object?> GetModelValue = _ => null;
        public Action<RulesetSettings, object?> SetModelValue = (_, __) => { };
    }

    /// <summary>
    /// The single source of truth for covered per-ruleset settings, plus the reflection
    /// helpers to read and write them without ever naming official ruleset types.
    /// Enum types are always discovered at runtime (from live bindables), never referenced.
    /// </summary>
    public static class RulesetSettingDescriptors
    {
        public static IReadOnlyDictionary<string, IReadOnlyList<RulesetSettingDescriptor>> Sections { get; } =
            new Dictionary<string, IReadOnlyList<RulesetSettingDescriptor>>(StringComparer.OrdinalIgnoreCase)
            {
                ["osu"] = new[]
                {
                    new RulesetSettingDescriptor
                    {
                        SettingName = "SnakingInSliders", Kind = RulesetSettingKind.Bool, Label = "Snaking in sliders",
                        GetModelValue = s => s.Osu.SnakingInSliders, SetModelValue = (s, v) => s.Osu.SnakingInSliders = (bool)v!,
                    },
                    new RulesetSettingDescriptor
                    {
                        SettingName = "SnakingOutSliders", Kind = RulesetSettingKind.Bool, Label = "Snaking out sliders",
                        GetModelValue = s => s.Osu.SnakingOutSliders, SetModelValue = (s, v) => s.Osu.SnakingOutSliders = (bool)v!,
                    },
                    new RulesetSettingDescriptor
                    {
                        SettingName = "HitAnimations", Kind = RulesetSettingKind.Bool, Label = "Hit animations",
                        GetModelValue = s => s.Osu.HitAnimations, SetModelValue = (s, v) => s.Osu.HitAnimations = (bool)v!,
                    },
                    new RulesetSettingDescriptor
                    {
                        SettingName = "ShowCursorTrail", Kind = RulesetSettingKind.Bool, Label = "Cursor trail",
                        GetModelValue = s => s.Osu.ShowCursorTrail, SetModelValue = (s, v) => s.Osu.ShowCursorTrail = (bool)v!,
                    },
                    new RulesetSettingDescriptor
                    {
                        SettingName = "ShowCursorRipples", Kind = RulesetSettingKind.Bool, Label = "Cursor ripples",
                        GetModelValue = s => s.Osu.ShowCursorRipples, SetModelValue = (s, v) => s.Osu.ShowCursorRipples = (bool)v!,
                    },
                    new RulesetSettingDescriptor
                    {
                        SettingName = "PlayfieldBorderStyle", Kind = RulesetSettingKind.Enum, Label = "Playfield border style",
                        ValueTypeName = "osu.Game.Rulesets.UI.PlayfieldBorderStyle, osu.Game",
                        GetModelValue = s => s.Osu.PlayfieldBorderStyle, SetModelValue = (s, v) => s.Osu.PlayfieldBorderStyle = (int)v!,
                    },
                },
                ["taiko"] = new[]
                {
                    new RulesetSettingDescriptor
                    {
                        SettingName = "TouchControlScheme", Kind = RulesetSettingKind.Enum, Label = "Touch control scheme",
                        ValueTypeName = "osu.Game.Rulesets.Taiko.Configuration.TaikoTouchControlScheme, osu.Game.Rulesets.Taiko",
                        GetModelValue = s => s.Taiko.TouchControlScheme, SetModelValue = (s, v) => s.Taiko.TouchControlScheme = (int)v!,
                    },
                    new RulesetSettingDescriptor
                    {
                        SettingName = "HitAnimations", Kind = RulesetSettingKind.Bool, Label = "Hit animations",
                        GetModelValue = s => s.Taiko.HitAnimations, SetModelValue = (s, v) => s.Taiko.HitAnimations = (bool)v!,
                    },
                    new RulesetSettingDescriptor
                    {
                        SettingName = "RateAdjustedHitAnimation", Kind = RulesetSettingKind.Bool, Label = "Rate-adjusted hit animations",
                        GetModelValue = s => s.Taiko.RateAdjustedHitAnimation, SetModelValue = (s, v) => s.Taiko.RateAdjustedHitAnimation = (bool)v!,
                    },
                },
                ["mania"] = new[]
                {
                    new RulesetSettingDescriptor
                    {
                        SettingName = "ScrollDirection", Kind = RulesetSettingKind.Enum, Label = "Scrolling direction",
                        ValueTypeName = "osu.Game.Rulesets.Mania.UI.ManiaScrollingDirection, osu.Game.Rulesets.Mania",
                        GetModelValue = s => s.Mania.ScrollDirection, SetModelValue = (s, v) => s.Mania.ScrollDirection = (int)v!,
                    },
                    new RulesetSettingDescriptor
                    {
                        SettingName = "ScrollSpeed", Kind = RulesetSettingKind.Double, Label = "Scroll speed", SliderStep = 1,
                        GetModelValue = s => s.Mania.ScrollSpeed, SetModelValue = (s, v) => s.Mania.ScrollSpeed = (double)v!,
                    },
                    new RulesetSettingDescriptor
                    {
                        SettingName = "TimingBasedNoteColouring", Kind = RulesetSettingKind.Bool, Label = "Timing-based note colouring",
                        GetModelValue = s => s.Mania.TimingBasedNoteColouring, SetModelValue = (s, v) => s.Mania.TimingBasedNoteColouring = (bool)v!,
                    },
                    new RulesetSettingDescriptor
                    {
                        SettingName = "TouchOverlay", Kind = RulesetSettingKind.Bool, Label = "Touch overlay",
                        GetModelValue = s => s.Mania.TouchOverlay, SetModelValue = (s, v) => s.Mania.TouchOverlay = (bool)v!,
                    },
                },
            };

        /// <summary>
        /// Returns the covered settings for a ruleset short name, or null when the ruleset has none.
        /// </summary>
        public static IReadOnlyList<RulesetSettingDescriptor>? ForShortName(string? shortName)
            => shortName != null && Sections.TryGetValue(shortName, out var settings) ? settings : null;

        /// <summary>
        /// Resolves the live bindable for a setting on a ruleset config manager, or null
        /// when anything on the path is missing (renamed setting, changed API, ...).
        /// </summary>
        public static object? GetBindable(object manager, RulesetSettingDescriptor descriptor)
        {
            MethodInfo? method = manager.GetType().GetMethod("GetBindable", BindingFlags.Public | BindingFlags.Instance);

            if (method == null)
                return null;

            Type? valueType = getSettingValueType(descriptor);

            if (valueType == null)
                return null;

            object? key = parseLookupKey(manager, descriptor.SettingName);

            if (key == null)
                return null;

            try
            {
                return method.MakeGenericMethod(valueType).Invoke(manager, new[] { key });
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Reads the current value of a live bindable. Enum values arrive boxed as-is;
        /// convert them with <see cref="ToModelValue"/>.
        /// </summary>
        public static object? GetBindableValue(object bindable)
            => bindable.GetType().GetProperty("Value")?.GetValue(bindable);

        /// <summary>
        /// Writes a live value (as produced by <see cref="ToLiveValue"/>) into a bindable.
        /// </summary>
        public static void SetBindableValue(object bindable, object? value)
            => bindable.GetType().GetProperty("Value")?.SetValue(bindable, value);

        /// <summary>
        /// Converts a bracket model value (bool, double or int) into a live value for the
        /// given bindable (enums arrive via their integer value).
        /// </summary>
        public static object? ToLiveValue(RulesetSettingDescriptor descriptor, object bindable, object? modelValue)
        {
            if (modelValue == null)
                return null;

            if (descriptor.Kind == RulesetSettingKind.Enum)
            {
                Type? valueType = bindable.GetType().GetGenericArguments().FirstOrDefault();
                return valueType == null ? null : Enum.ToObject(valueType, modelValue);
            }

            return modelValue;
        }

        /// <summary>
        /// Converts a live bindable value into a bracket model value (enums become integers).
        /// </summary>
        public static object? ToModelValue(RulesetSettingDescriptor descriptor, object? liveValue)
        {
            if (liveValue == null)
                return null;

            if (descriptor.Kind == RulesetSettingKind.Enum)
                return Convert.ToInt32(liveValue);

            return liveValue;
        }

        /// <summary>
        /// Discovers the bindable value type for a setting: primitives map directly,
        /// value enums resolve by name (their assemblies are loaded whenever the manager is).
        /// </summary>
        private static Type? getSettingValueType(RulesetSettingDescriptor descriptor)
        {
            switch (descriptor.Kind)
            {
                case RulesetSettingKind.Bool:
                    return typeof(bool);

                case RulesetSettingKind.Double:
                    return typeof(double);

                case RulesetSettingKind.Enum:
                    return descriptor.ValueTypeName == null ? null : Type.GetType(descriptor.ValueTypeName, false);

                default:
                    return null;
            }
        }

        /// <summary>
        /// Parses a lookup key (e.g. SnakingInSliders) on the manager's lookup enum type,
        /// or null when the manager has no such setting (renamed API, ...).
        /// </summary>
        private static object? parseLookupKey(object manager, string settingName)
        {
            Type? lookupEnum = getLookupEnumType(manager);

            if (lookupEnum?.GetField(settingName) == null)
                return null;

            return Enum.Parse(lookupEnum, settingName);
        }

        private static Type? getLookupEnumType(object manager)
        {
            // RulesetConfigManager<TLookup> sits somewhere up the hierarchy; its type
            // argument is the lookup enum. Compared by name to avoid naming it statically.
            for (Type? type = manager.GetType(); type != null; type = type.BaseType)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition().FullName == "osu.Game.Rulesets.Configuration.RulesetConfigManager`1")
                    return type.GetGenericArguments()[0];
            }

            return null;
        }
    }
}
