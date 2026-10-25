// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Online.API;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.LazerTourney.Tournament.Components.ChatCommands;
using osu.Game.Rulesets.LazerTourney.Tournament.Online;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// Chat send box pinned to the control-panel bottom bar.
    /// One instance is hosted in each stream screen's <see cref="SyncedControlPanel"/>;
    /// all instances share the same draft and stay in sync (see remarks).
    /// </summary>
    /// <remarks>
    /// Data flow mirrors the room chat (<see cref="osu.Game.Screens.OnlinePlay.Match.Components.MatchChatDisplay"/>,
    /// as hosted by <c>RoomScreen</c>) and <see cref="TournamentMatchChatDisplay"/>:
    /// the target channel is resolved from the joined API room via the shared <see cref="ChannelManager"/>
    /// (<c>JoinChannel</c> dedupes, so all instances share the same <see cref="Channel"/> object),
    /// sending goes through <c>ChannelManager.PostMessage / PostCommand</c>,
    /// and the unsent draft is synced by binding the text box to <see cref="Channel.TextBoxMessage"/>
    /// (the same mechanism <see cref="StandAloneChatDisplay"/> uses), so both screens
    /// (and the room screen's chat box) always show the same draft.
    /// </remarks>
    public partial class SyncedControlPanelBottomBar : CompositeDrawable
    {
        [Resolved]
        private ChannelManager? channelManager { get; set; }

        [Resolved]
        private TournamentOnlineState onlineState { get; set; } = null!;

        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        [Resolved]
        private ChatTimerService timerService { get; set; } = null!;

        [Resolved]
        private RefereeAuthController refereeAuth { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private readonly Bindable<Channel?> channel = new Bindable<Channel?>();

        private TournamentChatTextBox textBox = null!;
        private TournamentChatCommandHandler? commandHandler;
        private TournamentSpriteText countdownText = null!;
        private Framework.Graphics.Containers.Container historyContainer = null!;

        private Room? currentRoom;
        private long? lastRoomId;
        private int lastChannelId;

        private const float history_height = 110;

        private readonly bool showHistory;

        private BindableBool dynamicShowHistory = null!;

        /// <param name="showHistory">Whether to show a small read-only message list above the send box.</param>
        public SyncedControlPanelBottomBar(bool showHistory = false)
        {
            this.showHistory = showHistory;
        }

        public SyncedControlPanelBottomBar(BindableBool showHistory)
        {
            dynamicShowHistory = new()
            {
                BindTarget = showHistory,
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(30, 30, 30, 255),
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(5),
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 5f),
                    Children = new Drawable[]
                    {
                        new TitleContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Children = new Drawable[]
                            {
                                new TournamentSpriteText
                                {
                                    Text = "Chat",
                                },
                                countdownText = new TournamentSpriteText
                                {
                                    Anchor = Anchor.TopRight,
                                    Origin = Anchor.TopRight,
                                    Colour = new OsuColour().Yellow,
                                    Alpha = 0,
                                },
                            },
                        },
                        historyContainer = new Framework.Graphics.Containers.Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        },
                        textBox = new TournamentChatTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 30,
                            // PlaceholderText = ChatStrings.InputPlaceholder,
                            PlaceholderText = "type message...",
                            ReleaseFocusOnCommit = false,
                            // HoldFocus = true,
                        },
                    },
                },
            };

            if (dynamicShowHistory != null || showHistory)
            {
                // Small read-only message list between the title and the send box.
                // MapPool/Gameplay omit it because the full-width stream chat is visible there.
                historyContainer.Add(new TournamentMatchChatDisplay
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = history_height,
                });
            }

            textBox.OnCommit += postMessage;
            channel.BindValueChanged(onChannelChanged);
            timerService.RemainingSeconds.BindValueChanged(onRemainingSecondsChanged, true);

            commandHandler = new TournamentChatCommandHandler(client, refereeAuth, timerService, () => channel.Value, notify, api);

            dynamicShowHistory?.BindValueChanged(e =>
            {
                if (e.NewValue)
                    historyContainer.Show();
                else
                    historyContainer.Hide();
            }, true);
        }

        /// <summary>
        /// Shows the shared countdown readout next to the title while a countdown is running.
        /// </summary>
        private void onRemainingSecondsChanged(ValueChangedEvent<int> e)
        {
            if (e.NewValue < 0)
            {
                countdownText.FadeOut(200);
                return;
            }

            countdownText.Text = $"{e.NewValue / 60}:{e.NewValue % 60:D2}";
            countdownText.FadeIn(200);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            onlineState.RoomJoined.ValueChanged += onRoomJoinedChanged;
            client.RoomUpdated += onClientRoomUpdated;

            updateRoom();
        }

        private void onRoomJoinedChanged(ValueChangedEvent<bool> e) => Scheduler.AddOnce(updateRoom);

        private void onClientRoomUpdated() => Scheduler.AddOnce(updateChannel);

        /// <summary>
        /// Tracks the current API room, resubscribing when the instance is replaced.
        /// Mirrors <see cref="TournamentMatchChatDisplay.updateRoom"/>.
        /// </summary>
        private void updateRoom()
        {
            var room = onlineState.RoomJoined.Value ? onlineState.ApiRoom : null;

            if (room == currentRoom)
            {
                updateChannel();
                return;
            }

            currentRoom?.PropertyChanged -= onRoomPropertyChanged;

            currentRoom = room;

            currentRoom?.PropertyChanged += onRoomPropertyChanged;

            updateChannel();
        }

        private void onRoomPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Room.ChannelId))
                updateChannel();
        }

        /// <summary>
        /// Resolves the room's chat channel via the shared <see cref="ChannelManager"/>.
        /// Mirrors <see cref="TournamentMatchChatDisplay.updateChannel"/>.
        /// </summary>
        private void updateChannel()
        {
            long? roomId = currentRoom?.RoomID;
            int channelId = currentRoom?.ChannelId ?? 0;

            if (roomId == lastRoomId && channelId == lastChannelId)
                return;

            lastRoomId = roomId;
            lastChannelId = channelId;

            if (roomId == null || channelId == 0)
            {
                channel.Value = null;
                return;
            }

            channel.Value = channelManager?.JoinChannel(new Channel { Id = channelId, Type = ChannelType.Multiplayer, Name = $"#lazermp_{roomId.Value}" });
        }

        /// <summary>
        /// Binds the text box to the channel's draft, so unsent text stays in sync
        /// across both screens. Mirrors <see cref="StandAloneChatDisplay"/> channel switching.
        /// </summary>
        private void onChannelChanged(ValueChangedEvent<Channel?> e)
        {
            if (e.OldValue != null)
                textBox.Current.UnbindFrom(e.OldValue.TextBoxMessage);

            if (e.NewValue == null)
                return;

            textBox.Current.BindTo(e.NewValue.TextBoxMessage);
        }

        #region Commands

        /// <summary>
        /// Sends on commit (Enter) and clears the box. Mirrors <see cref="StandAloneChatDisplay"/> posting.
        /// Clearing propagates through the draft binding, so both screens clear together.
        /// Messages starting with <c>/</c> first go through <see cref="ChatCommands.TournamentChatCommandHandler"/>;
        /// anything unknown falls through to <see cref="ChannelManager.PostCommand"/>.
        /// </summary>
        private void postMessage(TextBox sender, bool newText)
        {
            string text = textBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(text))
                return;

            if (text[0] == '/' && commandHandler?.HandleCommand(text.Substring(1)) == true)
            {
                textBox.Text = string.Empty;
                return;
            }

            if (text[0] == '/')
                channelManager?.PostCommand(text.Substring(1), channel.Value);
            else
                channelManager?.PostMessage(text, target: channel.Value);

            textBox.Text = string.Empty;
        }

        private void notify(string text, bool important = false)
            => ChatNotifier.Notify(notifications, channel.Value, text, important);

        #endregion Commands

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            textBox?.OnCommit -= postMessage;

            commandHandler?.Dispose();
            commandHandler = null;

            channel.UnbindAll();

            dynamicShowHistory?.UnbindAll();

            onlineState?.RoomJoined.ValueChanged -= onRoomJoinedChanged;

            client?.RoomUpdated -= onClientRoomUpdated;

            currentRoom?.PropertyChanged -= onRoomPropertyChanged;
        }

        /// <summary>
        /// Title row showing the available referee chat commands on hover.
        /// </summary>
        private partial class TitleContainer : osu.Framework.Graphics.Containers.Container, IHasTooltip
        {
            public LocalisableString TooltipText => "/help for a list of commands\ntab for username completion";
        }
    }
}
