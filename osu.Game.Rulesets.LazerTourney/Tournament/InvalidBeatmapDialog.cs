// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Rulesets.LazerTourney.Tournament
{
    /// <summary>
    /// Shown during startup for every mappool entry the selected ruleset cannot process.
    /// Deleting clears the beatmap data but keeps the slot in the round; the only alternative
    /// is leaving the plugin (because continuing would crash the tournament UI).
    /// </summary>
    /// <remarks>
    /// Dismissing a <see cref="PopupDialog"/> (clicking outside, escape, ...) presses its last
    /// button, so the safe "don't delete and exit" action has to stay last here.
    /// Do not set <c>HideDialogOnAction = false</c> or call <c>Hide()</c> from a
    /// button action: the resulting pop-out would press the last button a second time.
    /// </remarks>
    internal partial class InvalidBeatmapDialog : PopupDialog
    {
        public InvalidBeatmapDialog(string beatmap, string roundName, string reason, Action deleteAndContinue, Action exitWithoutDeleting)
        {
            HeaderText = "Invalid beatmap";

            string roundContext = string.IsNullOrEmpty(roundName) ? string.Empty : $@" in round ""{roundName}""";

            BodyText = $@"{beatmap}{roundContext} is invalid for the current ruleset ({reason}).
Delete its beatmap data and continue? The slot stays in the round, so you can fill it again later.";
            Icon = FontAwesome.Solid.ExclamationTriangle;

            // The dangerous button is intentionally first: `PopupDialog` auto-presses the last
            // button when the dialog is dismissed, which must be the exit (cancel) action.
            Buttons = new PopupDialogButton[]
            {
                new PopupDialogDangerousButton
                {
                    Text = "Delete and continue",
                    Action = deleteAndContinue,
                },
                new PopupDialogCancelButton
                {
                    Text = "Don't delete and exit",
                    Action = exitWithoutDeleting,
                },
            };
        }
    }
}
