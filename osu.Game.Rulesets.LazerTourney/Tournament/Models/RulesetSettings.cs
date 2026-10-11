// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Taiko.Configuration;
using osu.Game.Rulesets.UI;

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
        [DefaultValue(PlayfieldBorderStyle.None)]
        public PlayfieldBorderStyle PlayfieldBorderStyle = PlayfieldBorderStyle.None;
    }

    [Serializable]
    public class TaikoRulesetSettings
    {
        [DefaultValue(TaikoTouchControlScheme.KDDK)]
        public TaikoTouchControlScheme TouchControlScheme = TaikoTouchControlScheme.KDDK;
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
        [DefaultValue(ManiaScrollingDirection.Down)]
        public ManiaScrollingDirection ScrollDirection = ManiaScrollingDirection.Down;
        [DefaultValue(false)]
        public bool TimingBasedNoteColouring = false;
        [DefaultValue(false)]
        public bool TouchOverlay = false;
    }
}
