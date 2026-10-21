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
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;
using osu.Game.Rulesets.LazerTourney.Tournament.UI.Results.V2;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tests.UI.Results.V2
{
    [TestFixture]
    public partial class TestSceneV2ResultsScreen : OsuTestScene
    {
        [Test]
        public void TestShowAndResize()
        {
            Container? host = null;
            OsuScreenStack? stack = null;
            V2ResultsScreen? screen = null;

            AddStep("show full-size results", () =>
            {
                host = new Container
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Size = new Vector2(1280, 720),
                    Child = stack = new OsuScreenStack(),
                };

                Add(host);
                stack.Push(screen = new V2ResultsScreen(createScore()));
            });

            AddUntilStep("screen loaded", () => screen?.IsLoaded == true);
            AddUntilStep("V2 panel loaded", () => screen!.ChildrenOfType<V2ResultsPanel>().Any());
            AddWaitStep("wait for 16:9 layout", 3);
            AddAssert("accuracy circle starts shrinking at 16:9", () =>
            {
                float scale = screen!.ChildrenOfType<AccuracyCircle>().Single().Parent!.Scale.X;
                return scale > 0.9f && scale < 1;
            });

            AddStep("resize to 4:3 cell", () => host!.Size = new Vector2(683, 512));
            AddWaitStep("wait for layout", 3);
            AddAssert("V2 panel still loaded", () => screen!.ChildrenOfType<V2ResultsPanel>().Single().IsLoaded);
            AddAssert("accuracy circle shrinks further", () =>
            {
                float scale = screen!.ChildrenOfType<AccuracyCircle>().Single().Parent!.Scale.X;
                return scale > 0.4f && scale < 0.6f;
            });
            AddAssert("accuracy circle keeps a proportional right margin", () =>
            {
                var area = screen!.ChildrenOfType<AccuracyCircle>().Single().Parent!;
                var column = area.Parent!;
                float halfSize = area.DrawWidth * area.Scale.X / 2;
                float rightMargin = column.DrawWidth - (area.X + halfSize);
                float baseMargin = column.DrawWidth - area.DrawWidth;

                return Math.Abs(rightMargin - baseMargin * area.Scale.X) < 1;
            });

            AddStep("resize to very narrow cell", () => host!.Size = new Vector2(600, 512));
            AddWaitStep("wait for layout", 3);
            AddAssert("accuracy circle reaches minimum size", () => Math.Abs(screen!.ChildrenOfType<AccuracyCircle>().Single().Parent!.Scale.X - 0.4f) < 0.001f);
            AddAssert("accuracy circle is pinned bottom-right", () =>
            {
                var area = screen!.ChildrenOfType<AccuracyCircle>().Single().Parent!;
                float halfSize = area.DrawWidth * area.Scale.X / 2;
                float rightEdge = area.X + halfSize;
                float bottomEdge = area.Y + halfSize;

                return rightEdge > 440 && bottomEdge > 520 && bottomEdge < 540;
            });
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
