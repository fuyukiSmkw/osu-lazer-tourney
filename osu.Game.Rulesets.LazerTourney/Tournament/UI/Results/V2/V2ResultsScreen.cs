// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;

namespace osu.Game.Rulesets.LazerTourney.Tournament.UI.Results.V2
{
    /// <summary>
    /// Tournament result screen using the V2 overview design originally developed in the
    /// g0v0 client fork. It hosts <see cref="V2ResultsPanel"/> and keeps the inherited
    /// lazer layout hidden so only the V2 layout is visible.
    /// </summary>
    [Cached]
    public partial class V2ResultsScreen : ResultsScreen
    {
        public readonly BindableBool IsPersonalBest = new BindableBool();
        public readonly Bindable<ScoreInfo?> ComparisonScore = new Bindable<ScoreInfo?>();

        public V2ResultsScreen(ScoreInfo score)
            : base(score)
        {
            // The inherited classic layout is hidden below. Clearing the selection prevents its
            // hidden statistics panel and score list from doing redundant work for this play.
            SelectedScore.Value = null;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (Score == null)
                return;

            // The base ResultsScreen builds the classic lazer layout. Keep it alive (its load
            // completion and update paths still run) but make it invisible and non-interactive.
            foreach (var child in InternalChildren.ToArray())
            {
                child.Alpha = 0;
            }

            AddInternal(new V2ResultsPanel(Score, withFlair: true));
        }
    }
}
