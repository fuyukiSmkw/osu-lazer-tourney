// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay.Components;

namespace osu.Game.Rulesets.LazerTourney.Tests.Gameplay
{
    [TestFixture]
    public class TournamentSpectatorPlayerLoaderTest
    {
        [Test]
        public void TestLoaderDimmingRemovedAndMasterVolumeKept()
        {
            var track = new TrackVirtual(1000);
            track.Volume.Value = 0.8;

            var masterAdjustments = new AudioAdjustments();
            masterAdjustments.Volume.Value = 0.5;

            // Mirrors the live state when a single-player reset enters: the master clock's
            // mod-driven volume is bound to the shared track and PlayerLoader added its dim on top.
            track.BindAdjustments(masterAdjustments);
            track.AddAdjustment(AdjustableProperty.Volume, new BindableDouble(0.5));

            Assert.That(track.AggregateVolume.Value, Is.EqualTo(0.8 * 0.5 * 0.5).Within(1e-9));

            TournamentSpectatorPlayerLoader.ResetTrackVolume(track, masterAdjustments);

            // The track's own volume and the master's mod volume remain; the loader dim is detached.
            Assert.That(track.AggregateVolume.Value, Is.EqualTo(0.8 * 0.5).Within(1e-9));

            // The master's volume stays live after being re-attached by reference.
            masterAdjustments.Volume.Value = 0.25;

            Assert.That(track.AggregateVolume.Value, Is.EqualTo(0.8 * 0.25).Within(1e-9));
        }

        [Test]
        public void TestNullMasterAdjustmentsKeepsOnlyTrackVolume()
        {
            var track = new TrackVirtual(1000);
            track.Volume.Value = 0.75;
            track.AddAdjustment(AdjustableProperty.Volume, new BindableDouble(0));

            TournamentSpectatorPlayerLoader.ResetTrackVolume(track, null);

            Assert.That(track.AggregateVolume.Value, Is.EqualTo(0.75).Within(1e-9));
        }
    }
}
