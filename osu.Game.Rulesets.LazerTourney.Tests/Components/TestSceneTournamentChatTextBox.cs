// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Rulesets.LazerTourney.Tournament.Components;
using osu.Game.Rulesets.LazerTourney.Tournament.Components.ChatCommands;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Rulesets.LazerTourney.Tests.Components
{
    [TestFixture]
    public partial class TestSceneTournamentChatTextBox : OsuManualInputManagerTestScene
    {
        private static readonly string[] command_candidates = TournamentChatCommandHandler.COMMANDS
                                                                                          .Concat(TournamentChatCommandHandler.COMMAND_ALIASES.Keys)
                                                                                          .ToArray();

        private readonly Bindable<string> draft = new Bindable<string>(string.Empty);

        private TournamentChatTextBox first = null!;
        private TournamentChatTextBox second = null!;

        [SetUp]
        public void SetUp()
        {
            Schedule(() =>
            {
                Add(first = createTextBox());
                Add(second = createTextBox());

                first.TakeFocus();
            });
        }

        [Test]
        public void TestCommandCompletion()
        {
            AddStep("set text", () => first.Text = "/he");
            AddStep("press tab", () => InputManager.Key(Key.Tab));
            AddAssert("command completed", () => first.Text == "/help ");
        }

        [Test]
        public void TestCommandCompletionAtCaret()
        {
            AddStep("set text", () => first.Text = "/hexyz");
            AddRepeatStep("move caret left", () => InputManager.Key(Key.Left), 3);
            AddStep("press tab", () => InputManager.Key(Key.Tab));
            AddAssert("text after the caret preserved", () => first.Text == "/help xyz");
        }

        [Test]
        public void TestCommandCyclingAndConfirm()
        {
            AddStep("set text", () => first.Text = "/");
            AddStep("press tab", () => InputManager.Key(Key.Tab));
            AddAssert("first candidate selected", () => first.SelectedText == command_candidates[0]);
            AddStep("press tab", () => InputManager.Key(Key.Tab));
            AddAssert("second candidate selected", () => first.SelectedText == command_candidates[1]);
            AddStep("press enter", () => InputManager.Key(Key.Enter));
            AddAssert("confirmed with trailing space", () => first.Text == "/" + command_candidates[1] + " ");
        }

        [Test]
        public void TestHistoryRecall()
        {
            commit("first message");
            commit("second message");

            AddStep("recall newest", () => InputManager.Key(Key.Up));
            AddAssert("newest recalled", () => first.Text == "second message");
            AddStep("recall older", () => InputManager.Key(Key.Up));
            AddAssert("older recalled", () => first.Text == "first message");
            AddStep("move down", () => InputManager.Key(Key.Down));
            AddAssert("newest again", () => first.Text == "second message");
            AddStep("move down to draft", () => InputManager.Key(Key.Down));
            AddAssert("draft restored", () => first.Text == string.Empty);
            AddStep("move down again", () => InputManager.Key(Key.Down));
            AddAssert("still draft", () => first.Text == string.Empty);
        }

        [Test]
        public void TestEditingRecalledMessageResetsHistory()
        {
            commit("recalled message");

            AddStep("recall", () => InputManager.Key(Key.Up));
            AddAssert("recalled", () => first.Text == "recalled message");

            AddStep("edit recalled text", () => first.Text = "edited message");
            AddStep("move down", () => InputManager.Key(Key.Down));
            AddAssert("edited text kept", () => first.Text == "edited message");

            AddStep("recall again", () => InputManager.Key(Key.Up));
            AddAssert("newest recalled", () => first.Text == "recalled message");
        }

        [Test]
        public void TestHistorySharedBetweenBoxes()
        {
            AddStep("focus first", () => first.TakeFocus());
            AddUntilStep("first focused", () => first.HasFocus);

            commit("shared message");

            AddStep("focus second", () => second.TakeFocus());
            AddUntilStep("second focused", () => second.HasFocus);

            AddStep("recall", () => InputManager.Key(Key.Up));
            AddAssert("second box recalls message", () => second.Text == "shared message");
        }

        private void commit(string text)
        {
            AddStep($"set text: {text}", () => first.Text = text);
            AddStep("press enter", () => InputManager.Key(Key.Enter));
            AddAssert("draft cleared", () => first.Text == string.Empty);
        }

        private TournamentChatTextBox createTextBox()
        {
            var box = new TournamentChatTextBox
            {
                RelativeSizeAxes = Axes.X,
                Height = 30,
                ReleaseFocusOnCommit = false,
            };

            // Mirrors the real send boxes: the draft is shared through the channel's
            // TextBoxMessage bindable, and committing clears the box.
            box.Current.BindTo(draft);
            box.OnCommit += (_, _) => box.Text = string.Empty;

            return box;
        }
    }
}
