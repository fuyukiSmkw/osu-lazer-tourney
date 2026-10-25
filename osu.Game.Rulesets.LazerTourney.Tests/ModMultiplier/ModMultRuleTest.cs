// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.LazerTourney.Tournament.Models.ModMultiplier;

namespace osu.Game.Rulesets.LazerTourney.Tests.ModMultiplier
{
    [TestFixture]
    public class ModMultRuleTest
    {
        private static double evaluate(string source, params Mod[] mods)
        {
            Assert.That(ModMultRule.TryParse(source, out var rule, out string? error), Is.True, error);
            Assert.That(rule, Is.Not.Null);
            return rule!.Evaluate(mods);
        }

        private static Mod[] mods(params Mod[] mods) => mods;

        [Test]
        public void EmptyRuleIsTimesOne()
        {
            Assert.That(ModMultRule.TryParse(string.Empty, out var rule, out _), Is.True);
            Assert.That(rule!.Evaluate(mods(new OsuModEasy())), Is.EqualTo(1));
            Assert.That(ModMultRule.TryParse("   ", out rule, out _), Is.True);
            Assert.That(rule!.Evaluate(mods()), Is.EqualTo(1));
        }

        [Test]
        public void BareAcronymTestsPresence()
        {
            Assert.That(evaluate("EZ ? 2", new OsuModEasy()), Is.EqualTo(2));
            Assert.That(evaluate("EZ ? 2", new OsuModHidden()), Is.EqualTo(1));
            Assert.That(evaluate("EZ ? 2", mods()), Is.EqualTo(1));
        }

        [Test]
        public void AcronymsAreCaseInsensitive()
        {
            Assert.That(evaluate("ez ? 2 : 1", new OsuModEasy()), Is.EqualTo(2));
            Assert.That(evaluate("Ez ? 2 : 1", new OsuModEasy()), Is.EqualTo(2));
        }

        [Test]
        public void NestedTernaryChains()
        {
            const string rule = "EZ && HD ? 2 : EZ ? 1.5";
            Assert.That(evaluate(rule, new OsuModEasy(), new OsuModHidden()), Is.EqualTo(2));
            Assert.That(evaluate(rule, new OsuModEasy()), Is.EqualTo(1.5));
            Assert.That(evaluate(rule, new OsuModHidden()), Is.EqualTo(1));
            Assert.That(evaluate(rule, mods()), Is.EqualTo(1));
        }

        [Test]
        public void FactorsComposeWithMultiplication()
        {
            const string rule = "(EZ ? 2) * (HD ? 1.05)";
            Assert.That(evaluate(rule, new OsuModEasy(), new OsuModHidden()), Is.EqualTo(2.1).Within(1e-9));
            Assert.That(evaluate(rule, new OsuModEasy()), Is.EqualTo(2));
            Assert.That(evaluate(rule, mods()), Is.EqualTo(1));
        }

        [Test]
        public void SettingValueParticipatesInArithmetic()
        {
            var adjust = new OsuModDifficultyAdjust();
            adjust.ApproachRate.Value = 7f;

            // (9 - 7) * 0.2 + 1 = 1.4
            Assert.That(evaluate("(9 - (DA.approach_rate ?? 9)) * 0.2 + 1", adjust), Is.EqualTo(1.4).Within(1e-9));
        }

        [Test]
        public void NullSettingFallsBackWithCoalesce()
        {
            // ApproachRate unset (null) -> ?? 9 -> (9-9)*0.2+1 = 1.
            Assert.That(evaluate("(9 - (DA.approach_rate ?? 9)) * 0.2 + 1", new OsuModDifficultyAdjust()), Is.EqualTo(1));
        }

        [Test]
        public void SettingOfAbsentModIsNull()
        {
            // No DA: DA.approach_rate is null, so the ternary picks the else branch.
            Assert.That(evaluate("DA.approach_rate == null ? 3 : 4", new OsuModEasy()), Is.EqualTo(3));
            Assert.That(evaluate("DA.approach_rate != null ? 3 : 4", new OsuModEasy()), Is.EqualTo(4));
        }

