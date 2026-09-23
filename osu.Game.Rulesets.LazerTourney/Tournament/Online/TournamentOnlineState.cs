// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Threading.Tasks;
using Humanizer;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;
using osu.Game.Online.Rooms;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Online
{
    /// <summary>
    /// Exposes live tournament-relevant information about the currently joined multiplayer room.
    /// </summary>
    /// <remarks>
    /// This is the online replacement for the tournament client's stable IPC state. Unlike the IPC
    /// implementation, it is not polled from disk but is projected from <see cref="MultiplayerClient.Room"/>.
    /// </remarks>
    public partial class TournamentOnlineState : Component
    {
        /// <summary>
        /// Whether the local user is currently in a multiplayer room.
        /// </summary>
        public IBindable<bool> RoomJoined => roomJoined;

        private readonly BindableBool roomJoined = new BindableBool();

        /// <summary>
        /// The currently selected playlist item, if a room is joined.
        /// </summary>
        public IBindable<MultiplayerPlaylistItem?> CurrentPlaylistItem => currentPlaylistItem;

        private readonly Bindable<MultiplayerPlaylistItem?> currentPlaylistItem = new Bindable<MultiplayerPlaylistItem?>();

        /// <summary>
        /// The state of the joined room, or <see cref="MultiplayerRoomState.Closed"/> when not joined.
        /// </summary>
        public IBindable<MultiplayerRoomState> RoomState => roomState;

        private readonly Bindable<MultiplayerRoomState> roomState = new Bindable<MultiplayerRoomState>();

        /// <summary>
        /// The settings of the joined room, if any.
        /// </summary>
        public IBindable<MultiplayerRoomSettings?> Settings => settings;

        private readonly Bindable<MultiplayerRoomSettings?> settings = new Bindable<MultiplayerRoomSettings?>();

        /// <summary>
        /// The name of the joined room, or an empty string when not joined.
        /// </summary>
        public IBindable<string> RoomName => roomName;

        private readonly Bindable<string> roomName = new Bindable<string>(string.Empty);

        /// <summary>
        /// The local user's state in the joined room, if any.
        /// Used by future referee control panels to gate host-only operations.
        /// </summary>
        public IBindable<MultiplayerUserState?> LocalUserState => localUserState;

        private readonly Bindable<MultiplayerUserState?> localUserState = new Bindable<MultiplayerUserState?>();

        /// <summary>
        /// Whether the local user is the host of the joined room.
        /// </summary>
        public IBindable<bool> IsHost => isHost;

        private readonly BindableBool isHost = new BindableBool();

        /// <summary>
        /// Whether the local user is a referee of the joined room.
        /// </summary>
        public IBindable<bool> IsReferee => isReferee;

        private readonly BindableBool isReferee = new BindableBool();

        /// <summary>
        /// The ID of the last joined room, kept in memory for quick rejoin after disconnects.
        /// </summary>
        public long? LastRoomID { get; private set; }

        /// <summary>
        /// The password of the last joined room, kept in memory for quick rejoin after disconnects.
        /// </summary>
        public string? LastRoomPassword { get; private set; }

        [Resolved]
        private MultiplayerClient multiplayerClient { get; set; } = null!;

        [Resolved]
        private ChannelManager? channelManager { get; set; }

        /// <summary>
        /// The underlying multiplayer client. Exposed for screens that need richer room access.
        /// </summary>
        public MultiplayerClient Client => multiplayerClient;

        /// <summary>
        /// The joined <see cref="MultiplayerRoom"/>, if any.
        /// </summary>
        public MultiplayerRoom? Room => multiplayerClient.Room;

        /// <summary>
        /// The API <see cref="Room"/> representing the joined room, if available.
        /// </summary>
        public Room? ApiRoom { get; private set; }

        private Room? pendingApiRoom;

        /// <summary>
        /// Stores a <see cref="Room"/> to be used as <see cref="ApiRoom"/> after the next
        /// <see cref="MultiplayerClient.RoomUpdated"/> callback. Call this before
        /// <see cref="MultiplayerClient.CreateRoom"/> or <see cref="MultiplayerClient.JoinRoom"/>
        /// so that the API room is available immediately.
        /// </summary>
        public void SetPendingRoom(Room room) => pendingApiRoom = room;

        [BackgroundDependencyLoader]
        private void load()
        {
            multiplayerClient.RoomUpdated += onRoomUpdated;
            multiplayerClient.UserStateChanged += onUserStateChanged;
            multiplayerClient.MatchEvent += onMatchEvent;
            onRoomUpdated();
        }

        /// <summary>
        /// Ensures the local user stays spectating (not participating) in the joined room.
        /// Call this after <see cref="MultiplayerClient.CreateRoom"/> or <see cref="MultiplayerClient.JoinRoom"/>.
        /// </summary>
        public async Task EnsureSpectateAsync()
        {
            try
            {
                // May be called before this component finishes loading (dependency not yet injected).
                if (multiplayerClient == null)
                    return;

                var localUser = multiplayerClient.LocalUser;

                if (localUser == null)
                    return;

                if (localUser.State != MultiplayerUserState.Idle && localUser.State != MultiplayerUserState.Ready)
                    return;

                await multiplayerClient.ChangeState(MultiplayerUserState.Spectating).ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                Logger.Log($"Failed to enter spectate state: {ex.Message}", LoggingTarget.Runtime, LogLevel.Important);
            }
        }

        private void onUserStateChanged(MultiplayerRoomUser user, MultiplayerUserState state) => Schedule(() =>
        {
            if (multiplayerClient.LocalUser == null || user.UserID != multiplayerClient.LocalUser.UserID)
                return;

            localUserState.Value = state;

            // The server may reset the local user to idle (e.g. after playlist changes), so re-assert spectate.
            if (state == MultiplayerUserState.Idle || state == MultiplayerUserState.Ready)
                EnsureSpectateAsync().FireAndForget();
        });

        private void onRoomUpdated() => Schedule(() =>
        {
            var room = multiplayerClient.Room;

            if (room == null)
            {
                roomJoined.Value = false;
                currentPlaylistItem.Value = null;
                roomState.Value = MultiplayerRoomState.Closed;
                settings.Value = null;
                roomName.Value = string.Empty;
                localUserState.Value = null;
                isHost.Value = false;
                isReferee.Value = false;
                ApiRoom = null;
                return;
            }

            roomJoined.Value = true;
            currentPlaylistItem.Value = room.CurrentPlaylistItem;
            roomState.Value = room.State;
            settings.Value = room.Settings;
            roomName.Value = room.Settings.Name;
            localUserState.Value = multiplayerClient.LocalUser?.State;
            isHost.Value = multiplayerClient.IsHost;
            isReferee.Value = multiplayerClient.IsReferee;

            // Remember the room for quick rejoin after disconnects (in-memory only).
            LastRoomID = room.RoomID;
            LastRoomPassword = room.Settings.Password;

            // Use pending room if available, otherwise reconstruct from joined room.
            // The Room(MultiplayerRoom) constructor mirrors MultiplayerClient.setupJoinedRoom
            // and copies ChannelId, Playlist, CurrentPlaylistItem and host data.
            if (pendingApiRoom != null)
            {
                ApiRoom = pendingApiRoom;
                pendingApiRoom = null;
            }
            else if (ApiRoom == null || ApiRoom.RoomID != room.RoomID)
            {
                ApiRoom = new Room(room);
            }

            // Keep the local user spectating while joined, matching the official
            // lounge -> room flow where the tournament client never participates.
            // Only Idle/Ready can transition to Spectating (see MultiplayerClient.ToggleSpectate).
            var localUser = multiplayerClient.LocalUser;

            if (localUser?.State == MultiplayerUserState.Idle || localUser?.State == MultiplayerUserState.Ready)
                EnsureSpectateAsync().FireAndForget();
        });

        /// <summary>
        /// Gets the team ID for a given user, if the room is in a team versus match.
        /// </summary>
        public int? GetTeamId(int userId)
            => multiplayerClient.Room?.Users.FirstOrDefault(u => u.UserID == userId)?.MatchState is TeamVersusUserState teamState
                   ? teamState.TeamID
                   : null;

        /// <summary>
        /// Posts server match events that have a chat representation (e.g. /roll results).
        /// Mirrors <c>MultiplayerMatchSubScreen.onMatchEvent</c>: /roll is sent as a
        /// <c>RollRequest</c> and its result comes back as a <c>RollEvent</c> here, not as a
        /// chat message. Without this subscription the result is silently dropped and /roll
        /// appears to do nothing. The message is added to the shared room channel, so it shows
        /// in RoomScreen, the tournament overlay and every synced send box's channel.
        /// </summary>
        private void onMatchEvent(MatchServerEvent matchEvent)
        {
            switch (matchEvent)
            {
                case RollEvent rollEvent:
                {
                    var room = multiplayerClient.Room;

                    if (room == null)
                        return;

                    var user = room.Users.SingleOrDefault(u => u.UserID == rollEvent.UserID)?.User ?? APIUser.UnknownUser(rollEvent.UserID);
                    string text = $"{user.Username} rolled {"point".ToQuantity(rollEvent.Result)} out of {rollEvent.Max}.";

                    var channel = channelManager?.JoinChannel(new Channel { Id = room.ChannelID, Type = ChannelType.Multiplayer, Name = $"#lazermp_{room.RoomID}" });
                    channel?.AddNewMessages(new InfoMessage(text));
                    break;
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (multiplayerClient != null)
            {
                multiplayerClient.RoomUpdated -= onRoomUpdated;
                multiplayerClient.UserStateChanged -= onUserStateChanged;
                multiplayerClient.MatchEvent -= onMatchEvent;
            }
        }
    }
}
