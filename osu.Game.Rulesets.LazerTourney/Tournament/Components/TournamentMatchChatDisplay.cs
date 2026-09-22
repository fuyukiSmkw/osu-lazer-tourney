// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Overlays.Chat;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Online;
using osu.Game.Screens.OnlinePlay.Match.Components;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// Tournament match chat overlay. Follows the joined room's channel using the shared
    /// <see cref="ChannelManager"/>, mirroring <see cref="MatchChatDisplay"/> (room chat),
    /// with tournament message styling. Never shows a send box.
    /// </summary>
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

        private Room? currentRoom;
        private long? lastRoomId;
        private int lastChannelId;

        public TournamentMatchChatDisplay()
        {
            RelativeSizeAxes = Axes.X;
            Height = 144;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;

            CornerRadius = 0;
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

        protected override ChatLine? CreateMessage(Message message)
        {
            if (message.Content.StartsWith("!mp", StringComparison.Ordinal))
                return null;

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
                        UsernameColour = TournamentColours.COLOUR_RED;
                    else if (match.Team2.Value?.Players.Any(u => u.OnlineID == Message.Sender.OnlineID) == true)
                        UsernameColour = TournamentColours.COLOUR_BLUE;
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (onlineState != null)
                onlineState.RoomJoined.ValueChanged -= onRoomJoinedChanged;

            if (client != null)
                client.RoomUpdated -= onClientRoomUpdated;

            if (currentRoom != null)
                currentRoom.PropertyChanged -= onRoomPropertyChanged;
        }
    }
}
