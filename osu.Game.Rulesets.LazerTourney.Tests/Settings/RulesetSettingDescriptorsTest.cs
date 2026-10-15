// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.UI;
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
    }
}
