// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.LazerTourney.Tournament.UI.Results.V2
{
    public static class V2ScoreInfoExtensions
    {
        /// <summary>
        /// A perfect full combo requires both perfect accuracy and the complete achievable combo.
        /// Bonus judgements are intentionally excluded: spinner bonus does not determine PFC.
        /// </summary>
        public static bool IsPerfectComboV2(this ScoreInfo score)
            => score.Passed && score.Rank != ScoreRank.F
               && score.MaxCombo == score.GetMaximumAchievableCombo();
        //    && score.GetMaximumAchievableCombo() > 0 && score.MaxCombo == score.GetMaximumAchievableCombo()
        //    && !score.Statistics.Any(s => s.Value > 0 && (s.Key.BreaksCombo() || (s.Key.IsMiss() && s.Key.AffectsAccuracy())));
    }
}
