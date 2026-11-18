// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Models
{
    public class SeedingResult
    {
        /// <summary>
        /// Beatmaps of this seeding result, in display order.
        /// A <see cref="BindableList{T}"/> (rather than a plain list) so the editor can drag them around.
        /// </summary>
        public BindableList<SeedingBeatmap> Beatmaps = new BindableList<SeedingBeatmap>();

        public Bindable<string> Mod = new Bindable<string>(string.Empty);

        public Bindable<int> Seed = new BindableInt
        {
            MinValue = 1,
            MaxValue = 256
        };
    }
}
