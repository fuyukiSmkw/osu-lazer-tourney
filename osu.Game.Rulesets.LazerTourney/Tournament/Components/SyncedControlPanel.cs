// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// <see cref="ControlPanel"/> used by the stream screens (Countdown, Schedule, Bracket,
    /// TeamIntro, Seeding, MapPool, Gameplay, Win).
    /// Wires the two synced blocks (scrollable room lists + chat bottom bar) so all screens
    /// share the same structure and live room data.
    /// Existing per-screen controls are still provided via <c>Children</c> (top section).
    /// </summary>
    public partial class SyncedControlPanel : ControlPanel
    {
        /// <param name="showChatHistory">Whether the bottom bar shows a small read-only
        /// message list above the send box. MapPool/Gameplay leave it off because the
        /// full-width stream chat is visible on those screens.</param>
        public SyncedControlPanel(bool showChatHistory = false)
        {
            ShowScrollSeparators = true;
            ScrollContent.Add(new SyncedControlPanelScrollBlock());
            BottomBarContent.Add(new SyncedControlPanelBottomBar(showChatHistory));
        }

        public SyncedControlPanel(BindableBool showChatHistory)
        {
            ShowScrollSeparators = true;
            ScrollContent.Add(new SyncedControlPanelScrollBlock());
            BottomBarContent.Add(new SyncedControlPanelBottomBar(showChatHistory));
        }
    }
}
