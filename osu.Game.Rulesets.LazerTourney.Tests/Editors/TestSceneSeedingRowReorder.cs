// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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
    public partial class TestSceneSeedingRowReorder : OsuManualInputManagerTestScene
    {
        private readonly LadderInfo ladder = new LadderInfo();
        private readonly TournamentTeam team = new TournamentTeam();

        private SeedingResult[] created = Array.Empty<SeedingResult>();
        private SeedingBeatmap[] originalBeatmaps = Array.Empty<SeedingBeatmap>();

        private OsuScrollContainer page = null!;

        private SeedingEditorScreen.SeedingResultList list = null!;

        private SeedingEditorScreen.SeedingResultRow[] rows => list.ChildrenOfType<SeedingEditorScreen.SeedingResultRow>().ToArray();

        private Vector2 dragStart;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create list", () =>
            {
                team.SeedingResults.Clear();

                created = Enumerable.Range(1, 3).Select(i => createResult($"Mod {i}")).ToArray();

                foreach (var result in created)
                {
                    // The first result gets enough beatmaps to drag around.
                    for (int i = 0; i < 3; i++)
                        result.Beatmaps.Add(createBeatmap());
                }

                originalBeatmaps = created[0].Beatmaps.ToArray();

                foreach (var result in created)
                    team.SeedingResults.Add(result);

                Add(new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new[] { (typeof(LadderInfo), (object)ladder) },
                    Child = page = new OsuScrollContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = list = new SeedingEditorScreen.SeedingResultList(result => new SeedingEditorScreen.SeedingResultRow(team, result)),
                    },
                });

                // The editor screen does the same; dragging changes the model, external model changes update the list.
                list.Items.BindTarget = team.SeedingResults;
            });

            AddUntilStep("all rows loaded", () => rows.Length == 3 && rows.All(r => r.IsLoaded));
            AddUntilStep("all beatmap rows loaded", () => beatmapRows(rows[0]).Length == 3 && beatmapRows(rows[0]).All(r => r.IsLoaded));
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
        public void TestDragReordersResults()
        {
            AddStep("press first row handle", () =>
            {
                var handle = rows[0].ChildrenOfType<OsuRearrangeableListItem<SeedingResult>.PlaylistItemHandle>().Single();

                InputManager.MoveMouseTo(handle);
                InputManager.PressButton(MouseButton.Left);
            });

            AddStep("drag below the list", () => InputManager.MoveMouseTo(list.ScreenSpaceDrawQuad.BottomLeft + new Vector2(20, -10)));

            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));

            AddAssert("first result moved to the end", () => team.SeedingResults.SequenceEqual(new[] { created[1], created[2], created[0] }));
        }

        [Test]
        public void TestDragReordersBeatmaps()
        {
            AddStep("press first beatmap handle", () =>
            {
                var handle = beatmapRows(rows[0])[0].ChildrenOfType<OsuRearrangeableListItem<SeedingBeatmap>.PlaylistItemHandle>().Single();

                InputManager.MoveMouseTo(handle);
                InputManager.PressButton(MouseButton.Left);
            });

            AddStep("drag below the list", () => InputManager.MoveMouseTo(beatmapEditor(rows[0]).ScreenSpaceDrawQuad.BottomLeft + new Vector2(20, -10)));

            AddAssert("drag still active", () => beatmapRows(rows[0])[0].DragActive.Value);

            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));

            AddAssert("result order unchanged", () => team.SeedingResults.SequenceEqual(created));

            AddAssert("first beatmap moved to the end", () =>
                rows[0].Model.Beatmaps.SequenceEqual(new[] { originalBeatmaps[1], originalBeatmaps[2], originalBeatmaps[0] }));
        }

        [Test]
        public void TestDraggingBeatmapsScrollsPageInsteadOfList()
        {
            AddUntilStep("page can scroll", () => page.ScrollableExtent > 0);
            AddUntilStep("nested lists cannot scroll", () => list.ChildrenOfType<ExpandedScrollContainer>().All(s => s.ScrollableExtent == 0));

            AddStep("press gap between beatmap rows", () =>
            {
                // The 20px gap is the two rows' margins, i.e. not covered by either row's content.
                dragStart = beatmapRows(rows[0])[0].ScreenSpaceDrawQuad.BottomLeft + new Vector2(100, 5);
                InputManager.MoveMouseTo(dragStart);
                InputManager.PressButton(MouseButton.Left);
            });

            AddStep("drag up", () => InputManager.MoveMouseTo(dragStart - new Vector2(0, 150)));

            AddUntilStep("page scrolled down", () => page.Current > 0);
            AddAssert("no nested list scrolled", () => list.ChildrenOfType<ExpandedScrollContainer>().All(s => s.Current == 0));

            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));
        }

        [Test]
        public void TestModelStaysInSync()
        {
            AddStep("reorder via list", () => list.Items.Move(0, 2));
            AddAssert("model order follows list", () => team.SeedingResults.SequenceEqual(new[] { created[1], created[2], created[0] }));

            AddStep("add result to model", () => team.SeedingResults.Add(createResult("Mod 4")));
            AddUntilStep("row created for new result", () => rows.Length == 4);

            AddStep("remove result from model", () => team.SeedingResults.RemoveAt(0));
            AddUntilStep("row removed with result", () => rows.Length == 3 && rows.All(r => r.Model != created[1]));

            AddStep("remove beatmap from model", () => created[0].Beatmaps.RemoveAt(0));
            AddUntilStep("beatmap row removed", () => beatmapRows(rows.Single(r => r.Model == created[0])).Length == 2);
        }

        private static SeedingEditorScreen.SeedingResultRow.SeedingBeatmapEditor beatmapEditor(SeedingEditorScreen.SeedingResultRow row)
            => row.ChildrenOfType<SeedingEditorScreen.SeedingResultRow.SeedingBeatmapEditor>().Single();

        private static SeedingEditorScreen.SeedingResultRow.SeedingBeatmapEditor.SeedingBeatmapRow[] beatmapRows(SeedingEditorScreen.SeedingResultRow row)
            => beatmapEditor(row).ChildrenOfType<SeedingEditorScreen.SeedingResultRow.SeedingBeatmapEditor.SeedingBeatmapRow>().ToArray();

        private static SeedingResult createResult(string mod)
        {
            var result = new SeedingResult();
            result.Mod.Value = mod;
            return result;
        }

        private static SeedingBeatmap createBeatmap() => new SeedingBeatmap();
    }
}
