// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Input.Events;
using osu.Game.Graphics.Containers;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// A scroll container that never scrolls itself: wheel and drag-scroll gestures are
    /// declined so they bubble to an outer scroll container.
    /// Used by the control-panel lists, which are exactly content-sized (nothing to scroll)
    /// but would otherwise swallow gestures meant for the panel (framework
    /// <c>ScrollContainer.OnScroll</c> claims the event whenever it has children,
    /// even with zero scrollable extent). Clicks are unaffected.
    /// </summary>
    public partial class NonScrollingOsuScrollContainer : OsuScrollContainer
    {
        protected override bool OnScroll(ScrollEvent e) => false;

        protected override bool OnDragStart(DragStartEvent e) => false;
    }
}
