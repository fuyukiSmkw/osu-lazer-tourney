// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Replays;
using osu.Game.Rulesets.Replays;

namespace osu.Game.Rulesets.LazerTourney.Replays
{
    public class LazerTourneyFramedReplayInputHandler(Replay replay) : FramedReplayInputHandler<LazerTourneyReplayFrame>(replay)
    {
        protected override bool IsImportant(LazerTourneyReplayFrame frame) => false;
    }
}
