// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
namespace osu.Game.Rulesets.LazerTourney.Tests.Models
{
    /// <summary>
    /// Validates the startup check that detects mappool entries the selected ruleset cannot process.
    /// Naming official rulesets here is safe: test assemblies are excluded from ruleset scanning.
    /// </summary>
    [TestFixture]
    public class RoundBeatmapValidatorTest
    {
        [Test]
        public void BeatmapWithMissingRulesetIsValidForOsuButInvalidForMania()
        {
            // Old brackets can store an APIRuleset with OnlineID -1; its ShortName throws.
            TournamentBeatmap beatmap = createBeatmap(new APIBeatmap.APIRuleset());

            Assert.That(RoundBeatmapValidator.TryGetInvalidReason(createRoundBeatmap(beatmap), new OsuRuleset(), out _), Is.False);
            Assert.That(RoundBeatmapValidator.TryGetInvalidReason(createRoundBeatmap(beatmap), new ManiaRuleset(), out string reason), Is.True);
            Assert.That(reason, Is.Not.Empty);
        }

        [Test]
        public void BeatmapFromAnotherRulesetIsInvalid()
        {
            TournamentBeatmap beatmap = createBeatmap(new OsuRuleset().RulesetInfo);

            Assert.That(RoundBeatmapValidator.TryGetInvalidReason(createRoundBeatmap(beatmap), new OsuRuleset(), out _), Is.False);
            Assert.That(RoundBeatmapValidator.TryGetInvalidReason(createRoundBeatmap(beatmap), new ManiaRuleset(), out string reason), Is.True);
            Assert.That(reason, Does.Contain("osu"));
        }

        [Test]
        public void BeatmapWithUnknownModsIsInvalid()
        {
            TournamentBeatmap beatmap = createBeatmap(new OsuRuleset().RulesetInfo);

            var known = createRoundBeatmap(beatmap);
            known.RequiredMods = new[] { new APIMod { Acronym = "HD" } };
            Assert.That(RoundBeatmapValidator.TryGetInvalidReason(known, new OsuRuleset(), out _), Is.False);

            var unknown = createRoundBeatmap(beatmap);
            unknown.RequiredMods = new[] { new APIMod { Acronym = "XX" } };
            Assert.That(RoundBeatmapValidator.TryGetInvalidReason(unknown, new OsuRuleset(), out _), Is.True);

            var unknownAllowed = createRoundBeatmap(beatmap);
            unknownAllowed.AllowedMods = new[] { new APIMod { Acronym = "XX" } };
            Assert.That(RoundBeatmapValidator.TryGetInvalidReason(unknownAllowed, new OsuRuleset(), out _), Is.True);
        }

        [Test]
        public void ClearingBeatmapKeepsSlotButErasesBeatmapData()
        {
            var round = new TournamentRound();

            var cleared = createRoundBeatmap(createBeatmap(new OsuRuleset().RulesetInfo));
            cleared.ID = 123;
            cleared.Freestyle = true;
            cleared.RequiredMods = new[] { new APIMod { Acronym = "HD" } };
            cleared.AllowedMods = new[] { new APIMod { Acronym = "HD" } };

            var untouched = createRoundBeatmap(createBeatmap(new OsuRuleset().RulesetInfo));
            untouched.ID = 456;

            round.Beatmaps.Add(cleared);
            round.Beatmaps.Add(untouched);

            RoundBeatmapValidator.ClearBeatmapData(cleared);

            Assert.That(round.Beatmaps, Has.Count.EqualTo(2));
            Assert.That(round.Beatmaps[0], Is.SameAs(cleared));
            Assert.That(cleared.ID, Is.EqualTo(0));
            Assert.That(cleared.Beatmap, Is.Null);
            Assert.That(cleared.Freestyle, Is.False);
            Assert.That(cleared.RequiredMods, Is.Empty);
            Assert.That(cleared.AllowedMods, Is.Empty);

            Assert.That(round.Beatmaps[1], Is.SameAs(untouched));
            Assert.That(untouched.ID, Is.EqualTo(456));
            Assert.That(untouched.Beatmap, Is.Not.Null);
        }

        [Test]
        public void DescribeFormatsBeatmapName()
        {
            var roundBeatmap = createRoundBeatmap(createBeatmap(new OsuRuleset().RulesetInfo));

            Assert.That(RoundBeatmapValidator.Describe(roundBeatmap), Is.EqualTo("Artist - Title [Insane]"));
            Assert.That(RoundBeatmapValidator.Describe(new RoundBeatmap { ID = 42 }), Is.EqualTo("beatmap #42"));
        }

        private static RoundBeatmap createRoundBeatmap(TournamentBeatmap beatmap) => new RoundBeatmap
        {
            ID = beatmap.OnlineID,
            Beatmap = beatmap,
            RequiredMods = Array.Empty<APIMod>(),
            AllowedMods = Array.Empty<APIMod>(),
        };

        private static TournamentBeatmap createBeatmap(IRulesetInfo ruleset) => new TournamentBeatmap
        {
            OnlineID = 123456,
            DifficultyName = "Insane",
            Metadata = new BeatmapMetadata
            {
                Artist = "Artist",
                Title = "Title",
            },
            Difficulty = new BeatmapDifficulty(),
            Ruleset = ruleset,
        };
    }
}
