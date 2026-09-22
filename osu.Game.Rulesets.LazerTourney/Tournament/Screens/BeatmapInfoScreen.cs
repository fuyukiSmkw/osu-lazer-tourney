// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.LazerTourney.Tournament.Components;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Online;
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
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

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
        private void load(TournamentOnlineState onlineState)
        {
            onlineStateRef = onlineState;
            onlineState.CurrentPlaylistItem.BindValueChanged(_ => Scheduler.AddOnce(updateSongBar));
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
                // Spectating (or a retained result screen): map and ruleset come from the score,
                // gameplay mods come from the room's required mods.
                setFromRoomItem(score.ScoreInfo.BeatmapInfo, score.ScoreInfo.Ruleset, spectateSession.SpectatedItem);
                return;
            }

            var liveItem = onlineStateRef.CurrentPlaylistItem.Value;

            if (liveItem == null)
            {
                SongBar.SetSource(null, null, Array.Empty<Mod>(), Array.Empty<Mod>(), null);
                return;
            }

            var round = CurrentMatch.Value?.Round.Value;
            IBeatmapInfo? info = round?.Beatmaps.FirstOrDefault(b => b.ID == liveItem.BeatmapID)?.Beatmap
                                 ?? (IBeatmapInfo?)beatmaps.QueryBeatmap(b => b.OnlineID == liveItem.BeatmapID);

            setFromRoomItem(info, rulesets.GetRuleset(liveItem.RulesetID), liveItem);
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

        private TournamentOnlineState onlineStateRef = null!;
    }
}
