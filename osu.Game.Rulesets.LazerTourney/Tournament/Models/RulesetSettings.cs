// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Models
{
    /// <summary>
    /// The tournament's per-ruleset gameplay settings, stored as a JSON object in the bracket file.
    /// Mirrors the lazer "Gameplay" settings tab's per-ruleset sections.
    /// Only rulesets with a config manager are covered (osu!, taiko, mania; catch has none).
    /// </summary>
    /// <remarks>
    /// The field initialisers below are the single place that defines the defaults used when
    /// the bracket file has no such object or lacks individual fields. They copy lazer's own defaults.
    /// Enum-valued settings are stored as plain integers on purpose: naming the official
    /// ruleset enum types in field/attribute positions would statically bind this assembly
    /// to the official ruleset assemblies and break plugin loading when they resolve late
    /// (e.g. developer builds). integers read/write identically on the wire, since
    /// Newtonsoft.Json serialises enums as numbers by default.
    /// </remarks>
    [Serializable]
    public class RulesetSettings
    {
        public OsuRulesetSettings Osu = new OsuRulesetSettings();
        public TaikoRulesetSettings Taiko = new TaikoRulesetSettings();
        public ManiaRulesetSettings Mania = new ManiaRulesetSettings();
    }

    [Serializable]
    public class OsuRulesetSettings
    {
        [DefaultValue(true)]
        public bool SnakingInSliders = true;
        [DefaultValue(true)]
        public bool SnakingOutSliders = true;
        [DefaultValue(true)]
        public bool HitAnimations = true;
        [DefaultValue(true)]
        public bool ShowCursorTrail = true;
        [DefaultValue(false)]
        public bool ShowCursorRipples = false;
        // PlayfieldBorderStyle.None.
        [DefaultValue(0)]
        public int PlayfieldBorderStyle = 0;
    }

    [Serializable]
    public class TaikoRulesetSettings
    {
        // TaikoTouchControlScheme.KDDK.
        [DefaultValue(0)]
        public int TouchControlScheme = 0;
        [DefaultValue(true)]
        public bool HitAnimations = true;
        [DefaultValue(true)]
        public bool RateAdjustedHitAnimation = true;
    }

    [Serializable]
    public class ManiaRulesetSettings
    {
        [DefaultValue(8.0)]
        public double ScrollSpeed = 8.0;
        // ManiaScrollingDirection.Down.
        [DefaultValue(1)]
        public int ScrollDirection = 1;
        [DefaultValue(false)]
        public bool TimingBasedNoteColouring = false;
        [DefaultValue(false)]
        public bool TouchOverlay = false;
    }
}
