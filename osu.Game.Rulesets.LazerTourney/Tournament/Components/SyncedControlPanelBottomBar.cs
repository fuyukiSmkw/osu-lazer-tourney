// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.Countdown;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets.LazerTourney.Tournament.Online;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// Chat send box pinned to the control-panel bottom bar.
    /// One instance is hosted in each of <c>MapPoolScreen</c> and <c>GameplayScreen</c>.
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

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        private readonly Bindable<Channel?> channel = new Bindable<Channel?>();

        private StandAloneChatDisplay.ChatTextBox textBox = null!;
        private TournamentSpriteText countdownText = null!;

        private Room? currentRoom;
        private long? lastRoomId;
        private int lastChannelId;

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
                        textBox = new StandAloneChatDisplay.ChatTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 30,
                            PlaceholderText = ChatStrings.InputPlaceholder,
                            ReleaseFocusOnCommit = false,
                            // HoldFocus = true,
                        },
                    },
                },
            };

            textBox.OnCommit += postMessage;
            channel.BindValueChanged(onChannelChanged);
            timerService.RemainingSeconds.BindValueChanged(onRemainingSecondsChanged, true);
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

        /// <summary>
        /// Sends on commit (Enter) and clears the box. Mirrors <see cref="StandAloneChatDisplay"/> posting.
        /// Clearing propagates through the draft binding, so both screens clear together.
        /// Messages starting with <c>/</c> first go through the custom referee commands below;
        /// anything unknown falls through to <see cref="ChannelManager.PostCommand"/>.
        /// </summary>
        private void postMessage(TextBox sender, bool newText)
        {
            string text = textBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(text))
                return;

            if (text[0] == '/' && tryHandleCustomCommand(text.Substring(1)))
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

        /// <summary>
        /// Handles the custom referee commands. Returns false for unknown commands
        /// so the caller falls through to <see cref="ChannelManager.PostCommand"/>.
        /// </summary>
        private bool tryHandleCustomCommand(string commandText)
        {
            string[] parts = commandText.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 0)
                return false;

            string content = parts.Length == 2 ? parts[1] : string.Empty;

            switch (parts[0].ToLowerInvariant())
            {
                case "timer":
                    if (!int.TryParse(content, out int timerSeconds) || timerSeconds < 0)
                        notify("Usage: /timer <number>");
                    else
                        timerService.StartTimer(timerSeconds, channel.Value);
                    return true;

                case "start":
                    if (string.IsNullOrEmpty(content))
                    {
                        startMatch();
                        return true;
                    }

                    if (!int.TryParse(content, out int startSeconds) || startSeconds < 0)
                        notify("Usage: /start [seconds]");
                    else if (startSeconds == 0)
                        startMatch();
                    else
                        startMatchDelayed(TimeSpan.FromSeconds(startSeconds));
                    return true;

                case "abort":
                    if (!string.IsNullOrEmpty(content))
                    {
                        notify("Usage: /abort");
                        return true;
                    }

                    abortMatch();
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Starts the match immediately. Mirrors <c>MatchStartControl</c>'s referee start.
        /// </summary>
        private void startMatch()
        {
            if (client.Room == null)
                return;

            client.StartMatch().FireAndForget(onError: ex => notify($"Failed to start match: {ex.Message}"));
        }

        /// <summary>
        /// Starts the match after <paramref name="delay"/>. Mirrors <c>MatchStartControl</c>'s countdown button.
        /// </summary>
        private void startMatchDelayed(TimeSpan delay)
        {
            if (client.Room == null)
                return;

            client.SendMatchRequest(new StartMatchCountdownRequest { Duration = delay })
                  .FireAndForget(onError: ex => notify($"Failed to start match: {ex.Message}"));
        }

        /// <summary>
        /// Aborts the running match. Mirrors <c>MatchStartControl</c>'s referee abort
        /// (without the confirm dialog). No-op unless a match is running.
        /// </summary>
        private void abortMatch()
        {
            if (client.Room?.State is not MultiplayerRoomState.WaitingForLoad and not MultiplayerRoomState.Playing)
                return;

            client.AbortMatch().FireAndForget(onError: ex => notify($"Failed to abort match: {ex.Message}"));
        }

        private void notify(string text)
        {
            if (notifications != null)
                notifications.Post(new SimpleNotification { Text = text });
            else
                channel.Value?.AddNewMessages(new ErrorMessage(text));
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            textBox?.OnCommit -= postMessage;

            channel.UnbindAll();

            onlineState?.RoomJoined.ValueChanged -= onRoomJoinedChanged;

            client?.RoomUpdated -= onClientRoomUpdated;

            currentRoom?.PropertyChanged -= onRoomPropertyChanged;
        }

        /// <summary>
        /// Title row showing the available referee chat commands on hover.
        /// </summary>
        private partial class TitleContainer : osu.Framework.Graphics.Containers.Container, IHasTooltip
        {
            public LocalisableString TooltipText => "commands: /timer, /start, /abort";
        }
    }
}
