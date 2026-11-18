// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay.Components
{
    /// <summary>
    /// Decides whether a spectated player clock must keep waiting for replay frames.
    /// </summary>
    /// <remarks>
    /// <see cref="osu.Game.Screens.OnlinePlay.Multiplayer.Spectate.MultiSpectatorPlayer"/> treats an
    /// empty replay as permanently waiting. That is correct while the live player is still sending
    /// frames, but wrong once the play has ended: if the local player never received a bundle
    /// (e.g. it was still loading when the live player passed), the clock would wait forever and
    /// the cell would never reach its results screen. Once
    /// <see cref="osu.Game.Replays.Replay.HasReceivedAllFrames"/> is set there is nothing left to
    /// wait for, even with zero frames.
    /// </remarks>
    public static class SpectatorFrameWait
    {
        /// <summary>
        /// Whether the clock should wait for more frames.
        /// </summary>
        /// <param name="frameStableClockWaiting">The player's frame-stable clock is waiting for the next replay frame.</param>
        /// <param name="receivedFrameCount">The number of frames currently in the local replay.</param>
        /// <param name="hasReceivedAllFrames">The live player has finished and no more frames will arrive.</param>
        public static bool ShouldWait(bool frameStableClockWaiting, int receivedFrameCount, bool hasReceivedAllFrames)
            => frameStableClockWaiting || (receivedFrameCount == 0 && !hasReceivedAllFrames);
    }
}
