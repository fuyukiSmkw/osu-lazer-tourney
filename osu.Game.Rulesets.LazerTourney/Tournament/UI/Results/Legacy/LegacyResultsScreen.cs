// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
// Ported from the GooGuTeam/g0v0 legacy results screen.

using System.Linq;
using osu.Framework.Allocation;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;

namespace osu.Game.Rulesets.LazerTourney.Tournament.UI.Results.Legacy
{
    /// <summary>
    /// Tournament result screen using the osu!stable-style legacy layout ported from the
    /// Torii client via the GooGuTeam/g0v0 fork. The inherited lazer layout is kept loaded
    /// but invisible, and the navigation chrome (retry, watch replay, favourites, online
    /// ranking) is omitted because tournament cells are display-only.
    /// </summary>
    [Cached]
    public partial class LegacyResultsScreen : ResultsScreen
    {
        public LegacyResultsScreen(ScoreInfo score)
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

            foreach (var child in InternalChildren.ToArray())
                child.Alpha = 0;

            AddInternal(new LegacyResultsPanel(Score));

            // The stock accuracy circle normally triggers the applause at the end of its flair
            // animation, but that layout is hidden. Trigger it here when the stable grade reveal
            // is due instead.
            Scheduler.AddDelayed(() => PlayApplause(Score.Rank), 3000);
        }
    }
}
