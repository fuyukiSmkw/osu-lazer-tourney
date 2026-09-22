// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Online.Multiplayer;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens
{
    /// <summary>
    /// The high-level state of the currently running match, used to drive screen progression.
    /// </summary>
    /// <remarks>
    /// This replaces the stable IPC <c>TourneyState</c> with one derived from the multiplayer room state.
    /// </remarks>
    public enum TourneyState
    {
        /// <summary>
        /// No room is joined or the room is idle.
        /// </summary>
        Idle,

        /// <summary>
        /// Players are loading the beatmap.
        /// </summary>
        WaitingForLoad,

        /// <summary>
        /// Gameplay is in progress.
        /// </summary>
        Playing,

        /// <summary>
        /// Gameplay has finished and results are being shown.
        /// </summary>
        Ranking,
    }

    public static class MultiplayerRoomStateExtensions
    {
        public static TourneyState ToTourneyState(this MultiplayerRoomState state)
        {
            switch (state)
            {
                case MultiplayerRoomState.WaitingForLoad:
                    return TourneyState.WaitingForLoad;

                case MultiplayerRoomState.Playing:
                    return TourneyState.Playing;

                default:
                    return TourneyState.Idle;
            }
        }
    }
}
