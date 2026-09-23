// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// Isolates a player instance from the game-wide ruleset/beatmap/mods.
    /// </summary>
    public partial class PlayerIsolationContainer : Container
    {
        [Cached]
        [Cached(typeof(IBindable<RulesetInfo>))]
        private readonly Bindable<RulesetInfo> ruleset = new Bindable<RulesetInfo>();

        [Cached]
        [Cached(typeof(IBindable<WorkingBeatmap>))]
        private readonly Bindable<WorkingBeatmap> beatmap = new Bindable<WorkingBeatmap>();

        [Cached]
        [Cached(typeof(IBindable<IReadOnlyList<Mod>>))]
        private readonly Bindable<IReadOnlyList<Mod>> mods = new Bindable<IReadOnlyList<Mod>>();

        public PlayerIsolationContainer(WorkingBeatmap beatmap, RulesetInfo ruleset, IReadOnlyList<Mod> mods)
        {
            this.beatmap.Value = beatmap;
            this.ruleset.Value = ruleset;
            this.mods.Value = mods;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs(ruleset.BeginLease(false));
            dependencies.CacheAs(beatmap.BeginLease(false));
            dependencies.CacheAs(mods.BeginLease(false));
            return dependencies;
        }
    }
}
