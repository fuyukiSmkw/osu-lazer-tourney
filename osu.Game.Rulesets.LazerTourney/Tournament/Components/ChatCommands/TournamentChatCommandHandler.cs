// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Extensions.ExceptionExtensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.Countdown;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.LazerTourney.Tournament.Online;
using osu.Game.Rulesets.LazerTourney.Tournament.Online.Referee;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components.ChatCommands
{
    /// <summary>
    /// Handles the tournament chat commands (custom referee commands plus the
    /// officially-handled names listed for completion and help).
    /// All output goes through the <c>notify</c> callback (notifications), never into chat.
    /// Referee hub commands default to the currently joined lazer room and require
    /// the local user to be a referee of that room.
    /// </summary>
    public class TournamentChatCommandHandler : IDisposable
    {
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
            "ban",
            "close",
            "invite",
            "kick",
            "lock",
            "me",
            "move",
            "mv",
            "name",
            "np",
            "password",
            "ping",
            "refadd",
            "refrm",
            "roll",
            "savelog",
            "settings",
            "size",
            "start",
            "team",
            "timer",
            "unlock"
        ];

        /// <summary>
        /// Legacy aliases for the commands in <see cref="COMMANDS"/> (alias -&gt; canonical name).
        /// Values must be entries of <see cref="COMMANDS"/>.
        /// Aliases run and tab-complete exactly like their canonical commands,
        /// but <c>/help</c> only lists canonical names.
        /// </summary>
        public static readonly Dictionary<string, string> COMMAND_ALIASES = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["addref"] = "refadd",
            ["refremove"] = "refrm",
            ["refdel"] = "refrm",
            ["rmref"] = "refrm",
            ["removeref"] = "refrm",
            ["delref"] = "refrm",
            ["slot"] = "move",
            ["edit"] = "settings",
            ["change"] = "settings",
            ["set"] = "settings",
            ["title"] = "name",
        };

        private IAPIProvider api { get; set; } = null!;

        // Reverse view of COMMAND_ALIASES (canonical name -> aliases), built once for listing aliases in /help.
        // C# has no compile-time map building without source generators; a static constructor is the closest equivalent.
        private static readonly Dictionary<string, string[]> aliases_by_command;

        static TournamentChatCommandHandler()
        {
            aliases_by_command = COMMAND_ALIASES
                                 .GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
                                 .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToArray(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves an alias to its canonical command name. Returns the input unchanged when it is not an alias.
        /// </summary>
        public static string ResolveCommandName(string name)
            => COMMAND_ALIASES.TryGetValue(name, out string? canonical) ? canonical : name;

        private static readonly IReadOnlyList<OptionSpec> move_options = new[]
        {
            new OptionSpec('s', "slot"),
            new OptionSpec('t', "team"),
        };

        private static readonly IReadOnlyList<OptionSpec> settings_options = new[]
        {
            new OptionSpec('n', "name"),
            new OptionSpec('p', "password"),
            new OptionSpec('t', "type"),
            new OptionSpec('q', "queue-mode"),
            new OptionSpec('m', "max-participants"),
        };

        private const string user_format_help = "`<user>` = `<User_name/#id>`.";

        private readonly MultiplayerClient client;
        private readonly RefereeAuthController refereeAuth;
        private readonly ChatTimerService timerService;
        private readonly Func<Channel?> getChannel;
        private readonly Action<string, bool> notify;

        private readonly object pendingPingsLock = new object();
        private readonly HashSet<string> pendingPings = new HashSet<string>();

        private string? pendingCloseCode;
        private DateTimeOffset pendingCloseExpiry;

        private bool disposed;

        public TournamentChatCommandHandler(MultiplayerClient client, RefereeAuthController refereeAuth, ChatTimerService timerService,
                                            Func<Channel?> getChannel, Action<string, bool> notify, IAPIProvider api)
        {
            this.client = client;
            this.refereeAuth = refereeAuth;
            this.timerService = timerService;
            this.getChannel = getChannel;
            this.notify = notify;
            this.api = api;

            refereeAuth.PongReceived += onPongReceived;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            refereeAuth.PongReceived -= onPongReceived;
        }

        /// <summary>
        /// Handles a command line without the leading slash.
        /// Returns true when handled (including handled errors), false for unknown
        /// commands so the caller falls through to <c>ChannelManager.PostCommand</c>.
        /// </summary>
        public bool HandleCommand(string commandText)
        {
            if (tryInterceptUnsupportedCommand(commandText))
                return true;

            return tryHandleCustomCommand(commandText);
        }

        /// <summary>
        /// Blocks commands whose official handlers would navigate away or otherwise break
        /// the tournament client. Returns true when blocked (with a notification posted).
        /// </summary>
        private bool tryInterceptUnsupportedCommand(string commandText)
        {
            string commandName = CommandLineParser.Tokenize(commandText).FirstOrDefault()?.ToLowerInvariant() ?? string.Empty;

            if (!UNSUPPORTED_COMMANDS.Contains(commandName))
                return false;

            notify($"/{commandName} is not supported in lazer!tourney.", false);
            return true;
        }

        /// <summary>
        /// Handles the custom commands. Returns false for unknown commands.
        /// </summary>
        private bool tryHandleCustomCommand(string commandText)
        {
            List<string> tokens = CommandLineParser.Tokenize(commandText);

            if (tokens.Count == 0)
            {
                notify("Where's your command?", false);
                return true;
            }

            List<string> args = tokens.Skip(1).ToList();

            switch (ResolveCommandName(tokens[0]).ToLowerInvariant())
            {
                case "help":
                    if (args.Count == 0)
                    {
                        string s = "Available commands:";
                        foreach (string i in COMMANDS)
                            s += $"\n{UsageOfCommand(i)}";
                        string aliasLine = AliasSummary();
                        if (aliasLine != null)
                            s += $"\n \nAliases:\n \n{aliasLine}";
                        notify(s, false);
                    }
                    else if (args.Count == 1)
                    {
                        string c = ResolveCommandName(args[0]);
                        if (COMMANDS.Contains(c))
                        {
                            string t = $"Usage: {UsageOfCommand(c)}\n \n{HelpOfCommand(c)}";
                            string aliasLine = AliasSummary(c);
                            if (aliasLine != null)
                                t += $"\n \nAliases: {aliasLine}";
                            notify(t, false);
                        }
                        else
                            notify($"Command /{args[0]} does not exist.", false);
                    }
                    else
                        notify(UsageOfCommand("help"), false);
                    return true;

                case "timer":
                    if (args.Count == 0 || !int.TryParse(args[0], out int timerSeconds) || timerSeconds < 0)
                        notify(UsageOfCommand("timer"), false);
                    else
                        timerService.StartTimer(timerSeconds, getChannel());
                    return true;

                case "start":
                    if (args.Count == 0)
                    {
                        startMatch();
                        return true;
                    }
                    if (!int.TryParse(args[0], out int startSeconds) || startSeconds < 0)
                        notify(UsageOfCommand("start"), false);
                    else if (startSeconds == 0)
                        startMatch();
                    else
                        startMatchDelayed(TimeSpan.FromSeconds(startSeconds));
                    return true;

                case "abort":
                    if (args.Count != 0)
                    {
                        notify(UsageOfCommand("abort"), false);
                        return true;
                    }
                    abortMatch();
                    return true;

                case "refadd":
                    runForUsers(args, "refadd",
                        (roomId, userId) => refereeAuth.AddRefereeAsync(roomId, userId),
                        name => $"Failed to add {name} as referee!",
                        false);
                    return true;

                case "refrm":
                    runForUsers(args, "refrm",
                        (roomId, userId) => refereeAuth.RemoveRefereeAsync(roomId, userId),
                        name => $"Failed to remove {name} from the referees!",
                        false);
                    return true;

                case "ping":
                    pingServer(string.Join(' ', args));
                    return true;

                case "invite":
                    runForUsers(args, "invite",
                        (roomId, userId) => refereeAuth.InvitePlayerAsync(roomId, userId),
                        name => $"Failed to invite {name}!",
                        false);
                    return true;

                case "kick":
                    runForUsers(args, "kick",
                        // (roomId, userId) => refereeAuth.KickPlayerAsync(roomId, userId),
                        (roomId, userId) => client.KickUser(userId),
                        name => $"Failed to kick {name}!");
                    return true;

                case "ban":
                    runForUsers(args, "ban",
                        (roomId, userId) => refereeAuth.BanUserAsync(roomId, userId),
                        name => $"Failed to ban {name}",
                        false);
                    return true;

                case "mv":
                    moveUser(args);
                    return true;

                case "move":
                    if (args.Count != 2)
                    {
                        notify(UsageOfCommand("move"), false);
                        return true;
                    }
                    moveUserTo(args[0], parseSlot(args[1]), null, "move");
                    return true;

                case "team":
                    if (args.Count != 2)
                    {
                        notify(UsageOfCommand("team"), false);
                        return true;
                    }
                    moveUserTo(args[0], null, parseTeam(args[1]), "team");
                    return true;

                case "settings":
                    changeSettings(args);
                    return true;

                case "password":
                    setRoomPassword(string.Join(' ', args));
                    return true;

                case "size":
                    setMaxParticipants(args);
                    return true;

                case "name":
                    if (args.Count == 0)
                    {
                        notify(UsageOfCommand("name"), false);
                        return true;
                    }
                    applyRoomSettings(new ChangeRoomSettingsRequest { Name = string.Join(' ', args) });
                    return true;

                case "lock":
                    if (args.Count != 0)
                    {
                        notify(UsageOfCommand("lock"), false);
                        return true;
                    }
                    setLockState(true);
                    return true;

                case "unlock":
                    if (args.Count != 0)
                    {
                        notify(UsageOfCommand("unlock"), false);
                        return true;
                    }
                    setLockState(false);
                    return true;

                case "close":
                    closeRoom(args);
                    return true;

                default:
                    return false;
            }
        }

        #region Referee command implementations

        /// <summary>
        /// Checks the preconditions shared by all room-scoped referee commands:
        /// joined to a lazer room, local user is a referee of it, hub is connected.
        /// </summary>
        private bool ensureReferee(out long roomId)
        {
            var room = client.Room;

            if (room == null)
            {
                notify("Not in a room.", false);
                roomId = 0;
                return false;
            }

            roomId = room.RoomID;

            if (!client.IsReferee)
            {
                notify("You are not a referee in this room.", false);
                return false;
            }

            if (refereeAuth.ConnectionState.Value != RefereeConnectionState.Connected)
            {
                notify("Not connected to the referee hub.", false);
                return false;
            }

            return true;
        }

        private static string refereeError(Exception ex)
        {
            Exception? singular = ex is AggregateException aggregate ? aggregate.AsSingular() : ex;
            return singular?.GetHubExceptionMessage() ?? ex.Message;
        }

        private string displayName(int userId)
            => client.Room?.Users.FirstOrDefault(u => u.UserID == userId)?.User?.Username ?? $"#{userId}";

        private static bool matchesUsername(string? username, string token)
            => username != null && (string.Equals(username, token, StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(username, token.Replace('_', ' '), StringComparison.OrdinalIgnoreCase));

        private class ResolvedUser(int id, string name)
        {
            public int id = id;
            public string name = name;
        }

        /// <summary>
        /// Resolves a user in or out of the room.
        /// Failures are notified by this method.
        /// </summary>
        private async Task<ResolvedUser> tryResolveUser(string token)
        {
            int userId = 0; // if #
            if (token.StartsWith('#'))
            {
                if (token.Length > 1 && int.TryParse(token.Substring(1), out int id) && id > 0)
                {
                    userId = id;
                    var u = client.Room?.Users.FirstOrDefault(u => u.User?.Id == id);
                    if (u != null)
                        return new(u.UserID, u.User.Username);
                }
                else
                {
                    notify($"Invalid user id \"{token}\". Use # followed by a positive number.", false);
                    return null;
                }
            }

            var target = client.Room?.Users.FirstOrDefault(u => matchesUsername(u.User?.Username, token));
            if (target != null)
                return new(target.UserID, target.User.Username);

            GetUserRequest req = userId == 0 ? new(token) : new(userId);
            var tcs = new TaskCompletionSource<ResolvedUser>(TaskCreationOptions.RunContinuationsAsynchronously);

            req.Success += u => tcs.TrySetResult(new(u.Id, u.Username));
            req.Failure += e =>
            {
                notify($"Failed to fetch user \"{token}\": {e.Message}", false);
                tcs.TrySetResult(null);
            };
            api.Queue(req);

            return await tcs.Task;
        }

        /// <summary>
        /// Resolves a <c>&lt;user&gt;</c> token to a user id: <c>#12345</c> by id,
        /// anything else by room username (case-insensitive, <c>_</c> counts as a space).
        /// Failures are notified by this method.
        /// </summary>
        private bool tryResolveRoomUser(string token, out int userId)
        {
            userId = 0;

            if (token.StartsWith('#'))
            {
                if (token.Length > 1 && int.TryParse(token.Substring(1), out int id) && id > 0)
                {
                    userId = id;
                    return true;
                }

                notify($"Invalid user id \"{token}\". Use # followed by a positive number.", false);
                return false;
            }

            var target = client.Room?.Users.FirstOrDefault(u => matchesUsername(u.User?.Username, token));

            if (target == null)
            {
                notify($"User {token} does not exist or is not in the room.", false);
                return false;
            }

            userId = target.UserID;
            return true;
        }

        /// <summary>
        /// Resolves every token first; aborts without calling anything when any token fails.
        /// Otherwise calls <paramref name="action"/> per user, notifying each result without blocking the rest.
        /// </summary>
        private async void runForUsers(List<string> args, string command, Func<long, int, Task> action,
                                       Func<string, string> failurePrefix, bool mustInRoom = true)
        {
            if (!ensureReferee(out long roomId))
                return;

            if (args.Count == 0)
            {
                notify(UsageOfCommand(command), false);
                return;
            }

            var targets = new List<(int id, string name)>();

            if (mustInRoom)
            {
                foreach (string token in args)
                {
                    if (tryResolveRoomUser(token, out int userId))
                        targets.Add((userId, displayName(userId)));
                }
            }
            else
            {
                var tasks = args.Select(token => tryResolveUser(token)).ToArray();
                var results = await Task.WhenAll(tasks);
                foreach (var u in results)
                    if (u != null)
                        targets.Add((u.id, u.name));
            }

            if (targets.Count != args.Count)
                return;

            foreach ((int id, string name) in targets)
            {
                try
                {
                    await action(roomId, id).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    notify($"{failurePrefix(name)}: {refereeError(ex)}", true);
                    Logging.LogError(ex);
                }
            }
        }

        private void pingServer(string message)
        {
            if (refereeAuth.ConnectionState.Value != RefereeConnectionState.Connected)
            {
                notify("Not connected to the referee hub.", false);
                return;
            }

            if (string.IsNullOrEmpty(message))
                message = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            lock (pendingPingsLock)
                pendingPings.Add(message);

            sendPing(message);
        }

        private async void sendPing(string message)
        {
            try
            {
                await refereeAuth.PingAsync(message).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lock (pendingPingsLock)
                    pendingPings.Remove(message);

                notify($"Ping failed: {refereeError(ex)}", true);
            }
        }

        private void onPongReceived(string message)
        {
            bool expected;

            lock (pendingPingsLock)
                expected = pendingPings.Remove(message);

            if (expected)
                notify($"Pong received: {message}", false);
        }

        private static byte? parseSlot(string? text)
            => byte.TryParse(text, out byte slot) && slot >= 1 && slot <= 16 ? slot : null;

        private static string? parseTeam(string? text)
        {
            switch (text?.ToLowerInvariant())
            {
                case "r":
                case "red":
                case "0":
                    return "red";

                case "b":
                case "blue":
                case "1":
                    return "blue";

                default:
                    return null;
            }
        }

        private static string? parseMatchType(string? text)
        {
            switch (text?.ToLowerInvariant())
            {
                case "head_to_head":
                    return "head_to_head";

                case "team_versus":
                    return "team_versus";

                default:
                    return null;
            }
        }

        private static string? parseQueueMode(string? text)
        {
            switch (text?.ToLowerInvariant())
            {
                case "host_only":
                    return "host_only";

                case "all_players":
                    return "all_players";

                case "all_players_round_robin":
                    return "all_players_round_robin";

                default:
                    return null;
            }
        }

        private void moveUser(List<string> args)
        {
            if (!ensureReferee(out long roomId))
                return;

            if (!CommandLineParser.TryParseOptions(args, move_options, out List<string> positionals, out Dictionary<string, string?> options, out _)
                || positionals.Count != 1 || options.Count == 0)
            {
                notify(UsageOfCommand("mv"), false);
                return;
            }

            if (!tryResolveRoomUser(positionals[0], out int userId))
                return;

            byte? slot = null;
            string? team = null;

            if (options.TryGetValue("slot", out string? slotText))
            {
                slot = parseSlot(slotText);

                if (slot == null)
                {
                    notify(UsageOfCommand("mv"), false);
                    return;
                }
            }

            if (options.TryGetValue("team", out string? teamText))
            {
                team = parseTeam(teamText);

                if (team == null)
                {
                    notify(UsageOfCommand("mv"), false);
                    return;
                }
            }

            moveUserTo(roomId, userId, displayName(userId), slot, team);
        }

        private void moveUserTo(string userToken, byte? slot, string? team, string command)
        {
            if (!ensureReferee(out long roomId))
                return;

            if ((command == "move" && slot == null) || (command == "team" && team == null))
            {
                notify(UsageOfCommand(command), false);
                return;
            }

            if (!tryResolveRoomUser(userToken, out int userId))
                return;

            moveUserTo(roomId, userId, displayName(userId), slot, team);
        }

        private async void moveUserTo(long roomId, int userId, string name, byte? slot, string? team)
        {
            try
            {
                await refereeAuth.MoveUserAsync(roomId, new MoveUserRequest { UserId = userId, Slot = slot, Team = team }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                notify($"Failed to move {name}: {refereeError(ex)}", true);
            }
        }

        private void changeSettings(List<string> args)
        {
            if (!ensureReferee(out long roomId))
                return;

            if (!CommandLineParser.TryParseOptions(args, settings_options, out List<string> positionals, out Dictionary<string, string?> options, out _)
                || positionals.Count > 0)
            {
                notify(UsageOfCommand("settings"), false);
                return;
            }

            if (options.Count == 0)
            {
                showRoomSettings();
                return;
            }

            var request = new ChangeRoomSettingsRequest();

            if (options.TryGetValue("name", out string? name))
                request.Name = name;

            if (options.TryGetValue("password", out string? password))
                request.Password = password;

            if (options.TryGetValue("type", out string? typeText))
            {
                string? type = parseMatchType(typeText);

                if (type == null)
                {
                    notify(UsageOfCommand("settings"), false);
                    return;
                }

                request.MatchType = type;
            }

            if (options.TryGetValue("queue-mode", out string? queueText))
            {
                string? queueMode = parseQueueMode(queueText);

                if (queueMode == null)
                {
                    notify(UsageOfCommand("settings"), false);
                    return;
                }

                request.QueueMode = queueMode;
            }

            if (options.TryGetValue("max-participants", out string? maxText))
            {
                if (!byte.TryParse(maxText, out byte max) || (max != 0 && max < 2) || max > 16)
                {
                    notify($"Invalid participant count \"{maxText}\". Use 0 for unlimited, or 2-16.", false);
                    return;
                }

                request.MaxParticipants = max;
            }

            applyRoomSettings(request);
        }

        private void setRoomPassword(string password)
            => applyRoomSettings(new ChangeRoomSettingsRequest { Password = password });

        private void setMaxParticipants(List<string> args)
        {
            if (args.Count > 1)
            {
                notify(UsageOfCommand("size"), false);
                return;
            }

            byte max = 0;

            if (args.Count == 1 && (!byte.TryParse(args[0], out max) || (max != 0 && max < 2) || max > 16))
            {
                notify($"Invalid participant count \"{args[0]}\". Use 0 for unlimited, or 2-16.", false);
                return;
            }

            applyRoomSettings(new ChangeRoomSettingsRequest { MaxParticipants = max });
        }

        private async void applyRoomSettings(ChangeRoomSettingsRequest request)
        {
            if (!ensureReferee(out long roomId))
                return;

            try
            {
                await refereeAuth.ChangeRoomSettingsAsync(roomId, request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                notify($"Failed to change room settings: {refereeError(ex)}", true);
            }
        }

        /// <summary>
        /// Prints the current name, match type, queue mode and participant limit. Never prints the password.
        /// </summary>
        private void showRoomSettings()
        {
            var settings = client.Room?.Settings;

            if (settings == null)
            {
                notify("Not in a room.", false);
                return;
            }

            notify($"Room settings:\nname: {settings.Name}\ntype: {toRefereeMatchType(settings.MatchType)}\nqueue mode: {toRefereeQueueMode(settings.QueueMode)}"
                   + $"\nmax participants: {(settings.MaxParticipants?.ToString() ?? "unlimited")}", false);
        }

        private void setLockState(bool locked)
        {
            if (!ensureReferee(out long roomId))
                return;

            setLockState(roomId, locked);
        }

        private async void setLockState(long roomId, bool locked)
        {
            try
            {
                await refereeAuth.SetLockStateAsync(roomId, locked).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                notify($"Failed to {(locked ? "lock" : "unlock")} room: {refereeError(ex)}", true);
            }
        }

        private void closeRoom(List<string> args)
        {
            if (args.Count == 0)
            {
                if (!ensureReferee(out _))
                    return;

                pendingCloseCode = Random.Shared.Next(0, 100_000_000).ToString("D8");
                pendingCloseExpiry = DateTimeOffset.UtcNow.AddSeconds(10);
                notify($"Type /close {pendingCloseCode} within 10 seconds to confirm closing the room.", false);
                return;
            }

            if (args.Count != 1)
            {
                notify(UsageOfCommand("close"), false);
                return;
            }

            if (pendingCloseCode == null || DateTimeOffset.UtcNow > pendingCloseExpiry)
            {
                pendingCloseCode = null;
                notify(UsageOfCommand("close"), false);
                return;
            }

            if (!string.Equals(args[0], pendingCloseCode, StringComparison.Ordinal))
            {
                pendingCloseCode = null;
                notify("Room close cancelled.", false);
                return;
            }

            pendingCloseCode = null;

            if (!ensureReferee(out long roomId))
                return;

            closeRoom(roomId);
        }

        private async void closeRoom(long roomId)
        {
            try
            {
                await refereeAuth.CloseRoomAsync(roomId).ConfigureAwait(false);
                notify("Room closed.", false);
            }
            catch (Exception ex)
            {
                notify($"Failed to close room: {refereeError(ex)}", true);
            }
        }

        private static string toRefereeMatchType(MatchType type) => type == MatchType.TeamVersus ? "team_versus" : "head_to_head";

        private static string toRefereeQueueMode(QueueMode mode)
        {
            switch (mode)
            {
                case QueueMode.AllPlayers:
                    return "all_players";

                case QueueMode.AllPlayersRoundRobin:
                    return "all_players_round_robin";

                default:
                    return "host_only";
            }
        }

        #endregion

        #region Lazer commands

        /// <summary>
        /// Starts the match immediately. Mirrors <c>MatchStartControl</c>'s referee start.
        /// </summary>
        private void startMatch()
        {
            if (client.Room == null)
                return;

            if (!isPriviledged())
            {
                notify("You don't have permission to start.", false);
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
                notify("You don't have permission to start.", false);
                return;
            }

            client.SendMatchRequest(new StartMatchCountdownRequest { Duration = delay })
                  .FireAndForget(onError: ex => notify($"Failed to start match: {ex.Message}", true));
        }

        /// <summary>
        /// Aborts the running match. Mirrors <c>MatchStartControl</c>'s referee abort
        /// (without the confirm dialog). No-op unless a match is running.
        /// </summary>
        private void abortMatch()
        {
            if (client.Room?.State is not MultiplayerRoomState.WaitingForLoad and not MultiplayerRoomState.Playing)
            {
                notify("No ongoing match.", false);
                return;
            }

            if (!isPriviledged())
            {
                notify("You don't have permission to abort.", false);
                return;
            }

            client.AbortMatch().FireAndForget(onError: ex => notify($"Failed to abort match: {ex.Message}", true));
        }

        private bool isPriviledged() => client.IsHost || client.IsReferee;

        #endregion

        #region Help for commands

        /// <summary>
        /// Summarises all aliases in one line for the end of <c>/help</c>, or null when there are none.
        /// </summary>
        public static string AliasSummary(string? c = null)
        {
            static string makePartOfOneCommand(string cmd)
            {
                var lilparts = new List<string>();
                if (!aliases_by_command.TryGetValue(cmd, out string[]? aliases))
                    return null;

                foreach (string a in aliases)
                    lilparts.Add($"/{a}");

                return string.Join(", ", lilparts);
            }

            if (c != null)
                return makePartOfOneCommand(c);

            var parts = new List<string>();

            foreach (string cmd in COMMANDS)
            {
                string s = makePartOfOneCommand(cmd);
                if (s != null)
                    parts.Add($"{s} -> /{cmd}");
            }

            return parts.Count == 0 ? null : string.Join(";  ", parts);
        }

        public static string UsageOfCommand(string cmd)
        {
            switch (cmd)
            {
                case "help":
                    return "/help [command]";
                case "abort":
                    return "/abort";
                case "ban":
                    return "/ban <user> [user]...";
                case "close":
                    return "/close [confirmcode]";
                case "invite":
                    return "/invite <user> [user]...";
                case "kick":
                    return "/kick <user> [user]...";
                case "lock":
                    return "/lock";
                case "me":
                    return "/me <message>";
                case "move":
                    return "/move <user> <number>";
                case "mv":
                    return "/mv <user> [-s/--slot <number>] [-t/--team <team>]";
                case "name":
                    return "/name <string>";
                case "np":
                    return "/np";
                case "password":
                    return "/password [string]";
                case "ping":
                    return "/ping [message]";
                case "refadd":
                    return "/refadd <user> [user]...";
                case "refrm":
                    return "/refrm <user> [user]...";
                case "roll":
                    return "/roll [2~100]";
                case "savelog":
                    return "/savelog";
                case "settings":
                    return "/settings [...]";
                case "size":
                    return "/size [number]";
                case "start":
                    return "/start [number]";
                case "team":
                    return "/team <user> <team>";
                case "timer":
                    return "/timer <number>";
                case "unlock":
                    return "/unlock";
            }
            return null;
        }

        public static string HelpOfCommand(string cmd)
        {
            switch (cmd)
            {
                case "help":
                    return "Show the list of all commands or help for a certain command";
                case "abort":
                    return "Abort the ongoing match.";
                case "ban":
                    return $"Ban users from the room.\n \n{user_format_help}";
                case "close":
                    return "Close the room, kicking everyone in it. Run without arguments to get a confirmation code, then repeat with the code within 10 seconds.";
                case "invite":
                    return $"Invite users to the room.\n \n{user_format_help}";
                case "kick":
                    return $"Kick users from the room.\n \n{user_format_help}";
                case "lock":
                    return "Lock the room so players cannot change teams or slots.";
                case "me":
                    return "Send an action message.";
                case "move":
                    return $"Move a user to a slot (1-16). Same as /mv with -s.\n \n{user_format_help}";
                case "mv":
                    return $"Move a user to a slot (1-16) and/or a team (r/b/red/blue/0/1).\n \n{user_format_help}";
                case "name":
                    return "Rename the room.";
                case "np":
                    return "Print to chat the current song you are listening to.";
                case "password":
                    return "Set the room password. No argument clears the password.";
                case "ping":
                    return "Ping the referee hub and print the pong reply.";
                case "refadd":
                    return $"Add users as referees of the room. Only the room host can do this.\n \n{user_format_help}";
                case "refrm":
                    return $"Remove users from the referees of the room. Only the room host can do this.\n \n{user_format_help}";
                case "roll":
                    return "Rolls a random number.";
                case "savelog":
                    return "Saves the current chat tab to a text file.";
                case "settings":
                    return "Show or change room settings.\n \nFull usage: /settings [-n/--name <string>] [-p/--password <string>] [-t/--type <type>] [-q/--queue-mode <queuemode>] [-m/--max-participants <number>]\n \n"
                           + "No arguments prints the current settings. Omit -p to keep the password, use -p \"\" to clear it. "
                           + "Omit -m to keep the participant limit, use -m 0 for unlimited (valid values: 0, 2-16). "
                           + "Type is head_to_head or team_versus; queue mode is host_only, all_players or all_players_round_robin.";
                case "size":
                    return "Set the participant limit (0 for unlimited, 2-16). No argument removes the limit.";
                case "start":
                    return "Start match immediately or in specific seconds.";
                case "team":
                    return $"Move a user to a team (r/b/red/blue/0/1). Same as /mv with -t.\n \n{user_format_help}";
                case "timer":
                    return "Start a local timer that sends message every 30 seconds or at the last few seconds.\nWill notify you when the timer ends.\n/start 0 to stop the current timer.";
                case "unlock":
                    return "Unlock the room so players can change teams and slots.";
            }
            return null;
        }

        #endregion
    }
}
