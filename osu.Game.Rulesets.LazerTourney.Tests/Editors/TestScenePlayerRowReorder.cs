// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Editors;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Editors.Components;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.LazerTourney.Tests.Editors
{
    [TestFixture]
    public partial class TestScenePlayerRowReorder : OsuManualInputManagerTestScene
    {
        private readonly TournamentTeam team = new TournamentTeam();

        private TournamentUser[] created = Array.Empty<TournamentUser>();

        private OsuScrollContainer page = null!;

        private TeamEditorScreen.TeamRow.PlayerEditor editor = null!;

        private TeamEditorScreen.TeamRow.PlayerEditor.PlayerRow[] rows => editor.ChildrenOfType<TeamEditorScreen.TeamRow.PlayerEditor.PlayerRow>().ToArray();

        private ExpandedScrollContainer innerScroll => editor.ChildrenOfType<ExpandedScrollContainer>().Single();

        private Vector2 dragStart;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create editor", () =>
            {
                team.Players.Clear();

                created = Enumerable.Range(1, 3).Select(i => createPlayer($"Player {i}", i)).ToArray();

                foreach (var player in created)
                    team.Players.Add(player);

                Add(page = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 300,
                    Child = editor = new TeamEditorScreen.TeamRow.PlayerEditor(team),
                });
            });

            AddUntilStep("all rows loaded", () => rows.Length == 3 && rows.All(r => r.IsLoaded));
        }

        [Test]
        public void TestRowsExpandInsteadOfClipping()
        {
            AddUntilStep("list has height", () => editor.DrawHeight > 0);

            AddAssert("list height fits all rows", () =>
            {
                // Rows must not be clipped by a nested scroll viewport (the surrounding page is the only scroll area).
                const float spacing = 5;
                const float padding = 5;
                float expected = rows.Sum(r => r.DrawHeight) + spacing * (rows.Length - 1) + padding * 2;
                return Math.Abs(editor.DrawHeight - expected) < 2;
            });
        }

        [Test]
        public void TestDragReordersModel()
        {
            AddStep("press first row handle", () =>
            {
                var handle = rows[0].ChildrenOfType<OsuRearrangeableListItem<TournamentUser>.PlaylistItemHandle>().Single();

                InputManager.MoveMouseTo(handle);
                InputManager.PressButton(MouseButton.Left);
            });

            AddStep("drag below the list", () => InputManager.MoveMouseTo(editor.ScreenSpaceDrawQuad.BottomLeft + new Vector2(20, -10)));

            AddAssert("drag still active", () => rows[0].DragActive.Value);

            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));

            AddAssert("first player moved to the end", () => team.Players.SequenceEqual(new[] { created[1], created[2], created[0] }));
        }

        [Test]
        public void TestDraggingPlayersScrollsPageInsteadOfList()
        {
            AddStep("add players to make page scrollable", addExtraPlayers);
            AddUntilStep("extra rows loaded", () => rows.Length == 9 && rows.All(r => r.IsLoaded));
            AddUntilStep("page can scroll", () => page.ScrollableExtent > 0);

            AddStep("press row background", () =>
            {
                // The background below the User ID box is not covered by any interactive control.
                dragStart = rows[0].ScreenSpaceDrawQuad.BottomLeft + new Vector2(40, -20);
                InputManager.MoveMouseTo(dragStart);
                InputManager.PressButton(MouseButton.Left);
            });

            AddStep("drag up", () => InputManager.MoveMouseTo(dragStart - new Vector2(0, 150)));

            AddUntilStep("page scrolled down", () => page.Current > 0);
            AddAssert("inner list did not scroll", () => innerScroll.Current == 0);

            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));
        }

        [Test]
        public void TestModelStaysInSync()
        {
            AddStep("reorder via list", () => editor.Items.Move(0, 2));
            AddAssert("model order follows list", () => team.Players.SequenceEqual(new[] { created[1], created[2], created[0] }));

            AddStep("add player to model", () => team.Players.Add(createPlayer("Player 4", 4)));
            AddUntilStep("row created for new player", () => rows.Length == 4);

            AddStep("remove player from model", () => team.Players.RemoveAt(0));
            AddUntilStep("row removed with player", () => rows.Length == 3 && rows.All(r => r.Model != created[1]));
        }

        private void addExtraPlayers()
        {
            for (int i = 4; i <= 9; i++)
                team.Players.Add(createPlayer($"Player {i}", i));
        }

        private static TournamentUser createPlayer(string username, int id)
        {
            return new TournamentUser
            {
                OnlineID = id,
                Username = username,
            };
        }
    }
}
