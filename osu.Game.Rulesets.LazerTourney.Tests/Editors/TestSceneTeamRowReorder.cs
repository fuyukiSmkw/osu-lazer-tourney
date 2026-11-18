// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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
    public partial class TestSceneTeamRowReorder : OsuManualInputManagerTestScene
    {
        private readonly LadderInfo ladder = new LadderInfo();

        private TournamentTeam[] created = Array.Empty<TournamentTeam>();

        private OsuScrollContainer page = null!;

        private TeamEditorScreen.TeamList list = null!;

        private TeamEditorScreen.TeamRow[] rows => list.ChildrenOfType<TeamEditorScreen.TeamRow>().ToArray();

        private ExpandedScrollContainer innerScroll => list.ChildrenOfType<ExpandedScrollContainer>().Single();

        private Vector2 dragStart;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create list", () =>
            {
                ladder.Teams.Clear();

                created = Enumerable.Range(1, 3).Select(i => createTeam($"Team {i}", $"T{i}")).ToArray();

                foreach (var team in created)
                    ladder.Teams.Add(team);

                Add(new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new[] { (typeof(LadderInfo), (object)ladder) },
                    Child = page = new OsuScrollContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = list = new TeamEditorScreen.TeamList(team => new TeamEditorScreen.TeamRow(team, null!)),
                    },
                });

                // The editor screen does the same; dragging changes the model, external model changes update the list.
                list.Items.BindTarget = ladder.Teams;
            });

            AddUntilStep("all rows loaded", () => rows.Length == 3 && rows.All(r => r.IsLoaded));
        }

        [Test]
        public void TestRowsExpandInsteadOfClipping()
        {
            AddUntilStep("list has height", () => list.DrawHeight > 0);

            AddAssert("list height fits all rows", () =>
            {
                // Rows must not be clipped by a nested scroll viewport (the surrounding page is the only scroll area).
                const float spacing = 20;
                float expected = rows.Sum(r => r.DrawHeight) + spacing * (rows.Length - 1);
                return Math.Abs(list.DrawHeight - expected) < 2;
            });
        }

        [Test]
        public void TestDragReordersModel()
        {
            AddStep("press first row handle", () =>
            {
                var handle = rows[0].ChildrenOfType<OsuRearrangeableListItem<TournamentTeam>.PlaylistItemHandle>().Single();

                InputManager.MoveMouseTo(handle);
                InputManager.PressButton(MouseButton.Left);
            });

            AddStep("drag below the list", () => InputManager.MoveMouseTo(list.ScreenSpaceDrawQuad.BottomLeft + new Vector2(20, -10)));

            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));

            AddAssert("first team moved to the end", () => ladder.Teams.SequenceEqual(new[] { created[1], created[2], created[0] }));
        }

        [Test]
        public void TestMouseDragScrollsPageInsteadOfList()
        {
            AddStep("add teams to make page scrollable", addExtraTeams);
            AddUntilStep("extra rows loaded", () => rows.Length == 9 && rows.All(r => r.IsLoaded));
            AddUntilStep("page can scroll", () => page.ScrollableExtent > 0);

            AddStep("press row background", () =>
            {
                dragStart = rowDragPoint();
                InputManager.MoveMouseTo(dragStart);
                InputManager.PressButton(MouseButton.Left);
            });

            AddStep("drag up", () => InputManager.MoveMouseTo(dragStart - new Vector2(0, 150)));

            AddUntilStep("page scrolled down", () => page.Current > 0);
            AddAssert("inner list did not scroll", () => innerScroll.Current == 0);

            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));
        }

        [Test]
        public void TestTouchDragScrollsPageInsteadOfList()
        {
            AddStep("add teams to make page scrollable", addExtraTeams);
            AddUntilStep("extra rows loaded", () => rows.Length == 9 && rows.All(r => r.IsLoaded));
            AddUntilStep("page can scroll", () => page.ScrollableExtent > 0);

            AddStep("touch row background", () =>
            {
                dragStart = rowDragPoint();
                InputManager.BeginTouch(new Touch(TouchSource.Touch1, dragStart));
            });

            AddStep("drag touch up", () => InputManager.MoveTouchTo(new Touch(TouchSource.Touch1, dragStart - new Vector2(0, 150))));

            AddUntilStep("page scrolled down", () => page.Current > 0);
            AddAssert("inner list did not scroll", () => innerScroll.Current == 0);

            AddStep("end touch", () => InputManager.EndTouch(new Touch(TouchSource.Touch1, dragStart - new Vector2(0, 150))));
        }

        [Test]
        public void TestModelStaysInSync()
        {
            AddStep("reorder via list", () => list.Items.Move(0, 2));
            AddAssert("model order follows list", () => ladder.Teams.SequenceEqual(new[] { created[1], created[2], created[0] }));

            AddStep("add team to model", () => ladder.Teams.Add(createTeam("Team 4", "T4")));
            AddUntilStep("row created for new team", () => rows.Length == 4);

            AddStep("remove team from model", () => ladder.Teams.RemoveAt(0));
            AddUntilStep("row removed with team", () => rows.Length == 3 && rows.All(r => r.Model != created[1]));
        }

        private Vector2 rowDragPoint() => rows[0].ScreenSpaceDrawQuad.BottomLeft + new Vector2(40, -20);

        private void addExtraTeams()
        {
            for (int i = 4; i <= 9; i++)
                ladder.Teams.Add(createTeam($"Team {i}", $"T{i}"));
        }

        private static TournamentTeam createTeam(string name, string acronym)
        {
            var team = new TournamentTeam();
            team.FullName.Value = name;
            team.Acronym.Value = acronym;
            return team;
        }
    }
}
