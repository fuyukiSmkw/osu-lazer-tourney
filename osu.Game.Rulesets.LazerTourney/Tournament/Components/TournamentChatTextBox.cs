// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osu.Game.Rulesets.LazerTourney.Tournament.Components.ChatCommands;
using osu.Game.Utils;
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

        // HistoryTextBox keeps its commit history in a private per-instance queue,
        // so every tournament send box uses this shared one instead. Capacity matches
        // the HistoryTextBox default.
        private static readonly LimitedCapacityQueue<string> shared_history = new(100);

        private int historyIndex;
        private string originalMessage = string.Empty;

        public TournamentChatTextBox()
        {
            historyIndex = shared_history.Count;

            // Mirrors HistoryTextBox: editing a recalled entry drops back to the draft.
            Current.ValueChanged += text =>
            {
                if (historyIndex != shared_history.Count && text.NewValue != shared_history[historyIndex])
                    historyIndex = shared_history.Count;
            };
        }

        // Pending multi-candidate completion: candidates stay fixed while cycling,
        // the current one is kept selected.
        private List<string>? pendingCandidates;
        private int pendingIndex;

        /// <summary>
        /// Keep draft text on back (escape), matching regular text boxes.
        /// The default <see cref="FocusedTextBox"/> behaviour clears the text first
        /// and only releases focus on the second press.
        /// </summary>
        protected override bool ClearTextOnBackKey => false;

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            // FocusedTextBox disables the framework-level escape handling and routes
            // escape through GlobalAction.Back instead, so release focus here to match
            // TextBox. Consume the event so the back press does not leak to screens.
            if (!e.Repeat && HasFocus && e.Action == GlobalAction.Back)
            {
                pendingCandidates = null;
                KillFocus();
                return true;
            }

            return base.OnPressed(e);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.Tab)
            {
                handleTab();
                return true;
            }

            // Skip base(ChatTextBox).OnKeyDown because it returns false for up and down
            if (HasFocus && (e.Key == Key.Up || e.Key == Key.Down))
                return handleHistoryKey(e);

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

        protected override void Commit()
        {
            if (!string.IsNullOrEmpty(Text))
                shared_history.Enqueue(Text);

            historyIndex = shared_history.Count;

            base.Commit();
        }

        /// <summary>
        /// Mirrors <see cref="HistoryTextBox.OnKeyDown"/> against <see cref="shared_history"/>.
        /// </summary>
        private bool handleHistoryKey(KeyDownEvent e)
        {
            if (e.ControlPressed || e.AltPressed || e.SuperPressed || e.ShiftPressed)
                return false;

            switch (e.Key)
            {
                case Key.Up:
                    if (historyIndex == 0)
                        return true;

                    if (historyIndex == shared_history.Count)
                        originalMessage = Text;

                    Text = shared_history[--historyIndex];

                    return true;

                case Key.Down:
                    if (historyIndex == shared_history.Count)
                        return true;

                    if (historyIndex == shared_history.Count - 1)
                    {
                        historyIndex = shared_history.Count;
                        Text = originalMessage;
                        return true;
                    }

                    Text = shared_history[++historyIndex];

                    return true;
            }

            return false;
        }

        /// <summary>
        /// Focuses this box for slash-command entry (global <c>/</c> shortcut).
        /// Existing text is left untouched with the caret moved to the end;
        /// an empty box starts with a single <c>/</c>.
        /// </summary>
        public void FocusForSlashEntry()
        {
            pendingCandidates = null;

            if (string.IsNullOrEmpty(Text))
                Text = "/";

            TakeFocus();
            // TakeFocus is scheduled, so place the caret once focus has landed.
            Scheduler.AddOnce(() => moveCaretTo(Text.Length));
        }

        private void handleTab()
        {
            if (ImeCompositionActive)
            {
                // Moving the selection is not safe during composition (the framework
                // ignores it while textInput.ImeActive), so the caret probe would lie.
                NotifyInputError();
                return;
            }

            if (validatePending())
            {
                // Cycle to the next candidate. The set stays fixed while cycling.
                pendingIndex = (pendingIndex + 1) % pendingCandidates!.Count;
                replaceRange(null, null, pendingCandidates[pendingIndex], true);
                return;
            }

            var names = roomUsernames();
            string selected = SelectedText;

            if (selected.Length > 0)
            {
                // Case 3: complete from the selected text (spaces count as underscores).
                selected = selected.Replace(' ', '_');

                if (!selected.All(isNameChar))
                {
                    NotifyInputError();
                    return;
                }

                completeFromPrefix(names, selected, null, null);
                return;
            }

            int caret = getCaretPosition();

            if (Text.Length > 0 && Text[0] == '/' && caret > 0)
            {
                // Command completion: from the start of the string to the caret.
                // Falls through to username completion when no command matches.
                string head = Text.Substring(1, caret - 1);

                if (!head.Contains(' '))
                {
                    // A single token right after '/' must be a command name; anything else is meaningless.
                    // Aliases come from the mapping, never from COMMANDS itself.
                    var commandMatches = TournamentChatCommandHandler.COMMANDS
                                                               .Concat(TournamentChatCommandHandler.COMMAND_ALIASES.Keys)
                                                               .Where(c => c.StartsWith(head, StringComparison.OrdinalIgnoreCase)).ToList();

                    if (commandMatches.Count == 1)
                    {
                        replaceRange(1, caret, commandMatches[0] + " ", false);
                        return;
                    }

                    if (commandMatches.Count > 1)
                    {
                        beginPending(commandMatches, commandMatches[0], 1, caret);
                        return;
                    }

                    NotifyInputError();
                    return;
                }
                // A space means the command may take a username argument: fall through to username completion.
            }

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

                beginPending(names, names[0], caret, caret);
                return;
            }

            // Case 1: complete from the word before the caret.
            completeFromPrefix(names, text.Substring(wordStart, caret - wordStart), wordStart, caret);
        }

        private void completeFromPrefix(List<string> names, string prefix, int? start, int? end)
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

            beginPending(matches, matches[0], start, end);
        }

        private void beginPending(List<string> candidates, string first, int? start, int? end)
        {
            pendingCandidates = candidates;
            pendingIndex = 0;
            replaceRange(start, end, first, true);
        }

        private void confirmPending()
        {
            string name = pendingCandidates![pendingIndex];
            replaceRange(null, null, name + " ", false);
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
                || pendingIndex < 0 || pendingIndex >= pendingCandidates.Count)
            {
                pendingCandidates = null;
                return false;
            }

            if (SelectedText != pendingCandidates[pendingIndex])
            {
                pendingCandidates = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Replaces the current selection (or inserts at the caret when there is none).
        /// <see cref="TextBox.InsertString"/> handles both cases, so the selection start
        /// never needs to be read.
        /// </summary>
        private void replaceRange(int? start, int? end, string replacement, bool select)
        {
            if (start is int startIndex && end is int endIndex && startIndex != endIndex)
                selectRange(startIndex, endIndex);

            InsertString(replacement);

            if (!select)
                return;

            MoveCursorBy(-replacement.Length);
            ExpandSelectionBy(replacement.Length);
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

        /// <summary>
        /// Reads the caret position without reflection: collapse the selection to its active end,
        /// select everything to the left of it and use the resulting selection length.
        /// Must not be called while an IME composition is active.
        /// </summary>
        private int getCaretPosition()
        {
            MoveCursorBy(0);
            ExpandSelectionBy(-Text.Length);

            int caret = SelectedText.Length;

            MoveCursorBy(caret);

            return caret;
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
