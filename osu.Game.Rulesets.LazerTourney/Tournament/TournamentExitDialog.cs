// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Rulesets.LazerTourney.Tournament
{
    /// <summary>
    /// Confirmation shown when exiting the tournament client with unsaved bracket changes.
    /// Uses the same save / discard / cancel layout as the beatmap editor's save prompt.
    /// </summary>
    internal partial class TournamentExitDialog : PopupDialog
    {
        public TournamentExitDialog(Action saveAndExit, Action exitWithoutSaving)
        {
            HeaderText = "Unsaved changes";
            BodyText = "Your bracket has unsaved changes. Save before exiting?";
            Icon = FontAwesome.Regular.Save;

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = "Save and exit",
                    Action = saveAndExit,
                },
                new PopupDialogDangerousButton
                {
                    Text = "Exit without saving",
                    Action = exitWithoutSaving,
                },
                new PopupDialogCancelButton
                {
                    Text = "Cancel",
                },
            };
        }
    }
}
