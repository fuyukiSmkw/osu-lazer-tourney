// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Containers;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Editors.Components
{
    /// <summary>
    /// A rearrangeable list which grows with its items instead of clipping them, so it can live inside a
    /// scrollable editor page without nesting scroll areas of the same direction.
    /// </summary>
    /// <remarks>
    /// The surrounding page remains the only scroll surface (wheel input is passed through to it) and is scrolled
    /// automatically while an item is dragged towards its top/bottom edge, mirroring
    /// <see cref="RearrangeableListContainer{TModel}"/>'s own drag-to-scroll behaviour.
    /// </remarks>
    public abstract partial class ExpandedRearrangeableListContainer<TModel> : OsuRearrangeableListContainer<TModel>
        where TModel : notnull
    {
        private const float scroll_edge = 60;
        private const float scroll_exp_base = 1.05f;
        private const float max_scroll_power = 50;

        private ScrollContainer<Drawable>? pageScroll;

        protected ExpandedRearrangeableListContainer()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        protected override ScrollContainer<Drawable> CreateScrollContainer() => new ExpandedScrollContainer();

        protected override FillFlowContainer<RearrangeableListItem<TModel>> CreateListFillFlowContainer() => new FillFlowContainer<RearrangeableListItem<TModel>>
        {
            LayoutDuration = 200,
            LayoutEasing = Easing.OutQuint,
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Skip our own expanding viewport (and any expanded list nested in ours) to find the page that actually scrolls.
            var scroll = this.FindClosestParent<ScrollContainer<Drawable>>();

            while (scroll is ExpandedScrollContainer)
                scroll = scroll.FindClosestParent<ScrollContainer<Drawable>>();

            pageScroll = scroll;
        }

        protected override void Update()
        {
            base.Update();

            if (!DragActive.Value || pageScroll == null)
                return;

            var inputManager = GetContainingInputManager();

            if (inputManager == null)
                return;

            float localY = pageScroll.ToLocalSpace(inputManager.CurrentState.Mouse.Position).Y;
            float overflow;

            if (localY < scroll_edge)
                overflow = localY - scroll_edge;
            else if (localY > pageScroll.DrawHeight - scroll_edge)
                overflow = localY - (pageScroll.DrawHeight - scroll_edge);
            else
                return;

            float power = Math.Min(max_scroll_power, Math.Abs(overflow));
            pageScroll.ScrollBy(MathF.Sign(overflow) * (float)(MathF.Pow(scroll_exp_base, power) * Clock.ElapsedFrameTime * 0.1));
        }
    }

    /// <summary>
    /// A scroll container which grows to fit its content instead of clipping it.
    /// </summary>
    /// <remarks>
    /// <see cref="RearrangeableListContainer{TModel}"/> sizes its scroll container relatively on both axes, which
    /// would clip the items (and trap wheel input); this one sizes to its content and leaves scrolling to the page.
    /// </remarks>
    public partial class ExpandedScrollContainer : OsuScrollContainer
    {
        public ExpandedScrollContainer()
        {
            ScrollbarVisible = false;
            Masking = false;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // The list container forces relative sizing on both axes; grow with the items instead.
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        // Wheel input is left to the page.
        protected override bool OnScroll(ScrollEvent e) => false;

        // Drag-scrolling is left to the page as well. This also covers touch scrolling, which the framework maps to
        // mouse drags: without this, dragging inside the list scrolls the (non-scrollable) inner viewport and the
        // page never moves.
        protected override bool OnDragStart(DragStartEvent e) => false;
    }
}
