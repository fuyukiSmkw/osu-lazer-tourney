// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Input;
using osu.Game.Beatmaps;
using osu.Game.Input.Handlers;
using osu.Game.Replays;
using osu.Game.Rulesets.LazerTourney.Objects;
using osu.Game.Rulesets.LazerTourney.Objects.Drawables;
using osu.Game.Rulesets.LazerTourney.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.LazerTourney.UI
{
    [Cached]
    public partial class DrawableLazerTourneyRuleset(LazerTourneyRuleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod> mods = null) : DrawableRuleset<LazerTourneyHitObject>(ruleset, beatmap, mods)
    {
        protected override Playfield CreatePlayfield() => new LazerTourneyPlayfield();

        protected override ReplayInputHandler CreateReplayInputHandler(Replay replay) => new LazerTourneyFramedReplayInputHandler(replay);

        public override DrawableHitObject<LazerTourneyHitObject> CreateDrawableRepresentation(LazerTourneyHitObject h) => new DrawableLazerTourneyHitObject(h);

        protected override PassThroughInputManager CreateInputManager() => new LazerTourneyInputManager(Ruleset?.RulesetInfo);
    }
}
