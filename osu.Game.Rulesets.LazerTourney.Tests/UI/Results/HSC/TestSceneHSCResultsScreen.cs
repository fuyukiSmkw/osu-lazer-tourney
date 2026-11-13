// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.UI.Results.HSC;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tests.UI.Results.HSC
{
    /// <summary>
    /// Smoke tests for the Hello! Shanden Cup results screen. Verifies that the red (left) and
    /// blue (right) mirrored cards, mods and an overlong difficulty name all load and render.
    /// </summary>
    [TestFixture]
    public partial class TestSceneHSCResultsScreen : OsuTestScene
    {
        [Test]
        public void TestShow()
        {
            HSCResultsScreen? screen = null;

            AddStep("show red results", () =>
            {
                OsuScreenStack stack;

                Add(new Container
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Size = new Vector2(1280, 720),
                    Child = stack = new OsuScreenStack
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                });

                stack.Push(screen = new HSCResultsScreen(createScore(), TeamColour.Red, 3));
            });

            AddUntilStep("screen loaded", () => screen?.IsLoaded == true);
            AddUntilStep("HSC score panel loaded", () => screen!.ChildrenOfType<HSCScorePanel>().Any());
        }

        [Test]
        public void TestShowBlueWithModsAndLongDifficulty()
        {
            HSCScorePanel? panel = null;

            AddStep("show blue panel", () =>
            {
                var score = createScore();
                score.BeatmapInfo!.DifficultyName = "A Deliberately Overlong Difficulty Name That Can Never Fit";
                score.Mods = new Mod[] { new OsuModDoubleTime() };

                // Test the panel directly rather than through another OsuScreenStack; running
                // multiple screen stacks in one headless host trips a parallax/input race.
                Add(new Container
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Size = new Vector2(640, 480),
                    Child = panel = new HSCScorePanel(score, TeamColour.Blue, 12)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                });
            });

            AddUntilStep("panel loaded", () => panel?.IsLoaded == true);
        }

        private static ScoreInfo createScore()
        {
            var ruleset = new OsuRuleset().RulesetInfo;
            var beatmap = new TestBeatmap(ruleset).BeatmapInfo;
            beatmap.OnlineID = 0;
            beatmap.BeatmapSet!.OnlineID = 0;

            return new ScoreInfo
            {
                BeatmapInfo = beatmap,
                Ruleset = ruleset,
                User = new APIUser { Id = 2, Username = "tournament" },
                TotalScore = 987654,
                Accuracy = 0.9876,
                MaxCombo = 500,
                Rank = ScoreRank.S,
                Date = DateTimeOffset.Now,
                Mods = Array.Empty<Mod>(),
                PP = 123.45,
                Statistics = new Dictionary<HitResult, int>
                {
                    { HitResult.Great, 500 },
                    { HitResult.Ok, 10 },
                    { HitResult.Meh, 2 },
                    { HitResult.Miss, 1 },
                },
                MaximumStatistics = new Dictionary<HitResult, int>
                {
                    { HitResult.Great, 513 },
                },
            };
        }
    }
}
