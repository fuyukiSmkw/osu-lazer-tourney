// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Setup
{
    /// <summary>
    /// Confirms changing the tournament ruleset, which only takes effect after a restart
    /// (ruleset settings and mappool data are loaded once at startup).
    /// </summary>
    /// <remarks>
    /// Dismissing a <see cref="PopupDialog"/> (clicking outside, escape, ...) presses its last
    /// button, so "Cancel" has to stay last here. Do not set <c>HideDialogOnAction = false</c> or
    /// call <c>Hide()</c> from a button action: the resulting pop-out would press the last button a
    /// second time (and "Save and restart" would run the cancel path as well).
    /// </remarks>
    internal partial class RulesetRestartDialog : PopupDialog
    {
        public RulesetRestartDialog(string rulesetName, Action saveAndRestart, Action cancelled)
        {
            HeaderText = "Restart required";
            BodyText = $@"Switching to {rulesetName} requires reloading the bracket, which only happens when the plugin restarts. Save now and close lazer!tourney? Reopen it to apply the new ruleset.";
            Icon = FontAwesome.Solid.ExclamationTriangle;

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = "Save and restart",
                    Action = saveAndRestart,
                },
                new PopupDialogCancelButton
                {
                    Text = "Cancel",
                    Action = cancelled,
                },
            };
        }
    }
}
