// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// <see cref="ControlPanel"/> used by <c>MapPoolScreen</c> and <c>GameplayScreen</c>.
    /// Wires the two synced blocks (scrollable room lists + chat bottom bar) so both screens
    /// share the same structure and live room data.
    /// Existing per-screen controls are still provided via <c>Children</c> (top section).
    /// </summary>
    public partial class SyncedControlPanel : ControlPanel
    {
        public SyncedControlPanel()
        {
            ScrollContent.Add(new SyncedControlPanelScrollBlock());
            BottomBarContent.Add(new SyncedControlPanelBottomBar());
        }
    }
}
