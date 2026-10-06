// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using osu.Game.Configuration;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Models
{
    /// <summary>
    /// The tournament's visual and audio gameplay settings, stored as a JSON object in the bracket file.
    /// Mirrors the lazer "Gameplay" settings tab (General, Audio, Background, Beatmap, HUD, Input, Mods).
    /// </summary>
    /// <remarks>
    /// The field initialisers below are the single place that defines the defaults used when
    /// the bracket file has no such object. They currently copy lazer's own defaults.
    /// </remarks>
    [Serializable]
    public class VisualAudioSettings
    {
        // General
        [DefaultValue(ScoringMode.Standardised)]
        public ScoringMode ScoreDisplayMode = ScoringMode.Standardised;
        [DefaultValue(true)]
        public bool HitLighting = true;
        [DefaultValue(true)]
        public bool StarFountains = true;

        // Audio
        [DefaultValue(0.2f)]
        public float PositionalHitsoundsLevel = 0.2f;
        [DefaultValue(true)]
        public bool AlwaysPlayFirstComboBreak = true;

        // Background
        [DefaultValue(0.7)]
        public double DimLevel = 0.7;
        [DefaultValue(0d)]
        public double BlurLevel = 0;
        [DefaultValue(true)]
        public bool LightenDuringBreaks = true;
        [DefaultValue(true)]
        public bool FadePlayfieldWhenHealthLow = true;

        // Beatmap
        [DefaultValue(true)]
        public bool BeatmapSkins = true;
        [DefaultValue(true)]
        public bool BeatmapColours = true;
        [DefaultValue(true)]
        public bool BeatmapHitsounds = true;
        [DefaultValue(true)]
        public bool ShowStoryboard = true;
        [DefaultValue(0.0f)]
        public float ComboColourNormalisationAmount = 0.0f;

        // HUD
        [DefaultValue(HUDVisibilityMode.Always)]
        public HUDVisibilityMode HUDVisibilityMode = HUDVisibilityMode.Always;
        [DefaultValue(true)]
        public bool ReplaySettingsOverlay = true;
        [DefaultValue(true)]
        public bool KeyOverlay = true;
        [DefaultValue(false)]
        public bool GameplayLeaderboard = false;
        [DefaultValue(true)]
        public bool ShowHealthDisplayWhenCantFail = true;

        // Input
        [DefaultValue(1.0f)]
        public float GameplayCursorSize = 1.0f;
        [DefaultValue(false)]
        public bool AutoCursorSize = false;
        [DefaultValue(false)]
        public bool GameplayCursorDuringTouch = false;
        [DefaultValue(true)]
        public bool GameplayDisableWinKey = true;

        // Mods
        [DefaultValue(true)]
        public bool IncreaseFirstObjectVisibility = true;
    }
}
