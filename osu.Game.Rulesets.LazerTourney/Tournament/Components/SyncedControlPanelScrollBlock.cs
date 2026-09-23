// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens.Edit.Components;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// Scrollable block below the existing control-panel content.
    /// One instance is hosted in each of <c>MapPoolScreen</c> and <c>GameplayScreen</c>;
    /// both show the same live room data (participants + beatmap queue) and stay in sync
    /// because they bind to the shared <see cref="osu.Game.Online.Multiplayer.MultiplayerClient"/> state.
    /// </summary>
    /// <remarks>
    /// The lists mirror the corresponding columns of <c>RoomScreen</c>:
    /// <see cref="TournamentParticipantsList"/> and <see cref="TournamentQueueList"/>
    /// (download + delete buttons, no edit, no add button).
    /// Each lives in an <see cref="EditorSidebarSection"/> (the sidebar sections of the editor).
    /// Both lists size to their content; the outer control-panel scroll container handles overflow.
    /// </remarks>
    public partial class SyncedControlPanelScrollBlock : CompositeDrawable
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5f),
                Children = new Drawable[]
                {
                    new EditorSidebarSection("Players")
                    {
                        Children = new Drawable[]
                        {
                            new TournamentParticipantsList
                            {
                                RelativeSizeAxes = Axes.X,
                            },
                        },
                    },
                    new EditorSidebarSection("Queue")
                    {
                        Children = new Drawable[]
                        {
                            new TournamentQueueList
                            {
                                RelativeSizeAxes = Axes.X,
                            },
                        },
                    },
                },
            };
        }
    }
}
