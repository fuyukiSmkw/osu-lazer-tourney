// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

        private TournamentChatTextBox textBox = null!;
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

        #region Commands

        public static readonly List<string> UNSUPPORTED_COMMANDS =
        [
            "join",
            "chat",
            "msg",
            "query",
            "watch"
        ];

        public static readonly List<string> COMMANDS =
        [
            "help",
            "abort",
            "kick",
            "me",
            "np",
            "roll",
            "savelog",
            "start",
            "timer"
        ];

        /// <summary>
        /// Legacy aliases for the commands in <see cref="COMMANDS"/> (alias -&gt; canonical name).
        /// Values must be entries of <see cref="COMMANDS"/>.
        /// Aliases run and tab-complete exactly like their canonical commands,
        /// but <c>/help</c> only lists canonical names.
        /// </summary>
        public static readonly Dictionary<string, string> COMMAND_ALIASES = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // e.g. ["oldname"] = "newname",
        };

        /// <summary>
        /// Resolves an alias to its canonical command name. Returns the input unchanged when it is not an alias.
        /// </summary>
        public static string ResolveCommandName(string name)
            => COMMAND_ALIASES.TryGetValue(name, out string? canonical) ? canonical : name;

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

            if (text[0] == '/' && tryInterceptUnsupportedCommand(text.Substring(1)))
            {
                textBox.Text = string.Empty;
                return;
            }

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
        /// Blocks commands whose official handlers would navigate away or otherwise break
        /// the tournament client. Returns true when blocked (with a notification posted).
        /// </summary>
        private bool tryInterceptUnsupportedCommand(string commandText)
        {
            string commandName = commandText.Split(' ', 2)[0].ToLowerInvariant();

            if (!UNSUPPORTED_COMMANDS.Contains(commandName))
                return false;

            notify($"/{commandName} is not supported in lazer!tourney.");
            return true;
        }

        /// <summary>
        /// Handles the custom referee commands. Returns false for unknown commands
        /// so the caller falls through to <see cref="ChannelManager.PostCommand"/>.
        /// </summary>
        private bool tryHandleCustomCommand(string commandText)
        {
            string[] parts = commandText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 0)
            {
                notify("Where's your command?");
                return true;
            }

            bool noArg = parts.Length == 1;

            switch (ResolveCommandName(parts[0]).ToLowerInvariant())
            {
                case "help":
                    if (noArg)
                    {
                        string s = "Available commands:";
                        foreach (string i in COMMANDS)
                            s += $"\n{usageOfCommand(i)}";
                        string aliasLine = aliasSummary();
                        if (aliasLine != null)
                            s += $"\n{aliasLine}";
                        notify(s);
                    }
                    else if (parts.Length == 2)
                    {
                        string c = ResolveCommandName(parts[1]);
                        if (COMMANDS.Contains(c))
                            notify($"Usage: {usageOfCommand(c)}\n{helpOfCommand(c)}");
                        else
                            notify($"Command /{parts[1]} does not exist.");
                    }
                    else
                        notify(usageOfCommand("help"));
                    return true;

                case "timer":
                    if (noArg || !int.TryParse(parts[1], out int timerSeconds) || timerSeconds < 0)
                        notify(usageOfCommand("timer"));
                    else
                        timerService.StartTimer(timerSeconds, channel.Value);
                    return true;

                case "start":
                    if (noArg)
                    {
                        startMatch();
                        return true;
                    }
                    if (!int.TryParse(parts[1], out int startSeconds) || startSeconds < 0)
                        notify(usageOfCommand("start"));
                    else if (startSeconds == 0)
                        startMatch();
                    else
                        startMatchDelayed(TimeSpan.FromSeconds(startSeconds));
                    return true;

                case "abort":
                    if (!noArg)
                    {
                        notify(usageOfCommand("abort"));
                        return true;
                    }
                    abortMatch();
                    return true;

                case "kick":
                    if (noArg)
                        notify(usageOfCommand("kick"));
                    else
                        kickUser(string.Join(' ', parts.Skip(1)));
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

            if (!isPriviledged())
            {
                notify("You don't have permission to start.");
                return;
            }

            client.StartMatch().FireAndForget(onError: ex => notify($"Failed to start match: {ex.Message}", true));
        }

        /// <summary>
        /// Starts the match after <paramref name="delay"/>. Mirrors <c>MatchStartControl</c>'s countdown button.
        /// </summary>
        private void startMatchDelayed(TimeSpan delay)
        {
            if (client.Room == null)
                return;

            if (!isPriviledged())
            {
                notify("You don't have permission to start.");
                return;
            }

            client.SendMatchRequest(new StartMatchCountdownRequest { Duration = delay })
                  .FireAndForget(onError: ex => notify($"Failed to start match: {ex.Message}", true));
        }

        /// <summary>
        /// Kicks a room user by username. Mirrors the participant list kick button,
        /// plus a chat message announcing the kick.
        /// </summary>
        private void kickUser(string username)
        {
            var room = client.Room;

            if (room == null)
                return;

            if (!isPriviledged())
            {
                notify("You don't have permission to kick users.");
                return;
            }

            var target = room.Users.FirstOrDefault(u => string.Equals(u.User?.Username, username, StringComparison.OrdinalIgnoreCase));

            if (target == null)
            {
                notify($"User {username} does not exist or is not in the room.");
                return;
            }

            client.KickUser(target.UserID).FireAndForget(
                onSuccess: () => channelManager?.PostMessage($"Kicked {target.User?.Username}", target: channel.Value),
                onError: ex => notify($"Failed to kick {username}: {ex.Message}", true));

        }

        /// <summary>
        /// Aborts the running match. Mirrors <c>MatchStartControl</c>'s referee abort
        /// (without the confirm dialog). No-op unless a match is running.
        /// </summary>
        private void abortMatch()
        {
            if (client.Room?.State is not MultiplayerRoomState.WaitingForLoad and not MultiplayerRoomState.Playing)
            {
                notify("No ongoing match.");
                return;
            }

            if (!isPriviledged())
            {
                notify("You don't have permission to abort.");
                return;
            }

            client.AbortMatch().FireAndForget(onError: ex => notify($"Failed to abort match: {ex.Message}", true));
        }

        private bool isPriviledged() => client.IsHost || client.IsReferee;

        private void notify(string text, bool important = false)
        {
            if (notifications != null)
            {
                if (important)
                    notifications.Post(new TournamentNotification { Text = text, IsImportant = true });
                else
                    notifications.Post(new TournamentNotification { Text = text, Transient = true });
            }
            else
                channel.Value?.AddNewMessages(new ErrorMessage(text));
        }

        #endregion Commands

        #region Help for commands

        // Reverse view of COMMAND_ALIASES (canonical name -> aliases), built once for listing aliases in /help.
        // C# has no compile-time map building without source generators; a static constructor is the closest equivalent.
        private static readonly Dictionary<string, string[]> aliases_by_command;

        static SyncedControlPanelBottomBar()
        {
            aliases_by_command = COMMAND_ALIASES
                               .GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
                               .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToArray(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Summarises all aliases in one line for the end of <c>/help</c>, or null when there are none.
        /// </summary>
        private static string aliasSummary(string? cmd = null)
        {
            var parts = new List<string>();
            void addParts(string c)
            {
                if (!aliases_by_command.TryGetValue(c, out string[]? aliases))
                    return;
                foreach (string a in aliases)
                    parts.Add($"/{a}");
            }

            if (cmd is null) // all
                foreach (string c in COMMANDS)
                    addParts(c);
            else
                addParts(cmd);

            return parts.Count == 0 ? null : "Aliases: " + string.Join(", ", parts);
        }

        private string usageOfCommand(string cmd)
        {
            switch (cmd)
            {
                case "help":
                    return "/help [command]";
                case "abort":
                    return "/abort";
                case "kick":
                    return "/kick <username>";
                case "me":
                    return "/me <message>";
                case "np":
                    return "/np";
                case "roll":
                    return "/roll [2~100]";
                case "savelog":
                    return "/savelog";
                case "start":
                    return "/start [number]";
                case "timer":
                    return "/timer <number>";
            }
            return null;
        }

        private string helpOfCommand(string cmd)
        {
            switch (cmd)
            {
                case "help":
                    return "Show the list of all commands or help for a certain command";
                case "abort":
                    return "Abort the ongoing match.";
                case "kick":
                    return "Kick a user from the room by username.";
                case "me":
                    return "Send an action message.";
                case "np":
                    return "Print to chat the current song you are listening to.";
                case "roll":
                    return "Rolls a random number.";
                case "savelog":
                    return "Saves the current chat tab to a text file.";
                case "start":
                    return "Start match immediately or in specific seconds.";
                case "timer":
                    return "Start a local timer that sends message every 30 seconds or at the last few seconds.\nWill notify you when the timer ends.\n/start 0 to stop the current timer.";
            }
            return null;
        }

        #endregion

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
            public LocalisableString TooltipText => "/help for a list of commands\ntab for username completion";
        }
    }
}
