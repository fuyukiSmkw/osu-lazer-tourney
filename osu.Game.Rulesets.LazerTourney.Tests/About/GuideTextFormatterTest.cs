// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.About;

namespace osu.Game.Rulesets.LazerTourney.Tests.About
{
    [TestFixture]
    public class GuideTextFormatterTest
    {
        [Test]
        public void PlainTextIsSingleSegment()
        {
            var segments = GuideTextFormatter.Parse("hello world");

            Assert.That(segments, Has.Count.EqualTo(1));
            Assert.That(segments[0].Text, Is.EqualTo("hello world"));
            Assert.That(segments[0].Bold, Is.False);
            Assert.That(segments[0].Code, Is.False);
        }

        [Test]
        public void CodeSpanIsIsolated()
        {
            var segments = GuideTextFormatter.Parse("use `EZ ? 2` here");

            Assert.That(segments, Has.Count.EqualTo(3));
            Assert.That(segments[0].Text, Is.EqualTo("use "));
            Assert.That(segments[1].Text, Is.EqualTo("EZ ? 2"));
            Assert.That(segments[1].Code, Is.True);
            Assert.That(segments[2].Text, Is.EqualTo(" here"));
            Assert.That(segments[2].Code, Is.False);
        }

        [Test]
        public void BoldSpanIsIsolated()
        {
            var segments = GuideTextFormatter.Parse("a **bold** word");

            Assert.That(segments, Has.Count.EqualTo(3));
            Assert.That(segments[1].Text, Is.EqualTo("bold"));
            Assert.That(segments[1].Bold, Is.True);
            Assert.That(segments[1].Code, Is.False);
        }

        [Test]
        public void MarkersNest()
        {
            var segments = GuideTextFormatter.Parse("**defaults to `1`**");

            Assert.That(segments, Has.Count.EqualTo(2));
            Assert.That(segments[0].Text, Is.EqualTo("defaults to "));
            Assert.That(segments[0].Bold, Is.True);
            Assert.That(segments[0].Code, Is.False);
            Assert.That(segments[1].Text, Is.EqualTo("1"));
            Assert.That(segments[1].Bold, Is.True);
            Assert.That(segments[1].Code, Is.True);
        }

        [Test]
        public void AdjacentMarkersProduceNoEmptySegments()
        {
            var segments = GuideTextFormatter.Parse("`a`**b**");

            Assert.That(segments, Has.Count.EqualTo(2));
            Assert.That(segments[0].Text, Is.EqualTo("a"));
            Assert.That(segments[1].Text, Is.EqualTo("b"));
        }
    }
}
