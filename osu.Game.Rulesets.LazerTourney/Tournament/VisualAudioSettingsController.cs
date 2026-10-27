// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.Scoring;

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
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesetStore { get; set; } = null!;

        [Resolved]
        private IRulesetConfigCache configCache { get; set; } = null!;

        private readonly List<Action> restoreActions = new List<Action>();

        // Official ruleset types must only ever be touched from method bodies
        // (resolved lazily on first use), never from fields, signatures or attributes,
        // or this whole assembly fails to load. See RulesetAssemblyIsolationTest.

        /// <summary>
        /// The live ruleset config for a short name (osu, taiko, mania), or null when unavailable.
        /// </summary>
        public IRulesetConfigManager? GetRulesetConfig(string shortName)
        {
            try
            {
                var info = rulesetStore.GetRuleset(shortName);
                if (info == null)
                    return null;

                return configCache.GetConfigFor(info.CreateInstance());
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
            var defaults = new VisualAudioSettings();

            restoreActions.Clear();

            void backup<T>(OsuSetting setting)
            {
                var bindable = config.GetBindable<T>(setting);
                T value = bindable.Value;
                T defaultValue = bindable.Default;
                restoreActions.Add(() =>
                {
                    bindable.Default = defaultValue;
                    bindable.Value = value;
                });
            }

            void apply<T>(OsuSetting setting, T value, T defaultValue)
            {
                var bindable = config.GetBindable<T>(setting);
                // The reset button follows the bindable's default, so expose the tournament
                // model defaults instead of lazer's own setting defaults.
                bindable.Default = defaultValue;
                bindable.Value = value;
            }

            void backupAudio(BindableDouble bindable)
            {
                double value = bindable.Value;
                double defaultValue = bindable.Default;
                restoreActions.Add(() =>
                {
                    bindable.Default = defaultValue;
                    bindable.Value = value;
                });
            }

            void applyAudio(BindableDouble bindable, double value, double defaultValue)
            {
                bindable.Default = defaultValue;
                bindable.Value = value;
            }

            backup<ScoringMode>(OsuSetting.ScoreDisplayMode);
            backup<bool>(OsuSetting.HitLighting);
            backup<bool>(OsuSetting.StarFountains);
            backup<double>(OsuSetting.VolumeInactive);
            backupAudio(audio.VolumeTrack);
            backupAudio(audio.VolumeSample);
            backup<double>(OsuSetting.HitsoundVolume);
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

            apply(OsuSetting.ScoreDisplayMode, effective.ScoreDisplayMode, defaults.ScoreDisplayMode);
            apply(OsuSetting.HitLighting, effective.HitLighting, defaults.HitLighting);
            apply(OsuSetting.StarFountains, effective.StarFountains, defaults.StarFountains);
            apply(OsuSetting.VolumeInactive, effective.VolumeInactive, defaults.VolumeInactive);
            applyAudio(audio.VolumeTrack, effective.MusicVolume, defaults.MusicVolume);
            applyAudio(audio.VolumeSample, effective.EffectVolume, defaults.EffectVolume);
            apply(OsuSetting.HitsoundVolume, effective.HitsoundVolume, defaults.HitsoundVolume);
            apply(OsuSetting.PositionalHitsoundsLevel, effective.PositionalHitsoundsLevel, defaults.PositionalHitsoundsLevel);
            apply(OsuSetting.AlwaysPlayFirstComboBreak, effective.AlwaysPlayFirstComboBreak, defaults.AlwaysPlayFirstComboBreak);
            apply(OsuSetting.DimLevel, effective.DimLevel, defaults.DimLevel);
            apply(OsuSetting.BlurLevel, effective.BlurLevel, defaults.BlurLevel);
            apply(OsuSetting.LightenDuringBreaks, effective.LightenDuringBreaks, defaults.LightenDuringBreaks);
            apply(OsuSetting.FadePlayfieldWhenHealthLow, effective.FadePlayfieldWhenHealthLow, defaults.FadePlayfieldWhenHealthLow);
            apply(OsuSetting.BeatmapSkins, effective.BeatmapSkins, defaults.BeatmapSkins);
            apply(OsuSetting.BeatmapColours, effective.BeatmapColours, defaults.BeatmapColours);
            apply(OsuSetting.BeatmapHitsounds, effective.BeatmapHitsounds, defaults.BeatmapHitsounds);
            apply(OsuSetting.ShowStoryboard, effective.ShowStoryboard, defaults.ShowStoryboard);
            apply(OsuSetting.ComboColourNormalisationAmount, effective.ComboColourNormalisationAmount, defaults.ComboColourNormalisationAmount);
            apply(OsuSetting.HUDVisibilityMode, effective.HUDVisibilityMode, defaults.HUDVisibilityMode);
            apply(OsuSetting.ReplaySettingsOverlay, effective.ReplaySettingsOverlay, defaults.ReplaySettingsOverlay);
            apply(OsuSetting.KeyOverlay, effective.KeyOverlay, defaults.KeyOverlay);
            apply(OsuSetting.GameplayLeaderboard, effective.GameplayLeaderboard, defaults.GameplayLeaderboard);
            apply(OsuSetting.ShowHealthDisplayWhenCantFail, effective.ShowHealthDisplayWhenCantFail, defaults.ShowHealthDisplayWhenCantFail);
            apply(OsuSetting.GameplayCursorSize, effective.GameplayCursorSize, defaults.GameplayCursorSize);
            apply(OsuSetting.AutoCursorSize, effective.AutoCursorSize, defaults.AutoCursorSize);
            apply(OsuSetting.GameplayCursorDuringTouch, effective.GameplayCursorDuringTouch, defaults.GameplayCursorDuringTouch);
            apply(OsuSetting.GameplayDisableWinKey, effective.GameplayDisableWinKey, defaults.GameplayDisableWinKey);
            apply(OsuSetting.IncreaseFirstObjectVisibility, effective.IncreaseFirstObjectVisibility, defaults.IncreaseFirstObjectVisibility);
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
            target.VolumeInactive = get<double>(OsuSetting.VolumeInactive);
            target.MusicVolume = audio.VolumeTrack.Value;
            target.EffectVolume = audio.VolumeSample.Value;
            target.HitsoundVolume = get<double>(OsuSetting.HitsoundVolume);
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
            var defaults = new RulesetSettings();

            foreach (var section in RulesetSettingDescriptors.Sections)
            {
                object? manager = GetRulesetConfig(section.Key);

                if (manager == null)
                    continue;

                foreach (var setting in section.Value)
                {
                    switch (setting.Kind)
                    {
                        case RulesetSettingKind.Bool:
                            Bindable<bool>? boolBindable = RulesetSettingDescriptors.GetBoolBindable(manager, setting);

                            if (boolBindable == null
                                || setting.GetModelValue(defaults) is not bool defaultBool
                                || setting.GetModelValue(effective) is not bool valueBool)
                                break;

                            backupBindable(boolBindable);
                            boolBindable.Default = defaultBool;
                            boolBindable.Value = valueBool;
                            break;

                        case RulesetSettingKind.Double:
                            Bindable<double>? doubleBindable = RulesetSettingDescriptors.GetDoubleBindable(manager, setting);

                            if (doubleBindable == null
                                || setting.GetModelValue(defaults) is not double defaultDouble
                                || setting.GetModelValue(effective) is not double valueDouble)
                                break;

                            backupBindable(doubleBindable);
                            doubleBindable.Default = defaultDouble;
                            doubleBindable.Value = valueDouble;
                            break;

                        case RulesetSettingKind.Enum:
                            int? liveValue = RulesetSettingDescriptors.GetEnumModelValue(manager, setting);

                            if (liveValue == null || setting.GetModelValue(effective) is not int valueEnum)
                                break;

                            int live = liveValue.Value;
                            restoreActions.Add(() => RulesetSettingDescriptors.SetEnumModelValue(manager, setting, live));
                            RulesetSettingDescriptors.SetEnumModelValue(manager, setting, valueEnum);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Copies the live ruleset settings into the bracket object so saves persist them.
        /// </summary>
        public void SnapshotRulesetToBracket(RulesetSettings target)
        {
            foreach (var section in RulesetSettingDescriptors.Sections)
            {
                object? manager = GetRulesetConfig(section.Key);

                if (manager == null)
                    continue;

                foreach (var setting in section.Value)
                {
                    switch (setting.Kind)
                    {
                        case RulesetSettingKind.Bool:
                            if (RulesetSettingDescriptors.GetBoolBindable(manager, setting) is Bindable<bool> boolBindable)
                                setting.SetModelValue(target, boolBindable.Value);
                            break;

                        case RulesetSettingKind.Double:
                            if (RulesetSettingDescriptors.GetDoubleBindable(manager, setting) is Bindable<double> doubleBindable)
                                setting.SetModelValue(target, doubleBindable.Value);
                            break;

                        case RulesetSettingKind.Enum:
                            if (RulesetSettingDescriptors.GetEnumModelValue(manager, setting) is int enumValue)
                                setting.SetModelValue(target, enumValue);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Remembers a live bindable for restoration on exit. Holds the weakly bound copy and
        /// its values; nothing here names an official ruleset type.
        /// </summary>
        private void backupBindable<T>(Bindable<T> bindable)
        {
            T value = bindable.Value;
            T defaultValue = bindable.Default;
            restoreActions.Add(() =>
            {
                bindable.Default = defaultValue;
                bindable.Value = value;
            });
        }
    }
}
