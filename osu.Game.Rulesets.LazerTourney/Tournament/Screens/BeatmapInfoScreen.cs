// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.LazerTourney.Tournament.Components;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens
{
    public abstract partial class BeatmapInfoScreen : TournamentMatchScreen
    {
        protected readonly SongBar SongBar;

        [Resolved]
        private SpectateSession spectateSession { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> workingBeatmap { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> globalRuleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> globalMods { get; set; } = null!;

        protected BeatmapInfoScreen()
        {
            AddInternal(SongBar = new SongBar
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Depth = float.MinValue,
            });
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            workingBeatmap.BindValueChanged(_ => Scheduler.AddOnce(updateSongBar));
            globalRuleset.BindValueChanged(_ => Scheduler.AddOnce(updateSongBar));
            globalMods.BindValueChanged(_ => Scheduler.AddOnce(updateSongBar));
            spectateSession.SpectatedScore.BindValueChanged(_ => Scheduler.AddOnce(updateSongBar));
            CurrentMatch.BindValueChanged(_ => Scheduler.AddOnce(updateSongBar));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateSongBar();
        }

        private void updateSongBar()
        {
            if (!IsLoaded)
                return;

            var score = spectateSession.SpectatedScore.Value;

            if (score != null)
            {
                // Spectating (or a retained result screen): lock to the score's map,
                // mods and ruleset until the next match starts or spectate is reset.
                setFromRoomItem(score.ScoreInfo.BeatmapInfo, score.ScoreInfo.Ruleset, spectateSession.SpectatedItem);
                return;
            }

            // Otherwise follow the globally playing beatmap (room preview, spectate master
            // clock, or local showcase replay) rather than the room's current item.
            var working = workingBeatmap.Value;
            var info = working?.BeatmapInfo;

            if (info == null || working!.Beatmap.HitObjects.Count == 0)
            {
                SongBar.SetSource(null, null, Array.Empty<Mod>(), Array.Empty<Mod>(), null);
                return;
            }

            var ruleset = globalRuleset.Value;
            Mod[] mods = globalMods.Value.ToArray();

            RoundBeatmap? poolMatch = null;

            if (info.OnlineID > 0)
                poolMatch = CurrentMatch.Value?.Round.Value?.Beatmaps.FirstOrDefault(b => b.ID == info.OnlineID);

            SongBar.SetSource(info, ruleset, mods, mods, poolMatch);
            SongBar.FadeInFromZero(300, Easing.OutQuint);
        }

        private void setFromRoomItem(IBeatmapInfo? info, RulesetInfo? ruleset, MultiplayerPlaylistItem? item)
        {
            Mod[] required = Array.Empty<Mod>();

            if (ruleset != null && item != null)
                required = RoundBeatmap.InstantiateMods(item.RequiredMods.ToArray(), ruleset.CreateInstance());

            RoundBeatmap? poolMatch = null;

            if (item != null)
            {
                var round = CurrentMatch.Value?.Round.Value;
                poolMatch = round?.Beatmaps.FirstOrDefault(b => RoundBeatmap.Matches(item, b, LadderInfo.Ruleset.Value?.OnlineID));
            }

            SongBar.SetSource(info, ruleset, required, required, poolMatch);
            SongBar.FadeInFromZero(300, Easing.OutQuint);
        }
    }
}
