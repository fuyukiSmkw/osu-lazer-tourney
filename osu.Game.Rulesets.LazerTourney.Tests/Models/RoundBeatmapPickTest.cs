// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;

namespace osu.Game.Rulesets.LazerTourney.Tests.Models
{
    [TestFixture]
    public class RoundBeatmapPickTest
    {
        [Test]
        public void TestInferredPickUsesClassificationModsAndOrder()
        {
            var round = new TournamentRound();
            var firstHd = new RoundBeatmap { Mods = "HD" };
            var dt = new RoundBeatmap { Mods = "DT" };
            var secondHd = new RoundBeatmap { Mods = "HD" };
            var thirdHd = new RoundBeatmap { Mods = "HD" };

            round.Beatmaps.Add(firstHd);
            round.Beatmaps.Add(dt);
            round.Beatmaps.Add(secondHd);
            round.Beatmaps.Add(thirdHd);

            Assert.That(firstHd.GetInferredPick(round), Is.EqualTo("HD1"));
            Assert.That(secondHd.GetInferredPick(round), Is.EqualTo("HD2"));
            Assert.That(thirdHd.GetInferredPick(round), Is.EqualTo("HD3"));
            Assert.That(dt.GetInferredPick(round), Is.EqualTo("DT1"));
        }

        [Test]
        public void TestEmptyModsStillProducesNumberedPick()
        {
            var round = new TournamentRound();
            var first = new RoundBeatmap();
            var second = new RoundBeatmap();

            round.Beatmaps.Add(first);
            round.Beatmaps.Add(second);

            Assert.That(first.GetInferredPick(round), Is.EqualTo("1"));
            Assert.That(second.GetInferredPick(round), Is.EqualTo("2"));
        }

        [Test]
        public void TestNullPickIsNotSerialised()
        {
            string json = JsonConvert.SerializeObject(new RoundBeatmap { Mods = "HD" });

            Assert.That(json, Does.Not.Contain("Pick"));
        }

        [Test]
        public void TestEmptyPickIsSerialised()
        {
            string json = JsonConvert.SerializeObject(new RoundBeatmap { Mods = "HD", Pick = string.Empty });

            Assert.That(json, Does.Contain("\"Pick\":\"\""));
        }

        [Test]
        public void TestPickRoundTrips()
        {
            var beatmap = new RoundBeatmap { Mods = "HD", Pick = "custom" };
            var deserialised = JsonConvert.DeserializeObject<RoundBeatmap>(JsonConvert.SerializeObject(beatmap));

            Assert.That(deserialised!.Pick, Is.EqualTo("custom"));
        }
    }
}
