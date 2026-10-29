// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.CSharp.RuntimeBinder;
using osu.Framework.Bindables;

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
    /// The single source of truth for covered per-ruleset settings, plus the dynamic
    /// helpers to read and write them without ever naming official ruleset types.
    /// Enum types are always discovered at runtime (from <see cref="RulesetSettingDescriptor.ValueTypeName"/>), never referenced.
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
        /// Resolves the live bool bindable for a setting on a ruleset config manager, or null
        /// when anything on the path is missing (renamed setting, changed API, ...).
        /// </summary>
        public static Bindable<bool>? GetBoolBindable(object manager, RulesetSettingDescriptor descriptor)
            => descriptor.Kind == RulesetSettingKind.Bool ? getBindable<bool>(manager, descriptor) : null;

        /// <summary>
        /// Resolves the live double bindable for a setting, or null when unavailable.
        /// </summary>
        public static Bindable<double>? GetDoubleBindable(object manager, RulesetSettingDescriptor descriptor)
            => descriptor.Kind == RulesetSettingKind.Double ? getBindable<double>(manager, descriptor) : null;

        /// <summary>
        /// Resolves the enum type backing a descriptor, or null when it cannot be loaded.
        /// </summary>
        public static Type? GetEnumType(RulesetSettingDescriptor descriptor)
        {
            Type? type = descriptor.Kind == RulesetSettingKind.Enum && descriptor.ValueTypeName != null
                ? Type.GetType(descriptor.ValueTypeName, false)
                : null;

            return type is { IsEnum: true } ? type : null;
        }

        /// <summary>
        /// Reads the current enum setting as its integer model value, or null when unavailable.
        /// </summary>
        public static int? GetEnumModelValue(object manager, RulesetSettingDescriptor descriptor)
        {
            Type? enumType = GetEnumType(descriptor);

            if (enumType == null)
                return null;

            var (_, text) = findLookupEntry(manager, descriptor);

            if (text == null || !Enum.TryParse(enumType, text, out object? value) || value == null)
                return null;

            return Convert.ToInt32(value);
        }

        /// <summary>
        /// Writes an integer model value into the live enum setting.
        /// </summary>
        public static void SetEnumModelValue(object manager, RulesetSettingDescriptor descriptor, int value)
        {
            Type? enumType = GetEnumType(descriptor);

            if (enumType == null)
                return;

            var (key, _) = findLookupEntry(manager, descriptor);

            if (key == null)
                return;

            object boxed = Enum.ToObject(enumType, value);

            try
            {
                ((dynamic)manager).SetValue((dynamic)key, (dynamic)boxed);
            }
            catch (Exception e) when (e is RuntimeBinderException or ArgumentException or InvalidCastException)
            {
            }
        }

        /// <summary>
        /// Dynamically resolves a typed bindable without naming the manager's lookup enum type.
        /// </summary>
        private static Bindable<T>? getBindable<T>(object manager, RulesetSettingDescriptor descriptor)
        {
            var (key, _) = findLookupEntry(manager, descriptor);

            if (key == null)
                return null;

            try
            {
                return ((dynamic)manager).GetBindable<T>((dynamic)key);
            }
            catch (Exception e) when (e is RuntimeBinderException or ArgumentException or InvalidCastException)
            {
                return null;
            }
        }

        /// <summary>
        /// Finds the manager's lookup key for a descriptor by name, along with the current
        /// value text (as used by the manager's own logging snapshot).
        /// </summary>
        private static (object? key, string? text) findLookupEntry(object manager, RulesetSettingDescriptor descriptor)
        {
            try
            {
                if (((dynamic)manager).GetCurrentConfigurationForLogging() is not IDictionary configuration)
                    return (null, null);

                foreach (DictionaryEntry entry in configuration)
                {
                    if (entry.Key?.ToString() != descriptor.SettingName)
                        continue;

                    return (entry.Key, entry.Value?.ToString());
                }
            }
            catch (Exception e) when (e is RuntimeBinderException or ArgumentException or InvalidCastException)
            {
            }

            return (null, null);
        }
    }
}
