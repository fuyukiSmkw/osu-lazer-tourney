// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Online.API;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Screens.OnlinePlay;
using osu.Game.Screens.OnlinePlay.Multiplayer.Match.Playlist;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// The room's beatmap queue for the synced control-panel blocks.
    /// Mirrors the queue column of <c>RoomScreen</c> (official <see cref="MultiplayerQueueList"/>),
    /// except items can only be downloaded or deleted: the edit button is never shown
    /// and there is no "add beatmap" button (use the map pool screen to change the playlist).
    /// The height follows the content so the outer control-panel scroll container handles overflow.
    /// </summary>
    /// <remarks>
    /// The download button comes from <see cref="DrawableRoomPlaylistItem"/> itself
    /// (shown on demand when the beatmap is missing locally).
    /// Population from <see cref="MultiplayerClient"/> events mirrors <see cref="MultiplayerPlaylist"/>,
    /// minus the history list.
    /// </remarks>
    public partial class TournamentQueueList : MultiplayerQueueList
    {
        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        private bool firstPopulation = true;

        protected override DrawableRoomPlaylistItem CreateDrawablePlaylistItem(PlaylistItem item) => new NoEditQueuePlaylistItem(item);

        // Mirrors DrawableRoomPlaylist's factory, but non-scrolling: the list is exactly content-sized,
        // so wheel/drag gestures pass through to the outer panel (see NonScrollingOsuScrollContainer).
        protected override ScrollContainer<Drawable> CreateScrollContainer() => new NonScrollingOsuScrollContainer
        {
            ScrollbarVisible = false,
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Items.BindCollectionChanged((_, _) => updateHeight(), true);

            client.ItemAdded += playlistItemAdded;
            client.ItemRemoved += playlistItemRemoved;
            client.ItemChanged += playlistItemChanged;
            client.RoomUpdated += onRoomUpdated;

            updateState();
        }

        private void updateHeight() => Height = Items.Count == 0 ? 0 : Items.Count * (DrawableRoomPlaylistItem.HEIGHT + 2) - 2;

        private void onRoomUpdated() => Scheduler.AddOnce(updateState);

        private void updateState()
        {
            if (client.Room == null)
            {
                Items.Clear();
                firstPopulation = true;
                return;
            }

            if (firstPopulation)
            {
                foreach (var item in client.Room.Playlist)
                    addItem(item);

                firstPopulation = false;
            }

            PlaylistItem? currentItem = client.Room == null ? null : new PlaylistItem(client.Room.CurrentPlaylistItem);
            SelectedItem.Value = currentItem;
        }

        private void playlistItemAdded(MultiplayerPlaylistItem item) => Scheduler.Add(() => addItem(item));

        private void playlistItemRemoved(long item) => Scheduler.Add(() => Items.RemoveAll(i => i.ID == item));

        private void playlistItemChanged(MultiplayerPlaylistItem item) => Scheduler.Add(() =>
        {
            if (client.Room == null)
                return;

            var existingItem = Items.SingleOrDefault(i => i.ID == item.ID);

            // Test if the only change between the two playlist items is the order.
            if (existingItem != null && existingItem.With(playlistOrder: item.PlaylistOrder).Equals(new PlaylistItem(item)))
            {
                // Set the new order directly and refresh the flow layout as an optimisation to avoid refreshing the items' visual state.
                existingItem.PlaylistOrder = item.PlaylistOrder;
                Invalidate();
            }
            else
            {
                Items.RemoveAll(i => i.ID == item.ID);
                addItem(item);
            }
        });

        private void addItem(MultiplayerPlaylistItem item)
        {
            if (client.Room == null)
                return;

            // Expired items have no history view here.
            if (!item.Expired)
                Items.Add(new PlaylistItem(item));
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (client.IsNotNull())
            {
                client.ItemAdded -= playlistItemAdded;
                client.ItemRemoved -= playlistItemRemoved;
                client.ItemChanged -= playlistItemChanged;
                client.RoomUpdated -= onRoomUpdated;
            }
        }

        /// <summary>
        /// Queue item with delete (and the built-in on-demand download) buttons only.
        /// Mirrors <c>MultiplayerQueueList.QueuePlaylistItem</c> minus editing.
        /// </summary>
        private partial class NoEditQueuePlaylistItem : DrawableRoomPlaylistItem
        {
            [Resolved]
            private IAPIProvider api { get; set; } = null!;

            [Resolved]
            private MultiplayerClient multiplayerClient { get; set; } = null!;

            public NoEditQueuePlaylistItem(PlaylistItem item)
                : base(item)
            {
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                RequestDeletion = item => multiplayerClient.RemovePlaylistItem(item.ID).FireAndForget();

                multiplayerClient.RoomUpdated += onRoomUpdated;
                onRoomUpdated();
            }

            private void onRoomUpdated() => Scheduler.AddOnce(updateButtons);

            private void updateButtons()
            {
                if (multiplayerClient.Room == null)
                    return;

                bool isItemOwnerOrReferee = Item.OwnerID == api.LocalUser.Value.OnlineID || multiplayerClient.IsHost || multiplayerClient.IsReferee;
                bool isValidItem = isItemOwnerOrReferee && !Item.Expired;

                AllowDeletion = isValidItem
                                && (Item.ID != multiplayerClient.Room.Settings.PlaylistItemId // This is an optimisation for the following check.
                                    || multiplayerClient.Room.Playlist.Count(i => !i.Expired) > 1);

                // No editing from the tournament panel.
                AllowEditing = false;
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                if (multiplayerClient.IsNotNull())
                    multiplayerClient.RoomUpdated -= onRoomUpdated;
            }
        }
    }
}
