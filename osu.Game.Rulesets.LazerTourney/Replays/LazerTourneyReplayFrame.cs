// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Replays;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Replays
{
    public class LazerTourneyReplayFrame : ReplayFrame
    {
        public List<LazerTourneyAction> Actions = [];
        public Vector2 Position;

        public override bool IsEquivalentTo(ReplayFrame other)
            => other is LazerTourneyReplayFrame freeformFrame && Time == freeformFrame.Time && Position == freeformFrame.Position && Actions.SequenceEqual(freeformFrame.Actions);
    }
}
