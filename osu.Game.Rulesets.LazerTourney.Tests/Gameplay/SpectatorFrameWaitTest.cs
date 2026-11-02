// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay.Components;

namespace osu.Game.Rulesets.LazerTourney.Tests.Gameplay
{
    [TestFixture]
    public class SpectatorFrameWaitTest
    {
        [Test]
        public void TestWaitsBeforeFirstFrame()
        {
            // The live player is still playing but no bundle has arrived yet: keep the clock paused
            // rather than letting it advance without input.
            Assert.That(SpectatorFrameWait.ShouldWait(false, 0, false), Is.True);
        }

        [Test]
        public void TestWaitsForMoreFramesWhilePlaying()
        {
            // The frame-stable clock is mid-frame: the replay needs the next bundle.
            Assert.That(SpectatorFrameWait.ShouldWait(true, 120, false), Is.True);
        }

        [Test]
        public void TestDoesNotWaitAfterCompletionWithFrames()
        {
            // All frames were received and the replay ended normally.
            Assert.That(SpectatorFrameWait.ShouldWait(false, 120, true), Is.False);
        }

        [Test]
        public void TestDoesNotWaitAfterCompletionWithoutFrames()
        {
            // Regression: the local player loaded so slowly that the live user passed before any
            // bundle arrived. The base implementation keeps waiting forever on an empty replay,
            // which freezes the clock before the map end and suppresses the results screen.
            Assert.That(SpectatorFrameWait.ShouldWait(false, 0, true), Is.False);
        }

        [Test]
        public void TestFrameStableWaitStillWins()
        {
            // A mid-frame wait is independent of replay completion and must always be honoured.
            Assert.That(SpectatorFrameWait.ShouldWait(true, 0, true), Is.True);
            Assert.That(SpectatorFrameWait.ShouldWait(true, 120, true), Is.True);
        }
    }
}
