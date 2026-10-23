// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    public partial class DrawableTeamHeader : TournamentSpriteTextWithBackground
    {
        private readonly TeamColour teamColour;
        private Bindable<string>? configuredColour;

        [Resolved(canBeNull: true)]
        private LadderInfo? ladderInfo { get; set; }

        public DrawableTeamHeader(TeamColour colour)
        {
            teamColour = colour;
            Background.Colour = TournamentColours.GetTeamColour(colour);

            Text.Colour = TournamentColours.TEXT_COLOUR;
            Text.Text = $"Team {colour}".ToUpperInvariant();
            Text.Scale = new Vector2(0.6f);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (ladderInfo == null)
                return;

            configuredColour = (teamColour == TeamColour.Red ? ladderInfo.TeamColourRed : ladderInfo.TeamColourBlue).GetBoundCopy();
            configuredColour.BindValueChanged(_ => updateTeamColour(), true);
        }

        private void updateTeamColour()
            => Background.Colour = TournamentColours.GetTeamColour(teamColour, ladderInfo);
    }
}