        [Test]
        public void SettingNamesIgnoreCaseAndUnderscores()
        {
            var doubleTime = new OsuModDoubleTime();
            doubleTime.SpeedChange.Value = 1.25;

            Assert.That(evaluate("DT.speed_change", doubleTime), Is.EqualTo(1.25).Within(1e-9));
            Assert.That(evaluate("DT.SpeedChange", doubleTime), Is.EqualTo(1.25).Within(1e-9));
            Assert.That(evaluate("DT.speedchange", doubleTime), Is.EqualTo(1.25).Within(1e-9));
        }

        [Test]
        public void EnumSettingComparesAsText()
        {
            var approach = new OsuModApproachDifferent();
            Assert.That(approach.Style.Value, Is.EqualTo(OsuModApproachDifferent.AnimationStyle.Gravity));

            Assert.That(evaluate("AD.style == \"Gravity\" ? 5 : 6", approach), Is.EqualTo(5));
            Assert.That(evaluate("AD.style == \"gravity\" ? 5 : 6", approach), Is.EqualTo(5));
            Assert.That(evaluate("AD.style == \"Linear\" ? 5 : 6", approach), Is.EqualTo(6));
        }

        [Test]
        public void BoolSettingReadsValue()
        {
            var hidden = new OsuModHidden();
            Assert.That(evaluate("HD.only_fade_approach_circles ? 7 : 8", hidden), Is.EqualTo(8));

            hidden.OnlyFadeApproachCircles.Value = true;
            Assert.That(evaluate("HD.only_fade_approach_circles ? 7 : 8", hidden), Is.EqualTo(7));
        }

        [Test]
        public void OnlyMatchesExactSetIgnoringSettings()
        {
            const string rule = "only(AD, BU) ? 1.5 : only(AD, BU, TC) ? 1.6 : only(AD, TC) ? 1.1";

            Assert.That(evaluate(rule, new OsuModApproachDifferent(), new OsuModBubbles()), Is.EqualTo(1.5));
            Assert.That(evaluate(rule, new OsuModApproachDifferent(), new OsuModBubbles(), new OsuModTraceable()), Is.EqualTo(1.6));
            Assert.That(evaluate(rule, new OsuModApproachDifferent(), new OsuModTraceable()), Is.EqualTo(1.1));
            Assert.That(evaluate(rule, new OsuModApproachDifferent()), Is.EqualTo(1));
            Assert.That(evaluate(rule, new OsuModTraceable()), Is.EqualTo(1));
        }

        [Test]
        public void HasAllCountFunctions()
        {
            var set = mods(new OsuModEasy(), new OsuModHidden());

            Assert.That(evaluate("has(EZ) ? 2 : 1", set), Is.EqualTo(2));
            Assert.That(evaluate("has(HD, DT) ? 2 : 1", set), Is.EqualTo(2));
            Assert.That(evaluate("has(DT, NC) ? 2 : 1", set), Is.EqualTo(1));
            Assert.That(evaluate("all(EZ, HD) ? 2 : 1", set), Is.EqualTo(2));
            Assert.That(evaluate("all(EZ, DT) ? 2 : 1", set), Is.EqualTo(1));
            Assert.That(evaluate("count() * 10", set), Is.EqualTo(20));
            Assert.That(evaluate("count() * 10", mods()), Is.EqualTo(0));
        }

        [Test]
        public void NumericHelpers()
        {
            Assert.That(evaluate("clamp(5, 0, 3)", mods()), Is.EqualTo(3));
            Assert.That(evaluate("min(4, 2, 9)", mods()), Is.EqualTo(2));
            Assert.That(evaluate("max(4, 2, 9)", mods()), Is.EqualTo(9));
            Assert.That(evaluate("abs(0 - 2.5)", mods()), Is.EqualTo(2.5));
            Assert.That(evaluate("floor(2.9)", mods()), Is.EqualTo(2));
            Assert.That(evaluate("ceil(2.1)", mods()), Is.EqualTo(3));
            Assert.That(evaluate("round(2.5)", mods()), Is.EqualTo(3));
            Assert.That(evaluate("10 % 4", mods()), Is.EqualTo(2));
        }

