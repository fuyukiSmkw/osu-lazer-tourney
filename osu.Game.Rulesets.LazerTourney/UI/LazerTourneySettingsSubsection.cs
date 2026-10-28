// Copyright (c) 2026 fuyukiS <fuyukiS@outlook.jp>. Licensed under the MIT License.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.LazerTourney.Startup;
using osu.Game.Rulesets.LazerTourney.Tournament.Components;
using osu.Game.Screens.Menu;

#nullable enable

namespace osu.Game.Rulesets.LazerTourney.UI;

public partial class LazerTourneySettingsSubsection : RulesetSettingsSubsection
{
    [Resolved]
    private OsuGame game { get; set; } = null!;

    [Resolved(canBeNull: true)]
    private INotificationOverlay? notifications { get; set; }

    public LazerTourneySettingsSubsection(Ruleset ruleset)
        : base(ruleset)
    {
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        Children = new Drawable[]
        {
            new SettingsButton
            {
                Text = "Open tournament client",
                Action = () =>
                {
                    if (game.ScreenStack.CurrentScreen is not MainMenu)
                    {
                        notifications?.Post(new TournamentNotification
                        {
                            Text = "The tournament client can only be opened from the main menu.",
                            Transient = true,
                        });
                        return;
                    }

                    TournamentLauncher.TryLaunch(game);
                }
            },
            new OsuTextFlowContainer(t =>
            {
                t.Font = t.Font.With(size: 12);
                t.Colour = OsuColour.Gray(0.6f);
            })
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Margin = new MarginPadding
                {
                    Horizontal = SettingsPanel.CONTENT_MARGINS,
                    Top = 4,
                },
                Text = "Starting the tournament client disables osu!'s crash reporting for the rest of this session.\nRestart osu! to re-enable it.",
            },
        };
    }
}
