// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Screens.OnlinePlay.Multiplayer.Participants;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// The room's participant list for the synced control-panel blocks.
    /// Same as <c>RoomScreen</c>'s <see cref="ParticipantsList"/>, except the height
    /// follows the content (one row per participant) instead of filling leftover space,
    /// so the outer control-panel scroll container handles overflow.
    /// </summary>
    public partial class TournamentParticipantsList : ParticipantsList
    {
        protected override void LoadComplete()
        {
            base.LoadComplete();

            RowData.BindCollectionChanged((_, _) => updateHeight(), true);
        }

        private void updateHeight() => Height = RowData.Count * (ParticipantPanel.HEIGHT + 1);
    }
}
