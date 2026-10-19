// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.Multiplayer;
using osu.Game.Rulesets.LazerTourney.Tournament.Components;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Online;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay.Components
{
    /// <summary>
    /// A button that opens a popover to select and reset a specific player's spectate state.
    /// </summary>
    public partial class ResetPlayerButton : TourneyButton, IHasPopover
    {
        private readonly SpectateSession spectateSession;
        private readonly TournamentOnlineState onlineState;

        public ResetPlayerButton(SpectateSession spectateSession, TournamentOnlineState onlineState)
        {
            this.spectateSession = spectateSession;
            this.onlineState = onlineState;

            RelativeSizeAxes = Axes.X;
            Text = "Reset player...";
            Margin = new MarginPadding(3);
            Action = this.ShowPopover;
        }

        public Popover GetPopover()
        {
            var redCells = spectateSession.RedCells;
            var blueCells = spectateSession.BlueCells;

            if ((redCells == null || redCells.Count == 0) && (blueCells == null || blueCells.Count == 0))
                return new OsuPopover { Child = new OsuSpriteText { Text = "No cells available", Font = OsuFont.GetFont(size: 14), Margin = new MarginPadding(20) } };

            var flow = new FillFlowContainer
            {
                Width = 240,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(2),
            };

            // Build cell entries in visual order: red side first (top to bottom), then blue side (top to bottom)
            var cellEntries = new List<(SpectateCell Cell, string PositionLabel, Color4 TeamColour)>();

            if (redCells != null)
            {
                for (int i = 0; i < redCells.Count; i++)
                {
                    cellEntries.Add((redCells[i], $"Red {i + 1}", TournamentColours.GetTeamColour(TeamColour.Red)));
                }
            }

            if (blueCells != null)
            {
                for (int i = 0; i < blueCells.Count; i++)
                {
                    cellEntries.Add((blueCells[i], $"Blue {i + 1}", TournamentColours.GetTeamColour(TeamColour.Blue)));
                }
            }

            var room = onlineState.Client?.Room;
            var clocks = spectateSession.ClocksByUser;

            foreach (var entry in cellEntries)
            {
                var cell = entry.Cell;
                var positionLabel = entry.PositionLabel;
                var teamColour = entry.TeamColour;
                var userId = cell.AssignedUserId;

                string buttonText;
                bool enabled = false;

                if (userId.HasValue)
                {
                    var user = room?.Users.FirstOrDefault(u => u.UserID == userId.Value);
                    var username = user?.User?.Username ?? $"User {userId.Value}";
                    buttonText = $"{positionLabel}: {username}";

                    // Enable only if the user is actively playing (clock running, not catching up, not waiting)
                    // and the cell has gameplay attached.
                    if (clocks.TryGetValue(userId.Value, out var clock) &&
                        clock.IsRunning && !clock.IsCatchingUp && !clock.WaitingOnFrames &&
                        cell.HasPlay &&
                        room?.Users.FirstOrDefault(u => u.UserID == userId.Value)?.State == MultiplayerUserState.Playing)
                    {
                        enabled = true;
                    }
                }
                else
                {
                    buttonText = $"{positionLabel}: (empty)";
                }

                var button = new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Text = buttonText,
                    BackgroundColour = enabled ? teamColour : Color4.Gray,
                    Height = 36,
                };

                if (enabled)
                {
                    var capturedUserId = userId.Value;
                    button.Action = () =>
                    {
                        spectateSession.ResetPlayer(capturedUserId);
                        this.HidePopover();
                    };
                }

                flow.Add(button);
            }

            return new OsuPopover { Child = flow };
        }
    }
}
