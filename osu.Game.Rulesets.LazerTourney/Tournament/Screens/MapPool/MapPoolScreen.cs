// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.LazerTourney.Tournament.Components;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Online;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay.Components;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.MapPool
{
    public partial class MapPoolScreen : TournamentMatchScreen
    {
        private FillFlowContainer<FillFlowContainer<TournamentBeatmapPanel>> mapFlows = null!;

        [Resolved]
        private TournamentSceneManager? sceneManager { get; set; }

        [Resolved]
        private TournamentOnlineState onlineState { get; set; } = null!;

        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        private TeamColour pickColour;
        private ChoiceType pickType;

        private OsuButton buttonRedBan = null!;
        private OsuButton buttonBlueBan = null!;
        private OsuButton buttonRedPick = null!;
        private OsuButton buttonBluePick = null!;

        private ScheduledDelegate? scheduledScreenChange;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new TourneyVideo("mappool")
                {
                    Loop = true,
                    RelativeSizeAxes = Axes.Both,
                },
                new MatchHeader
                {
                    ShowScores = true,
                },
                mapFlows = new FillFlowContainer<FillFlowContainer<TournamentBeatmapPanel>>
                {
                    Y = 160,
                    Spacing = new Vector2(10, 10),
                    Direction = FillDirection.Vertical,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                },
                new ControlPanel
                {
                    Children = new Drawable[]
                    {
                        new TournamentSpriteText
                        {
                            Text = "Current Mode"
                        },
                        buttonRedBan = new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Red Ban",
                            Action = () => setMode(TeamColour.Red, ChoiceType.Ban)
                        },
                        buttonBlueBan = new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Blue Ban",
                            Action = () => setMode(TeamColour.Blue, ChoiceType.Ban)
                        },
                        buttonRedPick = new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Red Pick",
                            Action = () => setMode(TeamColour.Red, ChoiceType.Pick)
                        },
                        buttonBluePick = new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Blue Pick",
                            Action = () => setMode(TeamColour.Blue, ChoiceType.Pick)
                        },
                        new ControlPanel.Spacer(),
                        new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Reset",
                            Action = reset
                        },
                        new ControlPanel.Spacer(),
                        new OsuCheckbox
                        {
                            LabelText = "Split display by mods",
                            Current = LadderInfo.SplitMapPoolByMods,
                        },
                        new ControlPanel.Spacer(),
                        new OsuCheckbox
                        {
                            LabelText = "Show category mod icons",
                            Current = LadderInfo.DisplayCategoryModIcon,
                        },
                        new OsuCheckbox
                        {
                            LabelText = "Show required mods",
                            Current = LadderInfo.DisplayRequiredMods,
                        },
                    },
                }
            };

            onlineState.CurrentPlaylistItem.BindValueChanged(item =>
            {
                var newItem = item.NewValue;

                if (newItem == null)
                    return;

                // Highlight the round map only on full equality (beatmap, ruleset, freestyle and mods).
                // The matched entry drives the existing panel flash via addForBeatmap below.
                var round = LadderInfo.CurrentMatch.Value?.Round.Value;
                var match = round?.Beatmaps.FirstOrDefault(b => isRoomItemMatch(b, newItem));

                if (match?.Beatmap?.OnlineID > 0)
                    addForBeatmap(match.Beatmap.OnlineID);
            });
        }

        /// <summary>
        /// Whether a live room playlist item represents the given round map.
        /// </summary>
        private bool isRoomItemMatch(RoundBeatmap map, MultiplayerPlaylistItem item)
            => RoundBeatmap.Matches(item, map, LadderInfo.Ruleset.Value?.OnlineID);

        private Bindable<bool>? splitMapPoolByMods;
        private Bindable<bool>? displayCategoryModIcon;
        private Bindable<bool>? displayRequiredMods;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            splitMapPoolByMods = LadderInfo.SplitMapPoolByMods.GetBoundCopy();
            displayCategoryModIcon = LadderInfo.DisplayCategoryModIcon.GetBoundCopy();
            displayRequiredMods = LadderInfo.DisplayRequiredMods.GetBoundCopy();
            splitMapPoolByMods.BindValueChanged(_ => updateDisplay());
            displayCategoryModIcon.BindValueChanged(_ => updateDisplay());
            displayRequiredMods.BindValueChanged(_ => updateDisplay());
        }

        private void setMode(TeamColour colour, ChoiceType choiceType)
        {
            pickColour = colour;
            pickType = choiceType;

            buttonRedBan.Colour = setColour(pickColour == TeamColour.Red && pickType == ChoiceType.Ban);
            buttonBlueBan.Colour = setColour(pickColour == TeamColour.Blue && pickType == ChoiceType.Ban);
            buttonRedPick.Colour = setColour(pickColour == TeamColour.Red && pickType == ChoiceType.Pick);
            buttonBluePick.Colour = setColour(pickColour == TeamColour.Blue && pickType == ChoiceType.Pick);

            static Color4 setColour(bool active) => active ? Color4.White : Color4.Gray;
        }

        private void setNextMode()
        {
            if (CurrentMatch.Value?.Round.Value == null)
                return;

            int totalBansRequired = CurrentMatch.Value.Round.Value.BanCount.Value * 2;

            TeamColour lastPickColour = CurrentMatch.Value.PicksBans.LastOrDefault()?.Team ?? TeamColour.Red;

            TeamColour nextColour;

            bool hasAllBans = CurrentMatch.Value.PicksBans.Count(p => p.Type == ChoiceType.Ban) >= totalBansRequired;

            if (!hasAllBans)
            {
                // Ban phase: switch teams every second ban.
                nextColour = CurrentMatch.Value.PicksBans.Count % 2 == 1
                    ? getOppositeTeamColour(lastPickColour)
                    : lastPickColour;
            }
            else
            {
                // Pick phase : switch teams every pick, except for the first pick which generally goes to the team that placed the last ban.
                nextColour = pickType == ChoiceType.Pick
                    ? getOppositeTeamColour(lastPickColour)
                    : lastPickColour;
            }

            setMode(nextColour, hasAllBans ? ChoiceType.Pick : ChoiceType.Ban);

            TeamColour getOppositeTeamColour(TeamColour colour) => colour == TeamColour.Red ? TeamColour.Blue : TeamColour.Red;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            var maps = mapFlows.Select(f => f.FirstOrDefault(m => m.ReceivePositionalInputAt(e.ScreenSpaceMousePosition)));
            var map = maps.FirstOrDefault(m => m != null);

            if (map != null)
            {
                if (e.Button == MouseButton.Left && map.Beatmap?.OnlineID > 0)
                {
                    // Referees (host or referee role) also push the map to the room's current item.
                    // Local pick/ban bookkeeping below runs for everyone.
                    if (map.RoundMap != null)
                        applyToRoom(map.RoundMap);

                    addForBeatmap(map.Beatmap.OnlineID);
                }
                else
                {
                    var existing = CurrentMatch.Value?.PicksBans.FirstOrDefault(p => p.BeatmapID == map.Beatmap?.OnlineID);

                    if (existing != null)
                    {
                        CurrentMatch.Value?.PicksBans.Remove(existing);
                        setNextMode();
                    }
                }

                return true;
            }

            return base.OnMouseDown(e);
        }

        /// <summary>
        /// Replaces the room's current playlist item with the given round map
        /// (including its freestyle/required/allowed mods). No-op without host or referee rights.
        /// Local pick/ban bookkeeping is handled separately by the caller.
        /// </summary>
        private void applyToRoom(RoundBeatmap roundMap)
        {
            try
            {
                if (!onlineState.RoomJoined.Value)
                    return;

                if (!onlineState.IsHost.Value && !onlineState.IsReferee.Value)
                    return;

                var room = client.Room;

                if (room == null)
                    return;

                var rulesetInfo = LadderInfo.Ruleset.Value;

                if (rulesetInfo == null)
                    return;

                // Local checksum for the server; empty when the beatmap is not downloaded.
                string checksum = beatmaps.QueryBeatmap(b => b.OnlineID == roundMap.ID)?.MD5Hash ?? string.Empty;

                APIMod[] requiredMods = roundMap.SafeRequiredMods;
                APIMod[] allowedMods = roundMap.Freestyle ? Array.Empty<APIMod>() : roundMap.SafeAllowedMods;

                var current = room.Playlist.FirstOrDefault(i => i.ID == room.Settings.PlaylistItemId);

                if (current != null && !current.Expired)
                {
                    var edited = current.Clone();
                    edited.BeatmapID = roundMap.ID;
                    edited.BeatmapChecksum = checksum;
                    edited.RulesetID = rulesetInfo.OnlineID;
                    edited.Freestyle = roundMap.Freestyle;
                    edited.RequiredMods = requiredMods;
                    edited.AllowedMods = allowedMods;

                    client.EditPlaylistItem(edited).FireAndForget(onError: ex =>
                        Logger.Log($"Failed to update room playlist item: {ex.Message}", LoggingTarget.Runtime, LogLevel.Important));
                }
                else
                {
                    // No editable current item (e.g. it was already played): append instead and let the server advance.
                    var added = new MultiplayerPlaylistItem
                    {
                        BeatmapID = roundMap.ID,
                        BeatmapChecksum = checksum,
                        RulesetID = rulesetInfo.OnlineID,
                        Freestyle = roundMap.Freestyle,
                        RequiredMods = requiredMods,
                        AllowedMods = allowedMods,
                    };

                    client.AddPlaylistItem(added).FireAndForget(onError: ex =>
                        Logger.Log($"Failed to add room playlist item: {ex.Message}", LoggingTarget.Runtime, LogLevel.Important));
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to update room playlist item: {ex.Message}", LoggingTarget.Runtime, LogLevel.Important);
            }
        }

        private void reset()
        {
            CurrentMatch.Value?.PicksBans.Clear();
            setNextMode();
        }

        private void addForBeatmap(int beatmapId)
        {
            if (CurrentMatch.Value?.Round.Value == null)
                return;

            if (CurrentMatch.Value.Round.Value.Beatmaps.All(b => b.Beatmap?.OnlineID != beatmapId))
                // don't attempt to add if the beatmap isn't in our pool
                return;

            if (CurrentMatch.Value.PicksBans.Any(p => p.BeatmapID == beatmapId))
                // don't attempt to add if already exists.
                return;

            CurrentMatch.Value.PicksBans.Add(new BeatmapChoice
            {
                Team = pickColour,
                Type = pickType,
                BeatmapID = beatmapId
            });

            setNextMode();

            if (LadderInfo.AutoProgressScreens.Value)
            {
                if (pickType == ChoiceType.Pick && CurrentMatch.Value.PicksBans.Any(i => i.Type == ChoiceType.Pick))
                {
                    scheduledScreenChange?.Cancel();
                    scheduledScreenChange = Scheduler.AddDelayed(() => { sceneManager?.SetScreen(typeof(GameplayScreen)); }, 10000);
                }
            }
        }

        public override void Hide()
        {
            scheduledScreenChange?.Cancel();
            base.Hide();
        }

        protected override void CurrentMatchChanged(ValueChangedEvent<TournamentMatch?> match)
        {
            base.CurrentMatchChanged(match);
            updateDisplay();
        }

        private void updateDisplay()
        {
            mapFlows.Clear();

            if (CurrentMatch.Value == null)
                return;

            int totalRows = 0;

            if (CurrentMatch.Value.Round.Value != null)
            {
                FillFlowContainer<TournamentBeatmapPanel>? currentFlow = null;
                string? currentMods = null;
                int flowCount = 0;

                foreach (var b in CurrentMatch.Value.Round.Value.Beatmaps)
                {
                    if (currentFlow == null || (LadderInfo.SplitMapPoolByMods.Value && currentMods != b.Mods))
                    {
                        mapFlows.Add(currentFlow = new FillFlowContainer<TournamentBeatmapPanel>
                        {
                            Spacing = new Vector2(10, 5),
                            Direction = FillDirection.Full,
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y
                        });

                        currentMods = b.Mods;

                        totalRows++;
                        flowCount = 0;
                    }

                    if (++flowCount > 2)
                    {
                        totalRows++;
                        flowCount = 1;
                    }

                    currentFlow.Add(new TournamentBeatmapPanel(b.Beatmap, b.Mods, b)
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Height = 42,
                        // TODO: picks/bans are not implemented yet. When implementing referee picks in a later step,
                        // apply the beatmap's gameplay mods (RoundBeatmap.Freestyle / SafeRequiredMods / SafeAllowedMods)
                        // to the multiplayer room here, and consider showing required/allowed mod icons plus
                        // required-mod-adjusted star rating on the panel.
                    });
                }
            }

            mapFlows.Padding = new MarginPadding(5)
            {
                // remove horizontal padding to increase flow width to 3 panels
                Horizontal = totalRows > 9 ? 0 : 100
            };
        }
    }
}
