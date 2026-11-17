// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays.Mods;
using osu.Game.Online.API;
using osu.Game.Rulesets.LazerTourney.Tournament;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Editors;
using osu.Game.Screens.OnlinePlay;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.LazerTourney.Tests.Editors
{
    [TestFixture]
    public partial class TestSceneRoundBeatmapReorder : OsuManualInputManagerTestScene
    {
        private readonly LadderInfo ladder = new LadderInfo();

        private TournamentRound round = null!;
        private RoundEditorScreen.RoundRow.RoundBeatmapEditor editor = null!;

        private RoundEditorScreen.RoundRow.RoundBeatmapEditor.RoundBeatmapRow[] rows
            => editor.ChildrenOfType<RoundEditorScreen.RoundRow.RoundBeatmapEditor.RoundBeatmapRow>().ToArray();

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create editor", () =>
            {
                round = new TournamentRound();

                for (int i = 0; i < 3; i++)
                    round.Beatmaps.Add(createBeatmap(i + 1));

                var required = new UserModSelectOverlay();
                var allowed = new FreeModSelectOverlay();

                Add(required);
                Add(allowed);

                Add(new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new[]
                    {
                        (typeof(LadderInfo), (object)ladder),
                        (typeof(TournamentSceneManager), new TournamentSceneManager()),
                    },
                    Child = new OsuScrollContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = editor = new RoundEditorScreen.RoundRow.RoundBeatmapEditor(round, required, allowed)
                        {
                            Width = 0.95f,
                        },
                    },
                });
            });

            AddUntilStep("all rows loaded", () => rows.Length == 3 && rows.All(r => r.IsLoaded));
        }

        [Test]
        public void TestRowsExpandInsteadOfClipping()
        {
            AddAssert("editor has height", () => editor.DrawHeight > 0);

            AddAssert("editor height fits all rows", () =>
            {
                // Rows must not be clipped by a nested scroll viewport (the surrounding page is the only scroll area).
                float expected = rows.Sum(r => r.DrawHeight) + rows.Sum(r => r.Margin.TotalVertical);
                return Math.Abs(editor.DrawHeight - expected) < 2;
            });
        }

        [Test]
        public void TestDragReordersModel()
        {
            AddStep("press first row handle", () =>
            {
                var handle = rows[0].ChildrenOfType<OsuRearrangeableListItem<RoundBeatmap>.PlaylistItemHandle>().Single();

                InputManager.MoveMouseTo(handle);
                InputManager.PressButton(MouseButton.Left);
            });

            AddStep("drag below the list", () => InputManager.MoveMouseTo(editor.ScreenSpaceDrawQuad.BottomLeft + new Vector2(20, -10)));

            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));

            AddAssert("first map moved to the end", () => round.Beatmaps.Select(b => b.ID).SequenceEqual(new[] { 2, 3, 1 }));
        }

        [Test]
        public void TestModelStaysInSync()
        {
            AddStep("reorder via list", () => editor.Items.Move(0, 2));
            AddAssert("model order follows list", () => round.Beatmaps.Select(b => b.ID).SequenceEqual(new[] { 2, 3, 1 }));

            AddStep("add beatmap to model", () => round.Beatmaps.Add(createBeatmap(4)));
            AddUntilStep("row created for new beatmap", () => rows.Length == 4);

            AddStep("remove beatmap from model", () => round.Beatmaps.RemoveAt(0));
            AddUntilStep("row removed with beatmap", () => rows.Length == 3 && rows.All(r => r.Model.ID != 2));
        }

        private static RoundBeatmap createBeatmap(int id) => new RoundBeatmap
        {
            ID = id,
            RequiredMods = Array.Empty<APIMod>(),
            AllowedMods = Array.Empty<APIMod>(),
        };
    }
}
