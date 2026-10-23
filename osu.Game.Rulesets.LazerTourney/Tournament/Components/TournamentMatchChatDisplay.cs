// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using System.Linq;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Overlays.Chat;
using osu.Game.Rulesets.LazerTourney.Tournament.Components.ChatCommands;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Online;
using osu.Game.Screens.OnlinePlay.Match.Components;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// Tournament match chat overlay. Follows the joined room's channel using the shared
    /// <see cref="ChannelManager"/>, mirroring <see cref="MatchChatDisplay"/> (room chat),
    /// with tournament message styling.
    /// </summary>
    /// <remarks>
    /// The base send box is always disabled via <c>base(false)</c>. When <c>showTextBox</c>
    /// is set, our own <see cref="TournamentChatTextBox"/> is added instead, so custom
    /// commands and tab completion work here too.
    /// </remarks>
    public partial class TournamentMatchChatDisplay : StandAloneChatDisplay
    {
        [Resolved]
        private ChannelManager? channelManager { get; set; }

        [Resolved]
        private TournamentOnlineState onlineState { get; set; } = null!;

        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        [Resolved]
        private LadderInfo ladderInfo { get; set; } = null!;

        [Resolved]
        private RefereeAuthController refereeAuth { get; set; } = null!;

        [Resolved]
        private ChatTimerService timerService { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        private Room? currentRoom;
        private long? lastRoomId;
        private int lastChannelId;

        private static readonly FieldInfo? posting_text_box_field =
            typeof(StandAloneChatDisplay).GetField("postingTextBox", BindingFlags.Instance | BindingFlags.NonPublic);

        private ChatTextBox textBox = null!;
        private TournamentChatCommandHandler? commandHandler;

        private const float text_box_height = 30;
        private const float corner_radius = 10;

        private readonly bool showTextBox;

        /// <param name="showTextBox">Whether to show our own send box with custom commands and tab completion.</param>
        public TournamentMatchChatDisplay(bool showTextBox = false) : base(false)
        {
            this.showTextBox = showTextBox;

            RelativeSizeAxes = Axes.X;
            Height = 144;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;

            CornerRadius = 0;

            if (!showTextBox)
                return;

            // Depth below the message layer (added later by the base class):
            // higher depth draws behind, so this keeps the send box interactive.
            AddInternal(textBox = new TournamentChatTextBox
            {
                Depth = -1,
                RelativeSizeAxes = Axes.X,
                Height = text_box_height,
                PlaceholderText = "type message or command...",
                CornerRadius = corner_radius,
                ReleaseFocusOnCommit = false,
                HoldFocus = true,
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
            });

            textBox.OnCommit += postMessage;

            posting_text_box_field?.SetValue(this, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            onlineState.RoomJoined.ValueChanged += onRoomJoinedChanged;
            client.RoomUpdated += onClientRoomUpdated;

            if (!showTextBox)
            {
                updateRoom();
                return;
            }

            Channel.BindValueChanged(onChannelChanged, true);

            commandHandler = new TournamentChatCommandHandler(client, refereeAuth, timerService, () => Channel.Value, notify, api);

            updateRoom();
        }

        private void onRoomJoinedChanged(ValueChangedEvent<bool> e) => Scheduler.AddOnce(updateRoom);

        private void onClientRoomUpdated() => Scheduler.AddOnce(updateChannel);

        /// <summary>
        /// Tracks the current API room, resubscribing when the instance is replaced.
        /// </summary>
        private void updateRoom()
        {
            var room = onlineState.RoomJoined.Value ? onlineState.ApiRoom : null;

            if (room == currentRoom)
            {
                updateChannel();
                return;
            }

            if (currentRoom != null)
                currentRoom.PropertyChanged -= onRoomPropertyChanged;

            currentRoom = room;

            if (currentRoom != null)
                currentRoom.PropertyChanged += onRoomPropertyChanged;

            updateChannel();
        }

        private void onRoomPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Room.ChannelId))
                updateChannel();
        }

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
                Channel.Value = null;
                return;
            }

            Channel.Value = channelManager?.JoinChannel(new Channel { Id = channelId, Type = ChannelType.Multiplayer, Name = $"#lazermp_{roomId.Value}" });
        }

        public void Expand() => this.FadeIn(300);

        public void Contract() => this.FadeOut(200);

        /// <summary>
        /// Keeps the send box draft in sync with the other chat boxes through the channel.
        /// The base class only binds its own (disabled) text box, so this one is bound here.
        /// Mirrors <see cref="SyncedControlPanelBottomBar"/>.
        /// </summary>
        private void onChannelChanged(ValueChangedEvent<Channel?> e)
        {
            if (e.OldValue != null)
                textBox.Current.UnbindFrom(e.OldValue.TextBoxMessage);

            if (e.NewValue != null)
                textBox.Current.BindTo(e.NewValue.TextBoxMessage);
        }

        /// <summary>
        /// Sends on commit (Enter) and clears the box. Commands go through the shared
        /// <see cref="TournamentChatCommandHandler"/>; anything unknown falls through to
        /// <see cref="ChannelManager.PostCommand"/>, mirroring <see cref="SyncedControlPanelBottomBar"/>.
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
                channelManager?.PostCommand(text.Substring(1), Channel.Value);
            else
                channelManager?.PostMessage(text, target: Channel.Value);

            textBox.Text = string.Empty;
        }

        private void notify(string text, bool important = false)
            => ChatNotifier.Notify(notifications, Channel.Value, text, important);

        protected override ChatLine? CreateMessage(Message message)
        {
            // not stable anymore
            // if (message.Content.StartsWith("!mp", StringComparison.Ordinal)) return null;

            return new MatchMessage(message, ladderInfo);
        }

        protected override StandAloneDrawableChannel CreateDrawableChannel(Channel channel) => new MatchChannel(channel);

        public partial class MatchChannel : StandAloneDrawableChannel
        {
            public MatchChannel(Channel channel)
                : base(channel)
            {
                ScrollbarVisible = false;
            }
        }

        protected partial class MatchMessage : StandAloneMessage
        {
            public MatchMessage(Message message, LadderInfo info)
                : base(message)
            {
                if (info.CurrentMatch.Value is TournamentMatch match)
                {
                    if (match.Team1.Value?.Players.Any(u => u.OnlineID == Message.Sender.OnlineID) == true)
                        UsernameColour = TournamentColours.GetTeamNameColour(TeamColour.Red, info);
                    else if (match.Team2.Value?.Players.Any(u => u.OnlineID == Message.Sender.OnlineID) == true)
                        UsernameColour = TournamentColours.GetTeamNameColour(TeamColour.Blue, info);
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (showTextBox)
            {
                textBox.OnCommit -= postMessage;
                Channel.ValueChanged -= onChannelChanged;
            }

            commandHandler?.Dispose();
            commandHandler = null;

            if (onlineState != null)
                onlineState.RoomJoined.ValueChanged -= onRoomJoinedChanged;

            if (client != null)
                client.RoomUpdated -= onClientRoomUpdated;

            if (currentRoom != null)
                currentRoom.PropertyChanged -= onRoomPropertyChanged;
        }
    }
}
