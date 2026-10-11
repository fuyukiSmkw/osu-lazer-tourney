// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko.Configuration;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.LazerTourney.Tournament
{
    /// <summary>
    /// Backs up the user's lazer gameplay settings on tournament entry, applies the bracket's
    /// visual and audio settings (or the built-in defaults), and restores the backup on exit.
    /// </summary>
    public partial class VisualAudioSettingsController : Component
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesetStore { get; set; } = null!;

        [Resolved]
        private IRulesetConfigCache configCache { get; set; } = null!;

        private readonly List<System.Action> restoreActions = new List<System.Action>();

        private OsuRulesetConfigManager? osuRulesetConfig;
        private TaikoRulesetConfigManager? taikoRulesetConfig;
        private ManiaRulesetConfigManager? maniaRulesetConfig;

        /// <summary>
        /// The live osu! ruleset config, or null when the ruleset is unavailable.
        /// </summary>
        public OsuRulesetConfigManager? OsuRulesetConfig => osuRulesetConfig ??= getRulesetConfig<OsuRulesetConfigManager>("osu");

        /// <summary>
        /// The live taiko ruleset config, or null when the ruleset is unavailable.
        /// </summary>
        public TaikoRulesetConfigManager? TaikoRulesetConfig => taikoRulesetConfig ??= getRulesetConfig<TaikoRulesetConfigManager>("taiko");

        /// <summary>
        /// The live mania ruleset config, or null when the ruleset is unavailable.
        /// </summary>
        public ManiaRulesetConfigManager? ManiaRulesetConfig => maniaRulesetConfig ??= getRulesetConfig<ManiaRulesetConfigManager>("mania");

        private T? getRulesetConfig<T>(string shortName) where T : class
        {
            try
            {
                var info = rulesetStore.GetRuleset(shortName);
                if (info == null)
                    return null;

                return configCache.GetConfigFor(info.CreateInstance()) as T;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// Remembers every covered setting, then applies the bracket values (or built-in defaults).
        /// </summary>
        public void ApplyFromBracket(VisualAudioSettings? bracket)
        {
            var effective = bracket ?? new VisualAudioSettings();

            restoreActions.Clear();

            void backup<T>(OsuSetting setting)
            {
                var bindable = config.GetBindable<T>(setting);
                T value = bindable.Value;
                restoreActions.Add(() => bindable.Value = value);
            }

            void apply<T>(OsuSetting setting, T value) => config.GetBindable<T>(setting).Value = value;

            backup<ScoringMode>(OsuSetting.ScoreDisplayMode);
            backup<bool>(OsuSetting.HitLighting);
            backup<bool>(OsuSetting.StarFountains);
            backup<float>(OsuSetting.PositionalHitsoundsLevel);
            backup<bool>(OsuSetting.AlwaysPlayFirstComboBreak);
            backup<double>(OsuSetting.DimLevel);
            backup<double>(OsuSetting.BlurLevel);
            backup<bool>(OsuSetting.LightenDuringBreaks);
            backup<bool>(OsuSetting.FadePlayfieldWhenHealthLow);
            backup<bool>(OsuSetting.BeatmapSkins);
            backup<bool>(OsuSetting.BeatmapColours);
            backup<bool>(OsuSetting.BeatmapHitsounds);
            backup<bool>(OsuSetting.ShowStoryboard);
            backup<float>(OsuSetting.ComboColourNormalisationAmount);
            backup<HUDVisibilityMode>(OsuSetting.HUDVisibilityMode);
            backup<bool>(OsuSetting.ReplaySettingsOverlay);
            backup<bool>(OsuSetting.KeyOverlay);
            backup<bool>(OsuSetting.GameplayLeaderboard);
            backup<bool>(OsuSetting.ShowHealthDisplayWhenCantFail);
            backup<float>(OsuSetting.GameplayCursorSize);
            backup<bool>(OsuSetting.AutoCursorSize);
            backup<bool>(OsuSetting.GameplayCursorDuringTouch);
            backup<bool>(OsuSetting.GameplayDisableWinKey);
            backup<bool>(OsuSetting.IncreaseFirstObjectVisibility);

            apply(OsuSetting.ScoreDisplayMode, effective.ScoreDisplayMode);
            apply(OsuSetting.HitLighting, effective.HitLighting);
            apply(OsuSetting.StarFountains, effective.StarFountains);
            apply(OsuSetting.PositionalHitsoundsLevel, effective.PositionalHitsoundsLevel);
            apply(OsuSetting.AlwaysPlayFirstComboBreak, effective.AlwaysPlayFirstComboBreak);
            apply(OsuSetting.DimLevel, effective.DimLevel);
            apply(OsuSetting.BlurLevel, effective.BlurLevel);
            apply(OsuSetting.LightenDuringBreaks, effective.LightenDuringBreaks);
            apply(OsuSetting.FadePlayfieldWhenHealthLow, effective.FadePlayfieldWhenHealthLow);
            apply(OsuSetting.BeatmapSkins, effective.BeatmapSkins);
            apply(OsuSetting.BeatmapColours, effective.BeatmapColours);
            apply(OsuSetting.BeatmapHitsounds, effective.BeatmapHitsounds);
            apply(OsuSetting.ShowStoryboard, effective.ShowStoryboard);
            apply(OsuSetting.ComboColourNormalisationAmount, effective.ComboColourNormalisationAmount);
            apply(OsuSetting.HUDVisibilityMode, effective.HUDVisibilityMode);
            apply(OsuSetting.ReplaySettingsOverlay, effective.ReplaySettingsOverlay);
            apply(OsuSetting.KeyOverlay, effective.KeyOverlay);
            apply(OsuSetting.GameplayLeaderboard, effective.GameplayLeaderboard);
            apply(OsuSetting.ShowHealthDisplayWhenCantFail, effective.ShowHealthDisplayWhenCantFail);
            apply(OsuSetting.GameplayCursorSize, effective.GameplayCursorSize);
            apply(OsuSetting.AutoCursorSize, effective.AutoCursorSize);
            apply(OsuSetting.GameplayCursorDuringTouch, effective.GameplayCursorDuringTouch);
            apply(OsuSetting.GameplayDisableWinKey, effective.GameplayDisableWinKey);
            apply(OsuSetting.IncreaseFirstObjectVisibility, effective.IncreaseFirstObjectVisibility);
        }

        /// <summary>
        /// Restores everything remembered by <see cref="ApplyFromBracket"/>. No-op if never applied.
        /// </summary>
        public void RestoreBackup()
        {
            foreach (var restore in restoreActions)
                restore();
        }

        /// <summary>
        /// Restores the backup and saves to disk immediately. Used on exit paths,
        /// where the debounced background save might not flush before shutdown.
        /// </summary>
        public void RestoreBackupAndSave()
        {
            RestoreBackup();
            config.Save();
        }

        /// <summary>
        /// Copies the live settings into the bracket object so saves persist them.
        /// Runs on every serialisation, which also drives the unsaved-changes detection.
        /// </summary>
        public void SnapshotToBracket(VisualAudioSettings target)
        {
            T get<T>(OsuSetting setting) => config.GetBindable<T>(setting).Value;

            target.ScoreDisplayMode = get<ScoringMode>(OsuSetting.ScoreDisplayMode);
            target.HitLighting = get<bool>(OsuSetting.HitLighting);
            target.StarFountains = get<bool>(OsuSetting.StarFountains);
            target.PositionalHitsoundsLevel = get<float>(OsuSetting.PositionalHitsoundsLevel);
            target.AlwaysPlayFirstComboBreak = get<bool>(OsuSetting.AlwaysPlayFirstComboBreak);
            target.DimLevel = get<double>(OsuSetting.DimLevel);
            target.BlurLevel = get<double>(OsuSetting.BlurLevel);
            target.LightenDuringBreaks = get<bool>(OsuSetting.LightenDuringBreaks);
            target.FadePlayfieldWhenHealthLow = get<bool>(OsuSetting.FadePlayfieldWhenHealthLow);
            target.BeatmapSkins = get<bool>(OsuSetting.BeatmapSkins);
            target.BeatmapColours = get<bool>(OsuSetting.BeatmapColours);
            target.BeatmapHitsounds = get<bool>(OsuSetting.BeatmapHitsounds);
            target.ShowStoryboard = get<bool>(OsuSetting.ShowStoryboard);
            target.ComboColourNormalisationAmount = get<float>(OsuSetting.ComboColourNormalisationAmount);
            target.HUDVisibilityMode = get<HUDVisibilityMode>(OsuSetting.HUDVisibilityMode);
            target.ReplaySettingsOverlay = get<bool>(OsuSetting.ReplaySettingsOverlay);
            target.KeyOverlay = get<bool>(OsuSetting.KeyOverlay);
            target.GameplayLeaderboard = get<bool>(OsuSetting.GameplayLeaderboard);
            target.ShowHealthDisplayWhenCantFail = get<bool>(OsuSetting.ShowHealthDisplayWhenCantFail);
            target.GameplayCursorSize = get<float>(OsuSetting.GameplayCursorSize);
            target.AutoCursorSize = get<bool>(OsuSetting.AutoCursorSize);
            target.GameplayCursorDuringTouch = get<bool>(OsuSetting.GameplayCursorDuringTouch);
            target.GameplayDisableWinKey = get<bool>(OsuSetting.GameplayDisableWinKey);
            target.IncreaseFirstObjectVisibility = get<bool>(OsuSetting.IncreaseFirstObjectVisibility);
        }

        /// <summary>
        /// Remembers every covered ruleset setting, then applies the bracket values (or built-in defaults).
        /// Covers all rulesets with a config manager, not just the selected one.
        /// </summary>
        public void ApplyRulesetFromBracket(RulesetSettings? bracket)
        {
            var effective = bracket ?? new RulesetSettings();

            void backup<T>(Bindable<T> bindable)
            {
                T value = bindable.Value;
                restoreActions.Add(() => bindable.Value = value);
            }

            if (OsuRulesetConfig is { } osu)
            {
                var snakingIn = osu.GetBindable<bool>(OsuRulesetSetting.SnakingInSliders);
                var snakingOut = osu.GetBindable<bool>(OsuRulesetSetting.SnakingOutSliders);
                var hitAnimations = osu.GetBindable<bool>(OsuRulesetSetting.HitAnimations);
                var cursorTrail = osu.GetBindable<bool>(OsuRulesetSetting.ShowCursorTrail);
                var cursorRipples = osu.GetBindable<bool>(OsuRulesetSetting.ShowCursorRipples);
                var borderStyle = osu.GetBindable<PlayfieldBorderStyle>(OsuRulesetSetting.PlayfieldBorderStyle);

                backup(snakingIn);
                backup(snakingOut);
                backup(hitAnimations);
                backup(cursorTrail);
                backup(cursorRipples);
                backup(borderStyle);

                snakingIn.Value = effective.Osu.SnakingInSliders;
                snakingOut.Value = effective.Osu.SnakingOutSliders;
                hitAnimations.Value = effective.Osu.HitAnimations;
                cursorTrail.Value = effective.Osu.ShowCursorTrail;
                cursorRipples.Value = effective.Osu.ShowCursorRipples;
                borderStyle.Value = effective.Osu.PlayfieldBorderStyle;
            }

            if (TaikoRulesetConfig is { } taiko)
            {
                var touchScheme = taiko.GetBindable<TaikoTouchControlScheme>(TaikoRulesetSetting.TouchControlScheme);
                var hitAnimations = taiko.GetBindable<bool>(TaikoRulesetSetting.HitAnimations);
                var rateAdjusted = taiko.GetBindable<bool>(TaikoRulesetSetting.RateAdjustedHitAnimation);

                backup(touchScheme);
                backup(hitAnimations);
                backup(rateAdjusted);

                touchScheme.Value = effective.Taiko.TouchControlScheme;
                hitAnimations.Value = effective.Taiko.HitAnimations;
                rateAdjusted.Value = effective.Taiko.RateAdjustedHitAnimation;
            }

            if (ManiaRulesetConfig is { } mania)
            {
                var scrollSpeed = mania.GetBindable<double>(ManiaRulesetSetting.ScrollSpeed);
                var scrollDirection = mania.GetBindable<ManiaScrollingDirection>(ManiaRulesetSetting.ScrollDirection);
                var noteColouring = mania.GetBindable<bool>(ManiaRulesetSetting.TimingBasedNoteColouring);
                var touchOverlay = mania.GetBindable<bool>(ManiaRulesetSetting.TouchOverlay);

                backup(scrollSpeed);
                backup(scrollDirection);
                backup(noteColouring);
                backup(touchOverlay);

                scrollSpeed.Value = effective.Mania.ScrollSpeed;
                scrollDirection.Value = effective.Mania.ScrollDirection;
                noteColouring.Value = effective.Mania.TimingBasedNoteColouring;
                touchOverlay.Value = effective.Mania.TouchOverlay;
            }
        }

        /// <summary>
        /// Copies the live ruleset settings into the bracket object so saves persist them.
        /// </summary>
        public void SnapshotRulesetToBracket(RulesetSettings target)
        {
            if (OsuRulesetConfig is { } osu)
            {
                target.Osu.SnakingInSliders = osu.GetBindable<bool>(OsuRulesetSetting.SnakingInSliders).Value;
                target.Osu.SnakingOutSliders = osu.GetBindable<bool>(OsuRulesetSetting.SnakingOutSliders).Value;
                target.Osu.HitAnimations = osu.GetBindable<bool>(OsuRulesetSetting.HitAnimations).Value;
                target.Osu.ShowCursorTrail = osu.GetBindable<bool>(OsuRulesetSetting.ShowCursorTrail).Value;
                target.Osu.ShowCursorRipples = osu.GetBindable<bool>(OsuRulesetSetting.ShowCursorRipples).Value;
                target.Osu.PlayfieldBorderStyle = osu.GetBindable<PlayfieldBorderStyle>(OsuRulesetSetting.PlayfieldBorderStyle).Value;
            }

            if (TaikoRulesetConfig is { } taiko)
            {
                target.Taiko.TouchControlScheme = taiko.GetBindable<TaikoTouchControlScheme>(TaikoRulesetSetting.TouchControlScheme).Value;
                target.Taiko.HitAnimations = taiko.GetBindable<bool>(TaikoRulesetSetting.HitAnimations).Value;
                target.Taiko.RateAdjustedHitAnimation = taiko.GetBindable<bool>(TaikoRulesetSetting.RateAdjustedHitAnimation).Value;
            }

            if (ManiaRulesetConfig is { } mania)
            {
                target.Mania.ScrollSpeed = mania.GetBindable<double>(ManiaRulesetSetting.ScrollSpeed).Value;
                target.Mania.ScrollDirection = mania.GetBindable<ManiaScrollingDirection>(ManiaRulesetSetting.ScrollDirection).Value;
                target.Mania.TimingBasedNoteColouring = mania.GetBindable<bool>(ManiaRulesetSetting.TimingBasedNoteColouring).Value;
                target.Mania.TouchOverlay = mania.GetBindable<bool>(ManiaRulesetSetting.TouchOverlay).Value;
            }
        }
    }
}
