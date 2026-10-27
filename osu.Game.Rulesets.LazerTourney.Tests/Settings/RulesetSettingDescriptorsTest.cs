// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Taiko.Configuration;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;

namespace osu.Game.Rulesets.LazerTourney.Tests.Settings
{
    /// <summary>
    /// Validates the ruleset setting descriptor table against the real official types.
    /// Naming those types here is safe: test assemblies are excluded from ruleset scanning.
    /// </summary>
    [TestFixture]
    public class RulesetSettingDescriptorsTest
    {
        [Test]
        public void LookupSettingNamesExist()
        {
            assertLookupNames(typeof(OsuRulesetSetting), "osu");
            assertLookupNames(typeof(TaikoRulesetSetting), "taiko");
            assertLookupNames(typeof(ManiaRulesetSetting), "mania");

            static void assertLookupNames(Type lookupEnum, string shortName)
            {
                var settings = RulesetSettingDescriptors.ForShortName(shortName);
                Assert.That(settings, Is.Not.Null);

                foreach (var setting in settings!)
                    Assert.That(lookupEnum.GetField(setting.SettingName), Is.Not.Null, $"{shortName}.{setting.SettingName}");
            }
        }

        [Test]
        public void ValueTypeNamesResolveToEnums()
        {
            foreach (var section in RulesetSettingDescriptors.Sections)
            {
                foreach (var setting in section.Value)
                {
                    Assert.That(setting.Label, Is.Not.Empty, setting.SettingName);

                    if (setting.Kind != RulesetSettingKind.Enum)
                        continue;

                    Type? valueType = Type.GetType(setting.ValueTypeName!, false);
                    Assert.That(valueType, Is.Not.Null, setting.SettingName);
                    Assert.That(valueType!.IsEnum, Is.True, setting.SettingName);
                }
            }
        }

        [Test]
        public void ModelDefaultsMatchOfficialDefaults()
        {
            var defaults = new RulesetSettings();

            Assert.That((PlayfieldBorderStyle)defaults.Osu.PlayfieldBorderStyle, Is.EqualTo(PlayfieldBorderStyle.None));
            Assert.That((TaikoTouchControlScheme)defaults.Taiko.TouchControlScheme, Is.EqualTo(TaikoTouchControlScheme.KDDK));
            Assert.That((ManiaScrollingDirection)defaults.Mania.ScrollDirection, Is.EqualTo(ManiaScrollingDirection.Down));

            Assert.That(Enum.IsDefined(typeof(PlayfieldBorderStyle), defaults.Osu.PlayfieldBorderStyle));
            Assert.That(Enum.IsDefined(typeof(TaikoTouchControlScheme), defaults.Taiko.TouchControlScheme));
            Assert.That(Enum.IsDefined(typeof(ManiaScrollingDirection), defaults.Mania.ScrollDirection));
        }

        [Test]
        public void BoolSettingReadsAndWritesLiveConfig()
        {
            var manager = new OsuRulesetConfigManager(null, new OsuRuleset().RulesetInfo);
            var setting = RulesetSettingDescriptors.ForShortName("osu")!.Single(s => s.SettingName == nameof(OsuRulesetSetting.SnakingInSliders));

            var bindable = RulesetSettingDescriptors.GetBoolBindable(manager, setting);
            Assert.That(bindable, Is.Not.Null);

            bool original = manager.GetBindable<bool>(OsuRulesetSetting.SnakingInSliders).Value;

            bindable!.Value = !original;

            Assert.That(manager.GetBindable<bool>(OsuRulesetSetting.SnakingInSliders).Value, Is.EqualTo(!original));
        }

        [Test]
        public void DoubleSettingReadsAndWritesLiveConfig()
        {
            var manager = new ManiaRulesetConfigManager(null, new ManiaRuleset().RulesetInfo);
            var setting = RulesetSettingDescriptors.ForShortName("mania")!.Single(s => s.SettingName == nameof(ManiaRulesetSetting.ScrollSpeed));

            var bindable = RulesetSettingDescriptors.GetDoubleBindable(manager, setting);
            Assert.That(bindable, Is.Not.Null);

            bindable!.Value = 12.5;

            Assert.That(manager.GetBindable<double>(ManiaRulesetSetting.ScrollSpeed).Value, Is.EqualTo(12.5));
        }

        [Test]
        public void EnumSettingReadsAndWritesModelValue()
        {
            var manager = new ManiaRulesetConfigManager(null, new ManiaRuleset().RulesetInfo);
            var setting = RulesetSettingDescriptors.ForShortName("mania")!.Single(s => s.SettingName == nameof(ManiaRulesetSetting.ScrollDirection));

            Assert.That(RulesetSettingDescriptors.GetEnumType(setting), Is.EqualTo(typeof(ManiaScrollingDirection)));
            Assert.That(RulesetSettingDescriptors.GetEnumModelValue(manager, setting), Is.EqualTo((int)ManiaScrollingDirection.Down));

            RulesetSettingDescriptors.SetEnumModelValue(manager, setting, (int)ManiaScrollingDirection.Up);

            Assert.That(manager.GetBindable<ManiaScrollingDirection>(ManiaRulesetSetting.ScrollDirection).Value, Is.EqualTo(ManiaScrollingDirection.Up));
            Assert.That(RulesetSettingDescriptors.GetEnumModelValue(manager, setting), Is.EqualTo((int)ManiaScrollingDirection.Up));
        }

        [Test]
        public void MissingOrInvalidSettingsDegradeGracefully()
        {
            var manager = new OsuRulesetConfigManager(null, new OsuRuleset().RulesetInfo);

            var missing = new RulesetSettingDescriptor
            {
                SettingName = "RenamedSetting",
                Kind = RulesetSettingKind.Bool,
            };

            Assert.That(RulesetSettingDescriptors.GetBoolBindable(manager, missing), Is.Null);

            var badEnumType = new RulesetSettingDescriptor
            {
                SettingName = nameof(OsuRulesetSetting.PlayfieldBorderStyle),
                Kind = RulesetSettingKind.Enum,
                ValueTypeName = "Does.Not.Exist",
            };

            Assert.That(RulesetSettingDescriptors.GetEnumType(badEnumType), Is.Null);
            Assert.That(RulesetSettingDescriptors.GetEnumModelValue(manager, badEnumType), Is.Null);
            Assert.DoesNotThrow(() => RulesetSettingDescriptors.SetEnumModelValue(manager, badEnumType, 0));

            var wrongKind = RulesetSettingDescriptors.ForShortName("osu")!.Single(s => s.SettingName == nameof(OsuRulesetSetting.PlayfieldBorderStyle));

            Assert.That(RulesetSettingDescriptors.GetBoolBindable(manager, wrongKind), Is.Null);
            Assert.That(RulesetSettingDescriptors.GetDoubleBindable(manager, wrongKind), Is.Null);
        }
    }
}
