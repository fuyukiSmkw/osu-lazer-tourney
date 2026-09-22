// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Scoring;
using osu.Game.Screens.OnlinePlay.Multiplayer.Spectate;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay.Components
{
    /// <summary>
    /// Tournament spectator player. Identical to <see cref="MultiSpectatorPlayer"/> except the
    /// official white username label is removed: tournament cells show their own team-coloured
    /// username label instead.
    /// </summary>
    public partial class TournamentSpectatorPlayer : MultiSpectatorPlayer
    {
        public TournamentSpectatorPlayer(Score score, SpectatorPlayerClock clock)
            : base(score, clock)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // The base class adds a white username label at the top centre of the gameplay clock container.
            // Remove it (rather than hiding) so no layout space is reserved for it.
            foreach (var label in GameplayClockContainer.ChildrenOfType<OsuTextFlowContainer>().ToList())
            {
                if (label.Anchor == Anchor.TopCentre && label.Origin == Anchor.TopCentre)
                    label.RemoveAndDisposeImmediately();
            }
        }
    }
}
