// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Online.Multiplayer;
using osu.Game.Screens.OnlinePlay.Multiplayer.Participants;
using osu.Game.Users.Drawables;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// The room's participant list for the synced control-panel blocks.
    /// Same as <c>RoomScreen</c>'s <see cref="ParticipantsList"/>, except the height
    /// follows the content (one row per participant) instead of filling leftover space,
    /// so the outer control-panel scroll container handles overflow.
    /// Wheel/drag gestures pass through to the panel (see <see cref="NonScrollingOsuScrollContainer"/>).
    /// </summary>
    public partial class TournamentParticipantsList : VirtualisedListContainer<Slot, TournamentParticipantPanel>
    {
        private BindableList<Slot> slots => RowData;

        private Slot? currentHost;

        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        public TournamentParticipantsList()
            : base(ParticipantPanel.HEIGHT + 1, initialPoolSize: 20)
        {
        }

        // Mirrors the base factory, but non-scrolling: the list is exactly content-sized.
        protected override ScrollContainer<Drawable> CreateScrollContainer() => new NonScrollingOsuScrollContainer
        {
            ScrollbarVisible = false,
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            client.RoomUpdated += onRoomUpdated;
            updateState();

            RowData.BindCollectionChanged((_, _) => updateHeight(), true);
        }

        private void updateHeight() => Height = RowData.Count * (ParticipantPanel.HEIGHT + 1);

        private void onRoomUpdated() => Scheduler.AddOnce(updateState);

        private void updateState()
        {
            if (client.Room == null)
            {
                slots.Clear();
                return;
            }

            // pathway for handling rooms with participant count limit and slots
            if (client.Room.MatchState is StandardMatchRoomState standardMatchRoomState && standardMatchRoomState.Slots is int?[] slotUserIds)
            {
                // reset host tracking - in slots mode the host's position is decided solely by their slot
                // the reset has the side benefit of getting the host pinned to top of list again if slots are turned off (see logic lower down).
                currentHost = null;

                if (slots.Count > slotUserIds.Length)
                    slots.RemoveRange(slotUserIds.Length, slots.Count - slotUserIds.Length);

                for (byte i = 0; i < slotUserIds.Length; ++i)
                {
                    var user = slotUserIds[i] != null ? client.Room.Users.SingleOrDefault(u => u.UserID == slotUserIds[i]) : null;
                    var participant = user == null ? Slot.Empty(i) : Slot.FromUser(client.Room.Users.Single(u => u.UserID == slotUserIds[i]));

                    if (i >= slots.Count)
                        slots.Add(participant);
                    if (!participant.Equals(slots[i]))
                        slots[i] = participant;
                }

                return;
            }

            // Remove panels for empty slots & users no longer in the room.
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                var slot = slots[i];

                // Note that we *must* use reference equality here, as this call is scheduled and a user may have left and joined since it was last run.
                if (slot.IsEmpty || client.Room.Users.All(u => !ReferenceEquals(slot.User, u)))
                    slots.RemoveAt(i);
            }

            // This assertion guarantees that all subsequent accesses to `User` of any `Slot` is safe.
            // Unfortunately static analysis is not smart enough to pick this up, so there'll be a lot of `.AsNonNull()` lower down.
            Debug.Assert(slots.All(p => !p.IsEmpty));

            // Add panels for all users new to the room.
            foreach (var user in client.Room.Users.Except(slots.Select(u => u.User.AsNonNull())))
                slots.Add(Slot.FromUser(user));

            if (currentHost == null || !currentHost.User.AsNonNull().Equals(client.Room.Host))
            {
                currentHost = null;

                // Change position of new host to display above all participants.
                if (client.Room.Host != null)
                {
                    currentHost = slots.SingleOrDefault(u => u.User.AsNonNull().Equals(client.Room.Host));
                    int currentHostIndex = currentHost == null ? -1 : slots.IndexOf(currentHost);

                    if (currentHostIndex > 0)
                    {
                        slots.Move(currentHostIndex, 0);
                        currentHost = slots[0];
                    }
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (client.IsNotNull())
                client.RoomUpdated -= onRoomUpdated;
        }
    }

    /// <summary>
    /// <see cref="ParticipantPanel"/> but hide avatar and team flag.
    /// </summary>
    public partial class TournamentParticipantPanel : ParticipantPanel
    {
        private bool teamFlagHidden;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            this.ChildrenOfType<UpdateableAvatar>()?.FirstOrDefault()?.Hide();
            hideTeamFlag();
        }

        protected override void Update()
        {
            base.Update();

            // An empty slot only creates the team flag once a user is assigned to it,
            // so the container may not exist yet at load time.
            if (!teamFlagHidden)
                hideTeamFlag();
        }

        private void hideTeamFlag()
        {
            // Hiding the containing drawable (rather than the flag itself) keeps the hidden state
            // across the base class replacing the flag when the assigned user changes.
            var teamFlag = this.ChildrenOfType<UpdateableTeamFlag>().FirstOrDefault();

            if (teamFlag?.Parent == null)
                return;

            teamFlag.Parent.Hide();
            teamFlagHidden = true;
        }
    }
}
