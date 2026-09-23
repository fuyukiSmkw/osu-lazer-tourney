// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ExceptionExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.LazerTourney.Tournament.Online;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Setup;
using osu.Game.Screens.OnlinePlay;
using osu.Game.Screens.OnlinePlay.Lounge;
using osu.Game.Screens.OnlinePlay.Lounge.Components;
using osu.Game.Screens.OnlinePlay.Match.Components;
using osu.Game.Screens.OnlinePlay.Multiplayer;
using osu.Game.Screens.OnlinePlay.Multiplayer.Match;
using osu.Game.Screens.OnlinePlay.Multiplayer.Match.Playlist;
using osu.Game.Screens.OnlinePlay.Multiplayer.Participants;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Room
{
    /// <summary>
    /// Tournament room management screen. Functionally replicates
    /// <see cref="osu.Game.Screens.OnlinePlay.Multiplayer.MultiplayerLoungeSubScreen"/> while not joined
    /// and <see cref="MultiplayerMatchSubScreen"/> once joined, but as a
    /// <see cref="TournamentScreen"/> (<see cref="CompositeDrawable"/>, no <c>OsuScreen</c> navigation).
    /// </summary>
    /// <remarks>
    /// While not in a room, this screen shows the multiplayer lounge (filterable room list with
    /// create/join) and needs no back button. After joining, it shows the full official room
    /// controls (participants with kick/transfer, playlist with add/edit/remove, settings,
    /// timed and immediate start, chat) while keeping the local user spectating.
    /// The joined page is fixed: participants, queue and chat sit side by side in thirds and
    /// scroll internally. A back button in the footer returns to setup without leaving the room.
    /// The room stays joined across scene switches.
    /// </remarks>
    [Cached(typeof(IOnlinePlayLounge))]
    public partial class RoomScreen : TournamentScreen, IOnlinePlayLounge
    {
        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        [Resolved]
        private TournamentOnlineState onlineState { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private OngoingOperationTracker? ongoingOperationTracker { get; set; }

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private IdleTracker? idleTracker { get; set; }

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> workingBeatmap { get; set; } = null!;

        [Resolved]
        private TournamentSceneManager sceneManager { get; set; } = null!;

        [Cached(typeof(OnlinePlayBeatmapAvailabilityTracker))]
        private readonly OnlinePlayBeatmapAvailabilityTracker beatmapAvailabilityTracker = new MultiplayerBeatmapAvailabilityTracker();

        private readonly Bindable<LoungeFilterCriteria?> filter = new Bindable<LoungeFilterCriteria?>();
        private readonly Bindable<bool> hasListingResults = new Bindable<bool>();
        private readonly IBindable<bool> operationInProgress = new Bindable<bool>();
        private readonly IBindable<bool> isIdle = new BindableBool();

        private Container loungeContainer = null!;
        private Container joinedContainer = null!;
        private RoomListing roomListing = null!;
        private LoungeListingPoller listingPoller = null!;
        private PopoverContainer popoverContainer = null!;
        private LoadingLayer loadingLayer = null!;
        private BasicSearchTextBox searchTextBox = null!;
        private RoomCreateOverlay createOverlay = null!;

        private FormEnumDropdown<RoomModeFilter> statusDropdown = null!;
        private FormEnumDropdown<RoomPermissionsFilter> roomAccessTypeDropdown = null!;
        private FormCheckBox showInProgress = null!;
        private FormCheckBox showFull = null!;

        private OsuSpriteText spectateStatus = null!;
        private OsuSpriteText teamInfoText = null!;
        private Container roomPanelContainer = null!;
        private Container chatContainer = null!;
        private ParticipantsList participantsList = null!;
        private MultiplayerPlaylist multiplayerPlaylist = null!;
        private MatchChatDisplay? roomChat;
        private MultiplayerRoomPanel? roomPanel;
        private TourneyButton rejoinButton = null!;
        private TourneyButton editRoomButton = null!;

        private IDisposable? joiningRoomOperation;
        private ScheduledDelegate? scheduledFilterUpdate;
        private bool screenVisible;
        private bool inRoom;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                beatmapAvailabilityTracker,
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourProvider.Background5,
                },
                loungeContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        listingPoller = new LoungeListingPoller
                        {
                            RoomsReceived = onListingReceived,
                            Filter = { BindTarget = filter }
                        },
                        new FillFlowContainer
                        {
                            Name = "Header area",
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding(10),
                            Spacing = new Vector2(8),
                            Children = new Drawable[]
                            {
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    ColumnDimensions = new[]
                                    {
                                        new Dimension(GridSizeMode.AutoSize),
                                        new Dimension(),
                                        new Dimension(GridSizeMode.Relative, 0.45f),
                                    },
                                    RowDimensions = new[]
                                    {
                                        new Dimension(GridSizeMode.AutoSize),
                                    },
                                    Content = new[]
                                    {
                                        new Drawable[]
                                        {
                                            new FillFlowContainer
                                            {
                                                AutoSizeAxes = Axes.Both,
                                                Direction = FillDirection.Horizontal,
                                                Spacing = new Vector2(10),
                                                Children = new Drawable[]
                                                {
                                                    new TourneyButton
                                                    {
                                                        // TourneyButton (SettingsButton) defaults to relative-X sizing,
                                                        // which is illegal in a horizontal autosizing flow. Use absolute size.
                                                        RelativeSizeAxes = Axes.None,
                                                        Size = new Vector2(180, 48),
                                                        Text = "Create room",
                                                        Action = showCreateOverlay,
                                                    },
                                                    rejoinButton = new TourneyButton
                                                    {
                                                        RelativeSizeAxes = Axes.None,
                                                        Size = new Vector2(180, 48),
                                                        Text = "Rejoin room",
                                                        Action = rejoinLastRoom,
                                                    },
                                                    new TourneyButton
                                                    {
                                                        RelativeSizeAxes = Axes.None,
                                                        Size = new Vector2(140, 48),
                                                        Text = "Refresh",
                                                        Action = RefreshRooms,
                                                    },
                                                }
                                            },
                                            null,
                                            searchTextBox = new BasicSearchTextBox
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Height = 48,
                                            },
                                        },
                                    }
                                },
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Full,
                                    Spacing = new Vector2(10, 8),
                                    Children = new Drawable[]
                                    {
                                        new Container
                                        {
                                            Width = 160,
                                            AutoSizeAxes = Axes.Y,
                                            Child = statusDropdown = new FormEnumDropdown<RoomModeFilter>
                                            {
                                                Caption = LoungeSubScreenStrings.RoomFilterState,
                                            }
                                        },
                                        new Container
                                        {
                                            Width = 160,
                                            AutoSizeAxes = Axes.Y,
                                            Child = roomAccessTypeDropdown = new FormEnumDropdown<RoomPermissionsFilter>
                                            {
                                                Caption = LoungeSubScreenStrings.RoomFilterPrivacySetting,
                                                Current = config.GetBindable<RoomPermissionsFilter>(OsuSetting.MultiplayerRoomFilter),
                                            }
                                        },
                                        new Container
                                        {
                                            Width = 240,
                                            Child = showInProgress = new FormCheckBox
                                            {
                                                Caption = LoungeSubScreenStrings.RoomFilterInProgress,
                                                HintText = LoungeSubScreenStrings.RoomFilterInProgressDescription,
                                                Current = config.GetBindable<bool>(OsuSetting.MultiplayerShowInProgressFilter),
                                            }
                                        },
                                        new Container
                                        {
                                            Width = 220,
                                            AutoSizeAxes = Axes.Y,
                                            Child = showFull = new FormCheckBox
                                            {
                                                Caption = LoungeSubScreenStrings.RoomFilterFullRooms,
                                                HintText = LoungeSubScreenStrings.RoomFilterFullRoomsDescription,
                                                Current = config.GetBindable<bool>(OsuSetting.MultiplayerShowFullFilter),
                                            }
                                        },
                                    }
                                },
                            }
                        },
                        popoverContainer = new PopoverContainer
                        {
                            Name = "Rooms area",
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Horizontal = 10, Top = 170 },
                            Child = roomListing = new RoomListing
                            {
                                RelativeSizeAxes = Axes.Both,
                                Filter = { BindTarget = filter },
                            }
                        },
                        loadingLayer = new LoadingLayer(true),
                    }
                },
                joinedContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    // Fixed page mirroring MultiplayerMatchSubScreen: room panel on top,
                    // participants | queue | chat side by side below, footer at the bottom.
                    // The page itself never scrolls; only the three columns scroll internally.
                    // Hosted in a PopoverContainer so popover buttons in the footer
                    // (e.g. the countdown button) have a host.
                    Child = new PopoverContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding(10),
                            RowDimensions = new[]
                            {
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(),
                                new Dimension(GridSizeMode.Absolute, 10),
                                new Dimension(GridSizeMode.AutoSize),
                            },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    roomPanelContainer = new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                    },
                                },
                                new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(2),
                                        Children = new Drawable[]
                                        {
                                            spectateStatus = new OsuSpriteText
                                            {
                                                Font = OsuFont.GetFont(size: 16),
                                            },
                                            teamInfoText = new OsuSpriteText
                                            {
                                                Font = OsuFont.GetFont(size: 14),
                                            },
                                        },
                                    },
                                },
                                new Drawable[]
                                {
                                    new GridContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        ColumnDimensions = new[]
                                        {
                                            new Dimension(),
                                            new Dimension(GridSizeMode.Absolute, 10),
                                            new Dimension(),
                                            new Dimension(GridSizeMode.Absolute, 10),
                                            new Dimension(),
                                        },
                                        Content = new[]
                                        {
                                            new Drawable[]
                                            {
                                                new GridContainer
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    RowDimensions = new[]
                                                    {
                                                        new Dimension(GridSizeMode.AutoSize),
                                                        new Dimension(),
                                                    },
                                                    Content = new[]
                                                    {
                                                        new Drawable[] { new ParticipantsListHeader() },
                                                        new Drawable[]
                                                        {
                                                            participantsList = new ParticipantsList
                                                            {
                                                                RelativeSizeAxes = Axes.Both,
                                                            },
                                                        },
                                                    },
                                                },
                                                null,
                                                new GridContainer
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    RowDimensions = new[]
                                                    {
                                                        new Dimension(GridSizeMode.AutoSize),
                                                        new Dimension(),
                                                        new Dimension(GridSizeMode.AutoSize),
                                                        new Dimension(GridSizeMode.AutoSize),
                                                    },
                                                    Content = new[]
                                                    {
                                                        new Drawable[]
                                                        {
                                                            new OsuSpriteText
                                                            {
                                                                Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 18),
                                                                Text = "QUEUE",
                                                            },
                                                        },
                                                        new Drawable[]
                                                        {
                                                            multiplayerPlaylist = new MultiplayerPlaylist
                                                            {
                                                                RelativeSizeAxes = Axes.Both,
                                                                RequestEdit = onPlaylistEditRequested,
                                                            },
                                                        },
                                                        new Drawable[]
                                                        {
                                                            new TourneyButton
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                Margin = new MarginPadding { Top = 8 },
                                                                Text = "Add current beatmap",
                                                                Action = addCurrentBeatmapToRoom,
                                                            },
                                                        },
                                                        new Drawable[]
                                                        {
                                                            editRoomButton = new TourneyButton
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                Margin = new MarginPadding { Top = 8 },
                                                                Text = "Edit room",
                                                                Action = editRoomSettings,
                                                            },
                                                        },
                                                    },
                                                },
                                                null,
                                                new GridContainer
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    RowDimensions = new[]
                                                    {
                                                        new Dimension(GridSizeMode.AutoSize),
                                                        new Dimension(),
                                                    },
                                                    Content = new[]
                                                    {
                                                        new Drawable[]
                                                        {
                                                            new OsuSpriteText
                                                            {
                                                                Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 18),
                                                                Text = "CHAT",
                                                            },
                                                        },
                                                        new Drawable[]
                                                        {
                                                            chatContainer = new Container
                                                            {
                                                                RelativeSizeAxes = Axes.Both,
                                                            },
                                                        },
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },
                                null,
                                new Drawable[]
                                {
                                    new GridContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 50,
                                        ColumnDimensions = new[]
                                        {
                                            new Dimension(GridSizeMode.Absolute, 140),
                                            new Dimension(GridSizeMode.Absolute, 10),
                                            new Dimension(GridSizeMode.Absolute, 200),
                                            new Dimension(GridSizeMode.Absolute, 10),
                                            new Dimension(),
                                            new Dimension(GridSizeMode.Absolute, 10),
                                            new Dimension(GridSizeMode.Absolute, 200),
                                        },
                                        Content = new[]
                                        {
                                            new Drawable[]
                                            {
                                                new TourneyButton
                                                {
                                                    RelativeSizeAxes = Axes.None,
                                                    Size = new Vector2(140, 50),
                                                    Text = "Back",
                                                    Action = goBackToSetup,
                                                },
                                                null,
                                                new MultiplayerSpectateButton
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                },
                                                null,
                                                new MatchStartControl
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                },
                                                null,
                                                new TourneyButton
                                                {
                                                    RelativeSizeAxes = Axes.None,
                                                    Size = new Vector2(200, 50),
                                                    Text = "Leave room",
                                                    Action = leaveRoom,
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        }
                    },
                },
                createOverlay = new RoomCreateOverlay(new osu.Game.Online.Rooms.Room { Name = string.Empty })
                {
                    SettingsApplied = () => createOverlay.HideOverlay(),
                },
            };

            if (idleTracker != null)
                isIdle.BindTo(idleTracker.IsIdle);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            searchTextBox.Current.BindValueChanged(_ => updateFilterDebounced());
            ruleset.BindValueChanged(_ => UpdateFilter());
            isIdle.BindValueChanged(_ => updatePollingRate(), true);

            roomAccessTypeDropdown.Current.BindValueChanged(_ => UpdateFilter());
            showInProgress.Current.BindValueChanged(_ => UpdateFilter());
            showFull.Current.BindValueChanged(_ => UpdateFilter());
            statusDropdown.Current.BindValueChanged(status =>
            {
                showFull.Alpha = showInProgress.Alpha = status.NewValue == RoomModeFilter.Open ? 1 : 0;
                UpdateFilter();
            }, true);

            if (ongoingOperationTracker != null)
            {
                operationInProgress.BindTo(ongoingOperationTracker.InProgress);
                operationInProgress.BindValueChanged(_ => updateLoadingLayer());
            }

            hasListingResults.BindValueChanged(_ => updateLoadingLayer());

            filter.BindValueChanged(_ =>
            {
                roomListing.Rooms.Clear();
                RefreshRooms();
            });

            onlineState.RoomJoined.BindValueChanged(_ => Schedule(switchView), true);
            client.RoomUpdated += onClientRoomUpdated;

            updateLoadingLayer();
            UpdateFilter();
            switchView();
        }

        public override void Show()
        {
            base.Show();
            screenVisible = true;
            updatePollingRate();

            if (IsLoaded && !inRoom)
                listingPoller?.PollImmediately();
        }

        public override void Hide()
        {
            screenVisible = false;

            if (IsLoaded)
            {
                updatePollingRate();
                popoverContainer?.HidePopover();
                createOverlay?.HideOverlay();
            }

            base.Hide();
        }

        #region Lounge listing

        public void UpdateFilter() => Scheduler.AddOnce(updateFilter);

        private void updateFilterDebounced()
        {
            scheduledFilterUpdate?.Cancel();
            scheduledFilterUpdate = Scheduler.AddDelayed(UpdateFilter, 200);
        }

        private void updateFilter()
        {
            scheduledFilterUpdate?.Cancel();
            filter.Value = new LoungeFilterCriteria
            {
                SearchString = searchTextBox.Current.Value,
                Ruleset = ruleset.Value,
                Mode = statusDropdown.Current.Value,
                Category = "realtime",
                Permissions = roomAccessTypeDropdown.Current.Value,
                Full = showFull.Current.Value,
                Status = showInProgress.Current.Value && statusDropdown.Current.Value == RoomModeFilter.Open ? null : RoomStatusFilter.Idle,
            };
        }

        private void onListingReceived(osu.Game.Online.Rooms.Room[] result)
        {
            var localRoomsById = roomListing.Rooms.ToDictionary(r => r.RoomID!.Value);
            var resultRoomsById = result.ToDictionary(r => r.RoomID!.Value);

            roomListing.Rooms.RemoveAll(r => !resultRoomsById.ContainsKey(r.RoomID!.Value));

            foreach (var r in result)
            {
                if (localRoomsById.TryGetValue(r.RoomID!.Value, out osu.Game.Online.Rooms.Room? existingRoom))
                    existingRoom.CopyFrom(r);
                else
                    roomListing.Rooms.Add(r);
            }

            hasListingResults.Value = true;
        }

        public void RefreshRooms()
        {
            hasListingResults.Value = false;
            listingPoller?.PollImmediately();
        }

        private void updateLoadingLayer()
        {
            if (loadingLayer == null)
                return;

            if (operationInProgress.Value || !hasListingResults.Value)
                loadingLayer.Show();
            else
                loadingLayer.Hide();
        }

        private void updatePollingRate()
        {
            if (listingPoller == null)
                return;

            if (!screenVisible || inRoom)
                listingPoller.TimeBetweenPolls.Value = 0;
            else
                listingPoller.TimeBetweenPolls.Value = isIdle.Value ? 120000 : 15000;
        }

        #endregion

        #region Join / create / leave

        /// <summary>
        /// Attempts to join the given room and keeps the local user spectating.
        /// </summary>
        public void Join(osu.Game.Online.Rooms.Room room, string? password, Action<osu.Game.Online.Rooms.Room>? onSuccess = null, Action<string, Exception?>? onFailure = null) => Schedule(() =>
        {
            if (joiningRoomOperation != null)
                return;

            if (client.Room != null)
                return;

            if (!client.IsConnected.Value)
            {
                const string message = "Not currently connected to the multiplayer server.";
                Logger.Log(message, LoggingTarget.Runtime, LogLevel.Important);
                onFailure?.Invoke(message, null);
                return;
            }

            try
            {
                joiningRoomOperation = ongoingOperationTracker?.BeginOperation();
            }
            catch (InvalidOperationException)
            {
                return;
            }

            joinRoomInternal(room, password, r =>
            {
                joiningRoomOperation?.Dispose();
                joiningRoomOperation = null;
                onSuccess?.Invoke(r);
            }, (message, exception) =>
            {
                joiningRoomOperation?.Dispose();
                joiningRoomOperation = null;

                if (onFailure != null)
                    onFailure(message, exception);
                else
                    Logger.Error(exception, message);
            });
        });

        private void joinRoomInternal(osu.Game.Online.Rooms.Room room, string? password, Action<osu.Game.Online.Rooms.Room> onSuccess, Action<string, Exception?> onFailure)
        {
            // Never join with the listing-owned instance: MultiplayerClient keeps the passed room
            // as its APIRoom and live-syncs it, so sharing it with the lounge listing would let later
            // listing polls (Room.CopyFrom overwrites Playlist) corrupt live data and crash
            // updateLocalRoomSettings ("Sequence contains no matching element") on the next play start.
            // Join with a copy instead; everything else is synced from live data on join.
            var joinTarget = new osu.Game.Online.Rooms.Room
            {
                RoomID = room.RoomID,
                Password = password ?? room.Password,
                Name = room.Name,
                Type = room.Type,
            };

            onlineState.SetPendingRoom(joinTarget);

            client.JoinRoom(joinTarget, password).ContinueWith(result => Schedule(() =>
            {
                if (result.IsCompletedSuccessfully)
                {
                    onlineState.EnsureSpectateAsync().FireAndForget();
                    onSuccess(room);
                }
                else
                {
                    Exception? exception = result.Exception?.AsSingular();

                    if (exception?.GetHubExceptionMessage() is string message)
                        onFailure(message, exception);
                    else
                        onFailure($"Failed to join multiplayer room. {exception?.Message}", exception);
                }
            }));
        }

        public void OpenCopy(osu.Game.Online.Rooms.Room room)
        {
            Debug.Assert(room.RoomID != null);

            if (joiningRoomOperation != null)
                return;

            try
            {
                joiningRoomOperation = ongoingOperationTracker?.BeginOperation();
            }
            catch (InvalidOperationException)
            {
                return;
            }

            var req = new GetRoomRequest(room.RoomID.Value);

            req.Success += r => Schedule(() =>
            {
                // ID must be unset as it marks whether this is a client-side (not-yet-created) room or not.
                r.RoomID = null;

                // Null out dates because end date is not supported client-side.
                r.EndDate = null;
                r.Duration = null;

                createOverlay.ShowFor(r);

                joiningRoomOperation?.Dispose();
                joiningRoomOperation = null;
            });

            req.Failure += exception => Schedule(() =>
            {
                Logger.Error(exception, "Couldn't create a copy of this room.");
                joiningRoomOperation?.Dispose();
                joiningRoomOperation = null;
            });

            api.Queue(req);
        }

        public void Close(osu.Game.Online.Rooms.Room room)
            => throw new NotSupportedException("Cannot close multiplayer rooms.");

        private void showCreateOverlay() => createOverlay.ShowFor(new osu.Game.Online.Rooms.Room
        {
            Name = $"{api.LocalUser.Value.Username}'s awesome room",
            Type = MatchType.HeadToHead,
        });

        private void rejoinLastRoom()
        {
            if (client.Room != null)
                return;

            if (onlineState.LastRoomID == null)
                return;

            Join(new osu.Game.Online.Rooms.Room { RoomID = onlineState.LastRoomID }, onlineState.LastRoomPassword,
                null, (message, _) => Schedule(() => Logger.Log($"Failed to rejoin room: {message}", LoggingTarget.Runtime, LogLevel.Important)));
        }

        /// <summary>
        /// Returns to the setup screen. The joined room stays active in the background,
        /// unlike <see cref="leaveRoom"/> which leaves the room and shows the lounge.
        /// </summary>
        private void goBackToSetup() => sceneManager?.SetScreen(typeof(SetupScreen));

        private void leaveRoom()
        {
            if (client.Room == null)
                return;

            IDisposable? operation = null;

            try
            {
                operation = ongoingOperationTracker?.BeginOperation();
            }
            catch (InvalidOperationException e)
            {
                // Leaving must always work, even if a previous operation leaked its lease.
                // Proceed without busy indication rather than stranding the user in the room.
                Logging.LogError(e, "Leaving room without operation tracker lease; a previous operation may have leaked its lease.");
            }

            client.LeaveRoom().ContinueWith(_ => Schedule(() => operation?.Dispose()));
        }

        #endregion

        #region Joined room view (mirrors MultiplayerMatchSubScreen)

        private void switchView()
        {
            inRoom = onlineState.RoomJoined.Value;

            if (!IsLoaded)
                return;

            createOverlay.HideOverlay();

            if (inRoom)
            {
                loungeContainer.FadeOut(200);
                joinedContainer.FadeIn(200);
                rebuildJoinedRoom();
                refreshJoinedView();
            }
            else
            {
                joinedContainer.FadeOut(200);
                loungeContainer.FadeIn(200);
                clearJoinedRoom();
                RefreshRooms();
            }

            updateRejoinButton();
            updatePollingRate();
        }

        private void onClientRoomUpdated()
        {
            updateRejoinButton();

            // ParticipantsList, MultiplayerPlaylist, MatchStartControl and MatchChatDisplay
            // subscribe to RoomUpdated themselves (see MultiplayerMatchSubScreen.LoadComplete).
            // Only the tournament supplements need a manual refresh here.
            Scheduler.AddOnce(refreshJoinedView);
        }

        private void updateRejoinButton()
        {
            if (rejoinButton == null || !IsLoaded)
                return;

            rejoinButton.Alpha = !inRoom && onlineState.LastRoomID != null ? 1 : 0;
        }

        private void rebuildJoinedRoom()
        {
            var apiRoom = onlineState.ApiRoom;

            if (apiRoom == null)
                return;

            roomPanelContainer.Clear();
            roomPanel = new MultiplayerRoomPanel(apiRoom)
            {
                RelativeSizeAxes = Axes.X,
                OnEdit = editRoomSettings,
                ShowDescription = true,
            };
            roomPanelContainer.Add(roomPanel);

            chatContainer.Clear();
            roomChat?.Dispose();
            roomChat = new MatchChatDisplay(apiRoom, leaveChannelOnDispose: false)
            {
                RelativeSizeAxes = Axes.Both
            };
            chatContainer.Add(roomChat);
        }

        private void clearJoinedRoom()
        {
            roomPanelContainer?.Clear();
            roomPanel = null;

            chatContainer?.Clear();
            roomChat?.Dispose();
            roomChat = null;
        }

        private void refreshJoinedView()
        {
            var room = client.Room;

            if (room == null || !IsLoaded || !inRoom)
                return;

            var localState = client.LocalUser?.State;
            spectateStatus.Text = localState == MultiplayerUserState.Spectating
                ? "Broadcasting as spectator."
                : $"Local state: {localState?.ToString() ?? "unknown"} (switching to spectate...)";

            // Room settings changes require host or referee rights (mirrors the official
            // room panel, which hides its change-settings button otherwise).
            editRoomButton?.Enabled.Value = onlineState.IsHost.Value || onlineState.IsReferee.Value;

            // Tournament supplement: official ParticipantPanel shows slot/mods/state,
            // while team names come from the TeamVersus match state.
            if (room.MatchState is TeamVersusRoomState teamVersus)
            {
                var groups = room.Users
                    .Where(u => u.MatchState is TeamVersusUserState)
                    .GroupBy(u => ((TeamVersusUserState)u.MatchState).TeamID)
                    .OrderBy(g => g.Key)
                    .Select(g =>
                    {
                        string teamName = teamVersus.Teams?.FirstOrDefault(t => t.ID == g.Key)?.Name ?? $"Team {g.Key}";
                        return $"{teamName}: {string.Join(", ", g.Select(u => u.User?.Username ?? $"#{u.UserID}"))}";
                    });

                string teamText = string.Join(" | ", groups);
                teamInfoText.Text = teamText;
                teamInfoText.Alpha = string.IsNullOrEmpty(teamText) ? 0 : 1;
            }
            else
            {
                teamInfoText.Text = string.Empty;
                teamInfoText.Alpha = 0;
            }
        }

        private void editRoomSettings()
        {
            var apiRoom = onlineState.ApiRoom;

            if (apiRoom == null)
                return;

            if (!onlineState.IsHost.Value && !onlineState.IsReferee.Value)
                return;

            createOverlay.ShowFor(apiRoom);
        }

        private void onPlaylistEditRequested(PlaylistItem item)
        {
            // Playlist item edits (mods/beatmap replacement) are driven from the map pool screen,
            // which carries the tournament round context. See MapPoolScreen.applyToRoom.
            Logger.Log($"Playlist edit requested for item {item.ID}. Use the map pool screen to replace the current item.", LoggingTarget.Runtime, LogLevel.Important);
        }

        private void addCurrentBeatmapToRoom()
        {
            var room = client.Room;

            if (room == null)
                return;

            var current = workingBeatmap.Value?.BeatmapInfo;

            if (current == null || current.OnlineID <= 0)
            {
                Logger.Log("The current beatmap is not available online.", LoggingTarget.Runtime, LogLevel.Important);
                return;
            }

            // Matches MultiplayerMatchSongSelect.selectItem for the add path.
            var multiplayerItem = new MultiplayerPlaylistItem
            {
                BeatmapID = current.OnlineID,
                BeatmapChecksum = current.MD5Hash,
                RulesetID = ruleset.Value.OnlineID,
            };

            client.AddPlaylistItem(multiplayerItem).FireAndForget(onError: ex =>
                Logger.Log($"Failed to add playlist item: {ex.Message}", LoggingTarget.Runtime, LogLevel.Important));
        }

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            client?.RoomUpdated -= onClientRoomUpdated;
        }
    }
}
