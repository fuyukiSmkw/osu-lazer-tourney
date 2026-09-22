// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.LazerTourney.Beatmaps;
using osu.Game.Rulesets.LazerTourney.Graphics;
using osu.Game.Rulesets.LazerTourney.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.LazerTourney
{
    public partial class LazerTourneyRuleset : Ruleset
    {
        public override string Description => "lazer!tourney";

        public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod> mods = null) =>
            new DrawableLazerTourneyRuleset(this, beatmap, mods);

        public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) =>
            new LazerTourneyBeatmapConverter(beatmap, this);

        public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) =>
            new LazerTourneyDifficultyCalculator(RulesetInfo, beatmap);

        public override IEnumerable<Mod> GetModsFor(ModType type) => [];

        public override string ShortName => SHORT_NAME;

        public static readonly string SHORT_NAME = "lazertourney";

        public override IEnumerable<KeyBinding> GetDefaultKeyBindings(int variant = 0) => [];

        public override Drawable CreateIcon() => new IconWithListenerLoader();

        public partial class IconWithListenerLoader : LazerTourneyIcon
        {
            public IconWithListenerLoader()
            {
                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader(permitNulls: true)]
            private void load(OsuGame game, Storage storage, IModelImporter<BeatmapSetInfo> beatmapImporter, IAPIProvider api)
            {
                try
                {
                    Logging.Log("Begin init ListenerLoader");
                    Logging.Log($"Deps: Game = '{game}' :: Storage = '{storage}' :: Importer = '{beatmapImporter}' :: IAPIProvider = '{api}'");

                    if (!ListenerLoader.ListenerLoader.INSTANCE.BeginInject(storage, game, Scheduler))
                    {
                        Logging.Log("Injection failed!", level: LogLevel.Error);
                        return;
                    }
                }
                catch (Exception e)
                {
                    Logging.LogError(e, "Unknown exception");
                }
            }
        }

        // Leave this line intact. It will bake the correct version into the ruleset on each build/release.
        public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;
    }
}
