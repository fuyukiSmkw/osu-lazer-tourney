// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Rulesets.LazerTourney.Objects;
using osu.Game.Rulesets.Replays;

namespace osu.Game.Rulesets.LazerTourney.Replays
{
    public class LazerTourneyAutoGenerator : AutoGenerator<LazerTourneyReplayFrame>
    {
        public new Beatmap<LazerTourneyHitObject> Beatmap => (Beatmap<LazerTourneyHitObject>)base.Beatmap;

        public LazerTourneyAutoGenerator(IBeatmap beatmap)
            : base(beatmap)
        {
        }

        protected override void GenerateFrames()
        {
        }
    }
}
