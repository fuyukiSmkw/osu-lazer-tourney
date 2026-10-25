// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Game.Online.Multiplayer;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play.Leaderboards;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay.Components
{
    /// <summary>
    /// Leaderboard provider with combo-break observability and per-score custom multipliers
    /// for tournament spectating.
    /// <para>
    /// Each tracked score is adjusted as <c>round(total * weight(userId))</c> before being summed
    /// into <see cref="AdjustedTeamScores"/> (per-score evaluation: every player is weighted by
    /// their own mods, freestyle or not). <see cref="WeightFunction"/> defaults to x1, in which
    /// case the adjusted totals equal the raw <c>TeamScores</c>.
    /// </para>
    /// <para>
    /// The weight callback receives the live processor-side mod snapshot only as a
    /// fallback: that snapshot is built once and never refreshed upstream, so callers should
    /// prefer their own mod source (e.g. the Playing-time state) keyed by user id.
    /// </para>
    /// </summary>
    public partial class WeightedTournamentLeaderboardProvider : MultiSpectatorLeaderboardProvider
    {
        /// <summary>
        /// Per-score weight for a user. The second argument is the processor-side mod snapshot
        /// (fallback only; may be empty or stale, see class remarks).
        /// Must return a finite non-negative number; invalid results are clamped to x1 defensively.
        /// </summary>
        public Func<int, IReadOnlyList<Mod>, double> WeightFunction { get; set; } = (_, _) => 1;

        /// <summary>
        /// Team totals after per-score weights. Keyed exactly like <c>TeamScores</c> (0 = red, 1 = blue).
        /// Built in <see cref="LoadComplete"/> (not in load), so it is only valid afterwards.
        /// </summary>
        public readonly SortedDictionary<int, BindableLong> AdjustedTeamScores = new SortedDictionary<int, BindableLong>();

        /// <summary>
        /// Invoked once when load completes. Assigned before async load; used by the session to
        /// defer wiring that requires loaded state (clock flush, bindings). Cleared on invoke.
        /// </summary>
        public Action<WeightedTournamentLeaderboardProvider>? OnFirstLoad { get; set; }

        public WeightedTournamentLeaderboardProvider(MultiplayerRoomUser[] users)
            : base(users)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            foreach (int team in TeamScores.Keys)
                AdjustedTeamScores[team] = new BindableLong();

            foreach (var tracked in UserScores.Values)
            {
                tracked.ScoreProcessor.TotalScore.BindValueChanged(_ => Scheduler.AddOnce(recomputeAdjusted), true);
            }

            var callback = OnFirstLoad;
            OnFirstLoad = null;
            callback?.Invoke(this);
        }

        /// <summary>
        /// Recomputes adjusted totals with the current <see cref="WeightFunction"/>
        /// (e.g. after the active rule changed).
        /// </summary>
        public void RefreshWeights() => recomputeAdjusted();

        public bool TryGetCombo(int userId, out BindableInt? combo)
        {
            if (UserScores.TryGetValue(userId, out TrackedUserData data))
            {
                combo = data.ScoreProcessor.Combo;
                return true;
            }

            combo = null;
            return false;
        }

        private void recomputeAdjusted()
        {
            foreach (var total in AdjustedTeamScores.Values)
                total.Value = 0;

            foreach (var pair in UserScores)
            {
                var tracked = pair.Value;

                if (tracked.Team == null)
                    continue;

                if (!AdjustedTeamScores.TryGetValue(tracked.Team.Value, out var team))
                    continue;

                double weight;

                try
                {
                    weight = WeightFunction(pair.Key, tracked.ScoreProcessor.Mods);
                }
                catch
                {
                    weight = 1;
                }

                if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0)
                    weight = 1;

                team.Value += (long)Math.Round(tracked.ScoreProcessor.TotalScore.Value * weight);
            }
        }
    }
}
