// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osuTK.Input;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// Chat send box with Tab username completion against the current room's users.
    /// Usernames have their spaces treated as underscores for matching and completion.
    /// A leading slash completes chat commands (official and tournament custom ones) instead.
    /// </summary>
    public partial class TournamentChatTextBox : StandAloneChatDisplay.ChatTextBox
    {
        [Resolved(canBeNull: true)]
        private MultiplayerClient? multiplayerClient { get; set; }

        private static readonly FieldInfo? selection_start_field =
            typeof(TextBox).GetField("selectionStart", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? selection_end_field =
            typeof(TextBox).GetField("selectionEnd", BindingFlags.Instance | BindingFlags.NonPublic);

        // Pending multi-candidate completion: candidates stay fixed while cycling,
        // the current one is selected from pendingStart on.
        private List<string>? pendingCandidates;
        private int pendingIndex;
        private int pendingStart;

        private int selectionStart => (int?)selection_start_field?.GetValue(this) ?? 0;
        private int selectionEnd => (int?)selection_end_field?.GetValue(this) ?? 0;

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.Tab)
            {
                handleTab();
                return true;
            }

            if (pendingCandidates != null)
            {
                // Enter confirms the pending name instead of sending; Right confirms in place.
                // Anything stale falls through to the native behaviour below.
                if ((e.Key == Key.Enter || e.Key == Key.KeypadEnter || e.Key == Key.Right) && validatePending())
                {
                    confirmPending();
                    return true;
                }

                bool result = base.OnKeyDown(e);
                reconcilePending();
                return result;
            }

            return base.OnKeyDown(e);
        }

        protected override void OnFocusLost(FocusLostEvent e)
        {
            pendingCandidates = null;
            base.OnFocusLost(e);
        }

        private void handleTab()
        {
            if (validatePending())
            {
                // Cycle to the next candidate. The set stays fixed while cycling.
                string old = pendingCandidates![pendingIndex];
                pendingIndex = (pendingIndex + 1) % pendingCandidates.Count;
                replaceRange(pendingStart, pendingStart + old.Length, pendingCandidates[pendingIndex], true);
                return;
            }

            var names = roomUsernames();
            int start = Math.Min(selectionStart, selectionEnd);
            int end = Math.Max(selectionStart, selectionEnd);

            if (start == end && Text.Length > 0 && Text[0] == '/')
            {
                // Command completion: from the start of the string to the caret.
                // Falls through to username completion when no command matches.
                string head = Text.Substring(1, end - 1);

                if (!head.Contains(' '))
                {
                    // A single token right after '/' must be a command name; anything else is meaningless.
                    var commandMatches = SyncedControlPanelBottomBar.COMMANDS.Where(c => c.StartsWith(head, StringComparison.OrdinalIgnoreCase)).ToList();

                    if (commandMatches.Count == 1)
                    {
                        replaceRange(1, end, commandMatches[0] + " ", false);
                        return;
                    }

                    if (commandMatches.Count > 1)
                    {
                        beginPending(commandMatches, 1, end, commandMatches[0]);
                        return;
                    }

                    NotifyInputError();
                    return;
                }
                // A space means the command may take a username argument: fall through to username completion.
            }

            if (start != end)
            {
                // Case 3: complete from the selected text (spaces count as underscores).
                string selected = SelectedText.Replace(' ', '_');

                if (selected.Length == 0 || !selected.All(isNameChar))
                {
                    NotifyInputError();
                    return;
                }

                completeFromPrefix(names, selected, start, end);
                return;
            }

            int caret = end;
            string text = Text;
            int wordStart = caret;

            while (wordStart > 0 && isNameChar(text[wordStart - 1]))
                wordStart--;

            if (wordStart == caret)
            {
                // Case 2: no word before the caret, complete from every room username.
                if (names.Count == 0)
                {
                    NotifyInputError();
                    return;
                }

                beginPending(names, caret, caret, names[0]);
                return;
            }

            // Case 1: complete from the word before the caret.
            completeFromPrefix(names, text.Substring(wordStart, caret - wordStart), wordStart, caret);
        }

        private void completeFromPrefix(List<string> names, string prefix, int start, int end)
        {
            var matches = names.Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matches.Count == 0)
            {
                NotifyInputError();
                return;
            }

            if (matches.Count == 1)
            {
                replaceRange(start, end, matches[0] + " ", false);
                return;
            }

            beginPending(matches, start, end, matches[0]);
        }

        private void beginPending(List<string> candidates, int start, int end, string first)
        {
            pendingCandidates = candidates;
            pendingIndex = 0;
            pendingStart = start;
            replaceRange(start, end, first, true);
        }

        private void confirmPending()
        {
            string name = pendingCandidates![pendingIndex];
            replaceRange(pendingStart, pendingStart + name.Length, name + " ", false);
            pendingCandidates = null;
        }

        /// <summary>
        /// Drops the pending state unless the expected completion is still selected at its place.
        /// </summary>
        private void reconcilePending()
        {
            if (pendingCandidates == null)
                return;

            if (pendingIndex < 0 || pendingIndex >= pendingCandidates.Count || SelectedText != pendingCandidates[pendingIndex])
                pendingCandidates = null;
        }

        private bool validatePending()
        {
            if (pendingCandidates == null || pendingCandidates.Count == 0
                || pendingIndex < 0 || pendingIndex >= pendingCandidates.Count
                || pendingStart < 0 || pendingStart > Text.Length)
            {
                pendingCandidates = null;
                return false;
            }

            string name = pendingCandidates[pendingIndex];

            if (pendingStart + name.Length > Text.Length
                || Text.Substring(pendingStart, name.Length) != name
                || SelectedText != name)
            {
                pendingCandidates = null;
                return false;
            }

            return true;
        }

        private void replaceRange(int start, int end, string replacement, bool select)
        {
            string text = Text;
            start = Math.Clamp(start, 0, text.Length);
            end = Math.Clamp(end, 0, text.Length);

            Text = text.Substring(0, start) + replacement + text.Substring(end);

            if (select)
                selectRange(start, start + replacement.Length);
            else
                moveCaretTo(start + replacement.Length);
        }

        private void selectRange(int a, int b)
        {
            // Setting Text collapses the selection to zero; re-anchor through SelectAll
            // so only known lengths are needed (the caret itself is not readable).
            if (!SelectAll())
                return;

            MoveCursorBy(a - Text.Length);
            ExpandSelectionBy(b - a);
        }

        private void moveCaretTo(int position)
        {
            if (!SelectAll())
                return;

            MoveCursorBy(position - Text.Length);
        }

        private List<string> roomUsernames()
        {
            var room = multiplayerClient?.Room;

            if (room == null)
                return new List<string>();

            return room.Users
                       .Select(u => u.User?.Username)
                       .OfType<string>()
                       .Select(n => n.Replace(' ', '_'))
                       .Distinct()
                       .OrderBy(n => n, StringComparer.Ordinal)
                       .ToList();
        }

        private static bool isNameChar(char c)
            => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9'
               || c == '-' || c == '[' || c == ']' || c == '_';
    }
}