        [Test]
        public void DivisionByZeroFallsBackToOne()
        {
            Assert.That(evaluate("1 / 0", mods()), Is.EqualTo(1));
            Assert.That(evaluate("0 / 0", mods()), Is.EqualTo(1));
        }

        [Test]
        public void NegativeResultFallsBackToOne()
        {
            Assert.That(evaluate("0 - 2", mods()), Is.EqualTo(1));
        }

        [Test]
        public void TypeMismatchFallsBackToOne()
        {
            Assert.That(evaluate("EZ + 1", new OsuModEasy()), Is.EqualTo(1));
            Assert.That(evaluate("1 && EZ", new OsuModEasy()), Is.EqualTo(1));
        }

        [Test]
        public void SyntaxErrorsAreReported()
        {
            Assert.That(ModMultRule.TryParse("EZ ?", out _, out string? error1), Is.False);
            Assert.That(error1, Is.Not.Empty);
            Assert.That(ModMultRule.TryParse("(EZ", out _, out _), Is.False);
            Assert.That(ModMultRule.TryParse("frobnicate(1)", out _, out _), Is.False);
            Assert.That(ModMultRule.TryParse("EZ = 2", out _, out _), Is.False);
        }

        [Test]
        public void ValidationCatchesUnknownNames()
        {
            var known = new Mod[] { new OsuModEasy(), new OsuModHidden(), new OsuModDoubleTime() };

            Assert.That(ModMultRule.TryParse("EZ ? 2", out var ok, out _), Is.True);
            Assert.That(ok!.Validate(known), Is.Null);

            Assert.That(ModMultRule.TryParse("ZZ ? 2", out var unknownMod, out _), Is.True);
            Assert.That(unknownMod!.Validate(known), Is.Not.Null);

            Assert.That(ModMultRule.TryParse("DT.not_a_setting ? 2 : 1", out var unknownSetting, out _), Is.True);
            Assert.That(unknownSetting!.Validate(known), Is.Not.Null);

            Assert.That(ModMultRule.TryParse("DT.speed_change", out var knownSetting, out _), Is.True);
            Assert.That(knownSetting!.Validate(known), Is.Null);
        }

        [Test]
        public void CommentsAreIgnored()
        {
            Assert.That(evaluate("# leading comment\nEZ ? 2 // trailing", new OsuModEasy()), Is.EqualTo(2));
        }

        [Test]
        public void ResolveSourcePrefersBeatmapRule()
        {
            Assert.That(ModMultRule.ResolveSource("EZ ? 2", "HD ? 3"), Is.EqualTo("EZ ? 2"));
            Assert.That(ModMultRule.ResolveSource(string.Empty, "HD ? 3"), Is.EqualTo("HD ? 3"));
            Assert.That(ModMultRule.ResolveSource("  ", null), Is.EqualTo(string.Empty));
        }

        [Test]
        public void TournamentExampleEndToEnd()
        {
            // Round default, map A/B/C/D overrides from the feature request.
            const string roundDefault = "EZ ? 2";
            const string mapB = "EZ && HD ? 2 : EZ ? 1.5";
            const string mapC = "(DA ? (9 - (DA.approach_rate ?? 9)) * 0.2 + 1 : 1) * (TC ? 1.1 : 1)";

            var da = new OsuModDifficultyAdjust();
            da.ApproachRate.Value = 8f;

            // (9-8)*0.2+1 = 1.2, times TC 1.1 = 1.32.
            Assert.That(evaluate(mapC, da, new OsuModTraceable()), Is.EqualTo(1.32).Within(1e-9));
            Assert.That(evaluate(mapC, da), Is.EqualTo(1.2).Within(1e-9));

            Assert.That(evaluate(mapB, new OsuModEasy(), new OsuModHidden()), Is.EqualTo(2));
            Assert.That(evaluate(mapB, new OsuModEasy()), Is.EqualTo(1.5));

            // Maps without overrides inherit the round default.
            Assert.That(evaluate(ModMultRule.ResolveSource(string.Empty, roundDefault), new OsuModEasy()), Is.EqualTo(2));
        }
    }
}
