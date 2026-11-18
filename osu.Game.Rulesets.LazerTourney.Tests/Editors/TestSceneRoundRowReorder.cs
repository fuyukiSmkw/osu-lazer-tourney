// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays.Mods;
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
    public partial class TestSceneRoundRowReorder : OsuManualInputManagerTestScene
    {
        private readonly LadderInfo ladder = new LadderInfo();
        private readonly BindableList<TournamentRound> rounds = new BindableList<TournamentRound>();

        private TournamentRound[] created = Array.Empty<TournamentRound>();

        private RoundEditorScreen.RoundList list = null!;

        private RoundEditorScreen.RoundRow[] rows => list.ChildrenOfType<RoundEditorScreen.RoundRow>().ToArray();

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create list", () =>
            {
                rounds.Clear();

                created = Enumerable.Range(1, 3).Select(i => createRound($"Round {i}")).ToArray();

                foreach (var round in created)
                    rounds.Add(round);

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
                        Child = list = new RoundEditorScreen.RoundList(round => new RoundEditorScreen.RoundRow(round, required, allowed)),
                    },
                });

                // The editor screen does the same; dragging changes the model, external model changes update the list.
                list.Items.BindTarget = rounds;
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
                var handle = rows[0].ChildrenOfType<OsuRearrangeableListItem<TournamentRound>.PlaylistItemHandle>().Single();

                InputManager.MoveMouseTo(handle);
                InputManager.PressButton(MouseButton.Left);
            });

            AddStep("drag below the list", () => InputManager.MoveMouseTo(list.ScreenSpaceDrawQuad.BottomLeft + new Vector2(20, -10)));

            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));

            AddAssert("first round moved to the end", () => rounds.SequenceEqual(new[] { created[1], created[2], created[0] }));
        }

        [Test]
        public void TestModelStaysInSync()
        {
            AddStep("reorder via list", () => list.Items.Move(0, 2));
            AddAssert("model order follows list", () => rounds.SequenceEqual(new[] { created[1], created[2], created[0] }));

            AddStep("add round to model", () => rounds.Add(createRound("Round 4")));
            AddUntilStep("row created for new round", () => rows.Length == 4);

            AddStep("remove round from model", () => rounds.RemoveAt(0));
            AddUntilStep("row removed with round", () => rows.Length == 3 && rows.All(r => r.Model != created[1]));
        }

        private static TournamentRound createRound(string name)
        {
            var round = new TournamentRound();
            round.Name.Value = name;
            return round;
        }
    }
}
