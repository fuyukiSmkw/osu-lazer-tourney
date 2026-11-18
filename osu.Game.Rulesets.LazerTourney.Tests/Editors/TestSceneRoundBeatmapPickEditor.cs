// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Editors.Components;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.LazerTourney.Tests.Editors
{
    [TestFixture]
    public partial class TestSceneRoundBeatmapPickEditor : OsuTestScene
    {
        private TournamentRound round = null!;
        private RoundBeatmap beatmap = null!;
        private RoundBeatmapPickEditor editor = null!;

        private OutlinedTextBox textBox => editor.ChildrenOfType<OutlinedTextBox>().Single();

        private OsuCheckbox toggle => editor.ChildrenOfType<OsuCheckbox>().Single();

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create editor", () =>
            {
                round = new TournamentRound();
                round.Beatmaps.Add(new RoundBeatmap { Mods = "HD" });
                beatmap = new RoundBeatmap { Mods = "HD" };
                round.Beatmaps.Add(beatmap);
                round.Beatmaps.Add(new RoundBeatmap { Mods = "HD" });

                Add(editor = new RoundBeatmapPickEditor(round, beatmap)
                {
                    Width = 200,
                });
            });

            AddUntilStep("editor loaded", () => editor.IsLoaded);
        }

        [Test]
        public void TestInferredPickShownWhenDisabled()
        {
            AddAssert("pick is null", () => beatmap.Pick == null);
            AddAssert("text box shows inferred pick", () => textBox.Current.Value == "HD2");
            AddAssert("text box is read-only", () => textBox.ReadOnly);
        }

        [Test]
        public void TestEnablingSeedsInferredPick()
        {
            AddStep("enable custom pick", () => toggle.Current.Value = true);

            AddAssert("pick stored", () => beatmap.Pick == "HD2");
            AddAssert("text box is editable", () => !textBox.ReadOnly);
        }

        [Test]
        public void TestEditingUpdatesModel()
        {
            AddStep("enable custom pick", () => toggle.Current.Value = true);
            AddStep("set text", () => textBox.Current.Value = "custom");

            AddAssert("pick stored", () => beatmap.Pick == "custom");
        }

        [Test]
        public void TestDisablingClearsModel()
        {
            AddStep("enable custom pick", () => toggle.Current.Value = true);
            AddStep("set text", () => textBox.Current.Value = "custom");
            AddStep("disable custom pick", () => toggle.Current.Value = false);

            AddAssert("pick cleared", () => beatmap.Pick == null);
            AddAssert("inferred pick restored", () => textBox.Current.Value == "HD2");
            AddAssert("text box is read-only", () => textBox.ReadOnly);
        }

        [Test]
        public void TestInferenceRefreshesOnReorder()
        {
            AddStep("move map to top", () => round.Beatmaps.Move(1, 0));

            AddUntilStep("inferred pick updated", () => textBox.Current.Value == "HD1");
        }

        [Test]
        public void TestInferenceRefreshesOnModsChange()
        {
            AddStep("change first map mods", () => round.Beatmaps[0].Mods = "DT");
            AddStep("refresh editor", () => editor.Refresh());

            AddAssert("inferred pick updated", () => textBox.Current.Value == "HD1");
        }
    }
}
