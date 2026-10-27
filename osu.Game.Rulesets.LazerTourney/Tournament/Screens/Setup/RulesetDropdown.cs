// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Setup
{
    /// <summary>
    /// Ruleset selector for the setup screen. The ruleset only takes effect after a restart,
    /// so selections are staged locally: confirming saves the bracket and exits the plugin,
    /// cancelling (or dismissing the dialog) reverts the dropdown to the previous value.
    /// </summary>
    internal partial class RulesetDropdown : CompositeDrawable
    {
        private readonly Bindable<RulesetInfo?> selectedRuleset = new Bindable<RulesetInfo?>();

        private bool suppressSelectionChange;

        [Resolved]
        private LadderInfo ladderInfo { get; set; } = null!;

        [Resolved]
        private IDialogOverlay dialogOverlay { get; set; } = null!;

        [Resolved]
        private TournamentGameBase? tournamentGame { get; set; }

        [Resolved]
        private TournamentSceneManager? sceneManager { get; set; }

        public RulesetDropdown(IEnumerable<RulesetInfo?> items)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new SettingsItemV2(new FormDropdown<RulesetInfo?>
            {
                Caption = "Ruleset",
                HintText = "Decides what stats are displayed and which ranks are retrieved for players. Only rulesets available online (usable in multiplayer rooms) are listed. Changing this requires a restart to reload data for an existing bracket.",
                Items = items,
                Current = selectedRuleset,
            })
            {
                ShowRevertToDefaultButton = false,
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            selectedRuleset.Default = ladderInfo.Ruleset.Value;
            selectedRuleset.Value = ladderInfo.Ruleset.Value;

            selectedRuleset.BindValueChanged(v =>
            {
                if (suppressSelectionChange)
                {
                    suppressSelectionChange = false;
                    return;
                }

                if (v.NewValue == v.OldValue)
                    return;

                RulesetInfo? requested = v.NewValue;

                dialogOverlay.Push(new RulesetRestartDialog(
                    requested?.Name ?? "another ruleset",
                    () =>
                    {
                        ladderInfo.Ruleset.Value = requested;
                        tournamentGame?.SaveChanges();
                        sceneManager?.ExitTournament();
                    },
                    () =>
                    {
                        suppressSelectionChange = true;
                        selectedRuleset.Value = v.OldValue;
                    }));
            });
        }
    }
}
