// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets.LazerTourney.Tournament;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.LazerTourney.Tests.Components
{
    [TestFixture]
    public partial class TestSceneInvalidBeatmapDialog : OsuTestScene
    {
        private InvalidBeatmapDialog dialog = null!;
        private int deleted;
        private int exited;

        [Test]
        public void TestDeleteAndContinueDoesNotExit()
        {
            showDialog();

            AddStep("press delete and continue", () => dialog.PerformAction<PopupDialogDangerousButton>());

            AddUntilStep("delete invoked once", () => deleted == 1);
            AddWaitStep("wait for pop-out", 2);
            AddAssert("exit not invoked", () => exited == 0);
        }

        [Test]
        public void TestDismissalExitsOnce()
        {
            showDialog();

            AddStep("dismiss dialog", () => dialog.Hide());

            AddUntilStep("exit invoked once", () => exited == 1);
            AddWaitStep("wait for pop-out", 2);
            AddAssert("delete not invoked", () => deleted == 0);
            AddAssert("exit invoked only once", () => exited == 1);
        }

        private void showDialog()
        {
            AddStep("show dialog", () =>
            {
                deleted = 0;
                exited = 0;

                Add(dialog = new InvalidBeatmapDialog(
                    "Artist - Title [Insane]",
                    "Round of 32",
                    "stored ruleset does not match the selected ruleset",
                    () => deleted++,
                    () => exited++));

                dialog.Show();
            });
        }
    }
}
