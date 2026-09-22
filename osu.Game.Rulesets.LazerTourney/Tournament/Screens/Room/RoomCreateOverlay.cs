// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ExceptionExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Rulesets.LazerTourney.Tournament.Online;
using osu.Game.Screens.OnlinePlay;
using osu.Game.Screens.OnlinePlay.Match.Components;
using osu.Game.Screens.OnlinePlay.Multiplayer.Match;
using osuTK;
using Container = osu.Framework.Graphics.Containers.Container;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Room
{
    /// <summary>
    /// Form for creating a new lazer multiplayer room without leaving the tournament client.
    /// Adapted from <see cref="MultiplayerMatchSettingsOverlay"/>: after a successful create the
    /// overlay only hides itself (no screen push), and the local user is forced into spectate state.
    /// </summary>
    /// <remarks>
    /// When already joined, applying edits the live room via multiplayer client settings change,
    /// matching <see cref="MultiplayerMatchSettingsOverlay"/>. Otherwise it creates a new room.
    /// </remarks>
    public partial class RoomCreateOverlay : CompositeDrawable
    {
        private const float field_padding = 25;

        /// <summary>
        /// Invoked after the room was created successfully (overlay should hide itself).
        /// </summary>
        public Action? SettingsApplied;

        private osu.Game.Online.Rooms.Room room;

        public OsuTextBox NameField = null!;
        private FormSliderBar<byte> maximumParticipantsSliderBar = null!;
        private FormCheckBox maximumParticipantsCheckbox = null!;
        public MatchTypePicker TypePicker = null!;
        public OsuEnumDropdown<QueueMode> QueueModeDropdown = null!;
        public OsuTextBox PasswordTextBox = null!;
        public OsuCheckbox AutoSkipCheckbox = null!;
        private RoundedButton applyButton = null!;
        public OsuSpriteText ErrorText = null!;

        private OsuEnumDropdown<StartMode> startModeDropdown = null!;
        private OsuSpriteText typeLabel = null!;
        private LoadingLayer loadingLayer = null!;

        private DrawableRoomPlaylist drawablePlaylist = null!;
        private RoundedButton addBeatmapButton = null!;

        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        [Resolved]
        private TournamentOnlineState onlineState { get; set; } = null!;

        [Resolved]
        private OngoingOperationTracker ongoingOperationTracker { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private Bindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        private readonly IBindable<bool> operationInProgress = new BindableBool();

        private IDisposable? applyingSettingsOperation;

        public RoomCreateOverlay(osu.Game.Online.Rooms.Room room)
        {
            this.room = room;

            RelativeSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = 10;
            Alpha = 0;
        }

        /// <summary>
        /// Shows the overlay for the given not-yet-created room.
        /// </summary>
        public void ShowFor(osu.Game.Online.Rooms.Room newRoom)
        {
            if (IsLoaded)
            {
                room.PropertyChanged -= onRoomPropertyChanged;
                room = newRoom;
                room.PropertyChanged += onRoomPropertyChanged;
                refreshAllFields();
            }
            else
                room = newRoom;

            ErrorText?.FadeOut(50);
            Show();
        }

        public void HideOverlay() => Hide();

        // This overlay covers the whole screen while shown: swallow unhandled input
        // so clicks, drags and scrolls cannot reach the room list underneath.
        // Children (form fields, buttons, inner scroll container) still receive input first.
        protected override bool OnClick(ClickEvent e) => true;

        protected override bool OnDragStart(DragStartEvent e) => true;

        protected override bool OnScroll(ScrollEvent e) => true;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuColour colours)
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background4
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    RowDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new OsuScrollContainer
                            {
                                Padding = new MarginPadding { Horizontal = 30, Vertical = 10 },
                                RelativeSizeAxes = Axes.Both,
                                Children = new[]
                                {
                                    new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 10),
                                        Children = new Drawable[]
                                        {
                                            new Container
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Children = new Drawable[]
                                                {
                                                    new FillFlowContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Width = 0.5f,
                                                        Direction = FillDirection.Vertical,
                                                        Spacing = new Vector2(field_padding),
                                                        Padding = new MarginPadding { Right = field_padding / 2 },
                                                        Children = new Drawable[]
                                                        {
                                                            createSection("Room name", NameField = new OsuTextBox
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                TabbableContentContainer = this,
                                                                LengthLimit = 100,
                                                            }),
                                                            createSection("Game type", new FillFlowContainer
                                                            {
                                                                AutoSizeAxes = Axes.Y,
                                                                RelativeSizeAxes = Axes.X,
                                                                Direction = FillDirection.Vertical,
                                                                Spacing = new Vector2(7),
                                                                Children = new Drawable[]
                                                                {
                                                                    TypePicker = new MatchTypePicker
                                                                    {
                                                                        RelativeSizeAxes = Axes.X,
                                                                    },
                                                                    typeLabel = new OsuSpriteText
                                                                    {
                                                                        Font = OsuFont.GetFont(size: 14),
                                                                        Colour = colours.Yellow
                                                                    },
                                                                },
                                                            }),
                                                            createSection("Queue mode", new Container
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                Height = 40,
                                                                Child = QueueModeDropdown = new OsuEnumDropdown<QueueMode>
                                                                {
                                                                    RelativeSizeAxes = Axes.X
                                                                }
                                                            }),
                                                            createSection("Auto start", new Container
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                Height = 40,
                                                                Child = startModeDropdown = new OsuEnumDropdown<StartMode>
                                                                {
                                                                    RelativeSizeAxes = Axes.X
                                                                }
                                                            }),
                                                        },
                                                    },
                                                    new FillFlowContainer
                                                    {
                                                        Anchor = Anchor.TopRight,
                                                        Origin = Anchor.TopRight,
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Width = 0.5f,
                                                        Direction = FillDirection.Vertical,
                                                        Spacing = new Vector2(field_padding),
                                                        Padding = new MarginPadding { Left = field_padding / 2 },
                                                        Children = new Drawable[]
                                                        {
                                                            createSection("Player count", new FillFlowContainer
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                AutoSizeAxes = Axes.Y,
                                                                Direction = FillDirection.Vertical,
                                                                Children = new Drawable[]
                                                                {
                                                                    maximumParticipantsCheckbox = new FormCheckBox
                                                                    {
                                                                        Caption = "Limited slots",
                                                                        HintText = "When enabled, total players allowed in a room will be limited. Unlimited when disabled."
                                                                    },
                                                                    maximumParticipantsSliderBar = new FormSliderBar<byte>
                                                                    {
                                                                        Caption = "Slot count",
                                                                        RelativeSizeAxes = Axes.X,
                                                                        Margin = new MarginPadding { Top = 5 },
                                                                        Current = new BindableNumber<byte>(16)
                                                                        {
                                                                            MinValue = 2,
                                                                            MaxValue = 16,
                                                                        }
                                                                    },
                                                                },
                                                            }),
                                                            createSection("Password (optional)", PasswordTextBox = new OsuPasswordTextBox
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                TabbableContentContainer = this,
                                                                LengthLimit = 40,
                                                            }),
                                                            createSection("Other", AutoSkipCheckbox = new OsuCheckbox
                                                            {
                                                                LabelText = "Automatically skip the beatmap intro"
                                                            }),
                                                        },
                                                    },
                                                }
                                            },
                                            new FillFlowContainer
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Width = 0.6f,
                                                Spacing = new Vector2(5),
                                                Direction = FillDirection.Vertical,
                                                Children = new Drawable[]
                                                {
                                                    new OsuSpriteText
                                                    {
                                                        Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 12),
                                                        Text = "PLAYLIST",
                                                    },
                                                    drawablePlaylist = new DrawableRoomPlaylist
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        Height = DrawableRoomPlaylistItem.HEIGHT * 3,
                                                        AllowDeletion = true,
                                                        AllowSelection = true,
                                                    },
                                                    new FillFlowContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Direction = FillDirection.Horizontal,
                                                        Spacing = new Vector2(5),
                                                        Children = new Drawable[]
                                                        {
                                                            addBeatmapButton = new RoundedButton
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                Width = 0.5f,
                                                                Height = 40,
                                                                Text = "Add current beatmap",
                                                                Action = addCurrentBeatmap,
                                                            },
                                                            new RoundedButton
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                Width = 0.5f,
                                                                Height = 40,
                                                                Text = "Remove selected",
                                                                Action = removeSelectedBeatmap,
                                                            },
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    },
                                }
                            }
                        },
                        new Drawable[]
                        {
                            new Container
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = colourProvider.Background5
                                    },
                                    new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 10),
                                        Margin = new MarginPadding { Vertical = 10 },
                                        Padding = new MarginPadding { Horizontal = 30 },
                                        Children = new Drawable[]
                                        {
                                            new FillFlowContainer
                                            {
                                                Anchor = Anchor.BottomCentre,
                                                Origin = Anchor.BottomCentre,
                                                AutoSizeAxes = Axes.Both,
                                                Direction = FillDirection.Horizontal,
                                                Spacing = new Vector2(10, 0),
                                                Children = new Drawable[]
                                                {
                                                    new RoundedButton
                                                    {
                                                        Size = new Vector2(140, 50),
                                                        Text = "Back",
                                                        Action = HideOverlay,
                                                    },
                                                    applyButton = new RoundedButton
                                                    {
                                                        Size = new Vector2(230, 50),
                                                        Enabled = { Value = false },
                                                        Text = "Create",
                                                        Action = apply,
                                                    },
                                                }
                                            },
                                            ErrorText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.BottomCentre,
                                                Origin = Anchor.BottomCentre,
                                                Alpha = 0,
                                                Colour = colours.RedDark
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                loadingLayer = new LoadingLayer(true)
            };

            TypePicker.Current.BindValueChanged(type => typeLabel.Text = type.NewValue.GetLocalisableDescription(), true);

            operationInProgress.BindTo(ongoingOperationTracker.InProgress);
            operationInProgress.BindValueChanged(v =>
            {
                if (v.NewValue)
                    loadingLayer.Show();
                else
                    loadingLayer.Hide();
            });

            drawablePlaylist.RequestDeletion = item =>
                room.Playlist = room.Playlist.Where(i => i != item).ToArray();
        }

        private static Drawable createSection(string title, Drawable child) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(5),
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 12),
                    Text = title.ToUpperInvariant(),
                },
                child,
            }
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            room.PropertyChanged += onRoomPropertyChanged;

            refreshAllFields();

            maximumParticipantsCheckbox.Current.BindValueChanged(enabled =>
            {
                maximumParticipantsSliderBar.Alpha = enabled.NewValue ? 1 : 0;
            }, true);
        }

        private void refreshAllFields()
        {
            updateRoomName();
            updateRoomType();
            updateRoomQueueMode();
            updateRoomPassword();
            updateRoomAutoSkip();
            updateRoomMaxParticipants();
            updateRoomAutoStartDuration();
            updateRoomPlaylist();
        }

        private void onRoomPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(osu.Game.Online.Rooms.Room.Name):
                    updateRoomName();
                    break;

                case nameof(osu.Game.Online.Rooms.Room.Type):
                    updateRoomType();
                    break;

                case nameof(osu.Game.Online.Rooms.Room.QueueMode):
                    updateRoomQueueMode();
                    break;

                case nameof(osu.Game.Online.Rooms.Room.Password):
                    updateRoomPassword();
                    break;

                case nameof(osu.Game.Online.Rooms.Room.AutoSkip):
                    updateRoomAutoSkip();
                    break;

                case nameof(osu.Game.Online.Rooms.Room.MaxParticipants):
                    updateRoomMaxParticipants();
                    break;

                case nameof(osu.Game.Online.Rooms.Room.AutoStartDuration):
                    updateRoomAutoStartDuration();
                    break;

                case nameof(osu.Game.Online.Rooms.Room.Playlist):
                    updateRoomPlaylist();
                    break;
            }
        }

        private void updateRoomName()
            => NameField.Text = room.Name;

        private void updateRoomType()
            => TypePicker.Current.Value = room.Type;

        private void updateRoomQueueMode()
            => QueueModeDropdown.Current.Value = room.QueueMode;

        private void updateRoomPassword()
            => PasswordTextBox.Text = room.Password ?? string.Empty;

        private void updateRoomAutoSkip()
            => AutoSkipCheckbox.Current.Value = room.AutoSkip;

        private void updateRoomMaxParticipants()
        {
            if (room.MaxParticipants.HasValue)
            {
                maximumParticipantsCheckbox.Current.Value = true;
                maximumParticipantsSliderBar.Current.Value = room.MaxParticipants.Value;
            }
            else
                maximumParticipantsCheckbox.Current.Value = false;
        }

        private void updateRoomAutoStartDuration()
            => startModeDropdown.Current.Value = (StartMode)room.AutoStartDuration.TotalSeconds;

        private void updateRoomPlaylist()
            => drawablePlaylist.Items.ReplaceRange(0, drawablePlaylist.Items.Count, room.Playlist);

        protected override void Update()
        {
            base.Update();

            if (applyButton != null)
            {
                applyButton.Enabled.Value = room.Playlist.Count > 0 && NameField.Text.Length > 0 && !operationInProgress.Value;
                applyButton.Text = room.RoomID == null ? "Create" : "Update";
            }
        }

        private void addCurrentBeatmap()
        {
            var current = beatmap?.Value?.BeatmapInfo;

            if (current == null || current.OnlineID <= 0)
            {
                ErrorText.Text = "The current beatmap is not available online.";
                ErrorText.FadeIn(50);
                return;
            }

            ErrorText.FadeOut(50);
            room.Playlist = room.Playlist.Append(new PlaylistItem(current)
            {
                RulesetID = ruleset.Value.OnlineID,
            }).ToArray();
        }

        private void removeSelectedBeatmap()
        {
            if (drawablePlaylist.SelectedItem.Value == null)
                return;

            room.Playlist = room.Playlist.Where(i => i != drawablePlaylist.SelectedItem.Value).ToArray();
        }

        private async void apply()
        {
            if (!applyButton.Enabled.Value)
                return;

            byte? maxParticipants = maximumParticipantsCheckbox.Current.Value ? maximumParticipantsSliderBar.Current.Value : null;

            ErrorText.FadeOut(50);

            // Guard against double-submission: the button only disables a frame later via Update().
            if (applyingSettingsOperation != null)
                return;

            applyingSettingsOperation = ongoingOperationTracker.BeginOperation();

            // If already joined, update the live room via the client, matching MultiplayerMatchSettingsOverlay.
            // Otherwise update the pending room directly in preparation for creation.
            if (client.Room != null)
            {
                try
                {
                    await client.ChangeSettings(
                        name: NameField.Text,
                        password: PasswordTextBox.Text,
                        matchType: TypePicker.Current.Value,
                        queueMode: QueueModeDropdown.Current.Value,
                        autoStartDuration: TimeSpan.FromSeconds((int)startModeDropdown.Current.Value),
                        autoSkip: AutoSkipCheckbox.Current.Value,
                        maxParticipants: maxParticipants).ConfigureAwait(false);

                    Schedule(onSuccess);
                }
                catch (Exception ex)
                {
                    onError(ex, "Error changing settings");
                }
                finally
                {
                    // Release synchronously: a scheduled dispose would leak the lease if the callback never runs.
                    applyingSettingsOperation.Dispose();
                    applyingSettingsOperation = null;
                }

                return;
            }

            bool created = false;

            try
            {
                room.Name = NameField.Text;
                room.Password = PasswordTextBox.Text;
                room.Type = TypePicker.Current.Value;
                room.QueueMode = QueueModeDropdown.Current.Value;
                room.AutoStartDuration = TimeSpan.FromSeconds((int)startModeDropdown.Current.Value);
                room.AutoSkip = AutoSkipCheckbox.Current.Value;
                room.Playlist = drawablePlaylist.Items.ToArray();
                room.MaxParticipants = maxParticipants;

                onlineState.SetPendingRoom(room);
                await client.CreateRoom(room).ConfigureAwait(false);

                created = true;
                Schedule(onSuccess);
            }
            catch (Exception ex)
            {
                onError(ex, "Error creating room");
            }
            finally
            {
                // Release synchronously: a scheduled dispose would leak the lease if the callback never runs.
                applyingSettingsOperation.Dispose();
                applyingSettingsOperation = null;
            }

            // Independent of the create operation: never await spectate enforcement while holding its lease,
            // otherwise a stalled ChangeState would wedge the tracker and block leaving the room.
            // (Join failures are impossible here: a throw above means we are not in a room,
            // in which case EnsureSpectateAsync returns immediately.)
            if (created)
                onlineState.EnsureSpectateAsync().FireAndForget();
        }

        private void onSuccess() => Schedule(() =>
        {
            SettingsApplied?.Invoke();
        });

        private void onError(Exception? exception, string description)
        {
            if (exception is AggregateException aggregateException)
                exception = aggregateException.AsSingular();

            string message = exception?.GetHubExceptionMessage() ?? $"{description} ({exception?.Message})";

            Schedule(() =>
            {
                // see https://github.com/ppy/osu-web/blob/2c97aaeb64fb4ed97c747d8383a35b30f57428c7/app/Models/Multiplayer/PlaylistItem.php#L48.
                const string not_found_prefix = "beatmaps not found:";

                if (message.StartsWith(not_found_prefix, StringComparison.Ordinal))
                    ErrorText.Text = "The selected beatmap is not available online.";
                else
                    ErrorText.Text = message;

                ErrorText.FadeIn(50);
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            room.PropertyChanged -= onRoomPropertyChanged;
        }

        private enum StartMode
        {
            [Description("Off")]
            Off = 0,

            [Description("10 seconds")]
            Seconds10 = 10,

            [Description("30 seconds")]
            Seconds30 = 30,

            [Description("1 minute")]
            Seconds60 = 60,

            [Description("3 minutes")]
            Seconds180 = 180,

            [Description("5 minutes")]
            Seconds300 = 300
        }
    }
}
