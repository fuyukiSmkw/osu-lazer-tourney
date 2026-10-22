// Copyright (c) ppy Pty Ltd <contact@ppy.sh> Licensed under the MIT Licence.
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
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;
using osu.Game.Rulesets.LazerTourney.Tournament.UI.Results.Legacy;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tests.UI.Results.Legacy
{
    [TestFixture]
    public partial class TestSceneLegacyResultsScreen : OsuTestScene
    {
        [Test]
        public void TestShowAndResize()
        {
            Container? host = null;
            OsuScreenStack? stack = null;
            LegacyResultsScreen? screen = null;

            AddStep("show full-size results", () =>
            {
                var score = createScore();
                score.HitEvents = createHitEvents();

                host = new Container
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Size = new Vector2(1280, 720),
                    Child = stack = new OsuScreenStack(),
                };

                Add(host);
                stack.Push(screen = new LegacyResultsScreen(score));
            });

            AddUntilStep("screen loaded", () => screen?.IsLoaded == true);
            AddUntilStep("legacy panel loaded", () => screen!.ChildrenOfType<LegacyRankingPanel>().Any());
            AddUntilStep("legacy header loaded", () => screen!.ChildrenOfType<Drawable>().Any(d => d.GetType().Name == "LegacyHeaderStrip"));
            AddUntilStep("health graph loaded", () => screen!.ChildrenOfType<LegacyHealthGraph>().SingleOrDefault()?.IsLoaded == true);
            AddUntilStep("lazer mod display loaded", () => screen!.ChildrenOfType<LegacyRankingPanel>().Single().ChildrenOfType<ModDisplay>().Any());
            AddWaitStep("wait for graph computation", 10);
            AddAssert("classic layout hidden", () => screen!.ChildrenOfType<StatisticsPanel>().Single().Alpha == 0);

            AddStep("resize to 4:3 cell", () => host!.Size = new Vector2(683, 512));
            AddWaitStep("wait for layout", 3);
            AddAssert("legacy panel still loaded", () => screen!.ChildrenOfType<LegacyRankingPanel>().Single().IsLoaded);
            AddAssert("4:3 layout fills cell", () =>
            {
                var panel = screen!.ChildrenOfType<LegacyRankingPanel>().Single();
                Vector2 topLeft = panel.ToScreenSpace(Vector2.Zero);
                Vector2 bottomRight = panel.ToScreenSpace(new Vector2(panel.DrawWidth, panel.DrawHeight));

                return Math.Abs(bottomRight.X - topLeft.X - 683) < 1
                       && Math.Abs(bottomRight.Y - topLeft.Y - 512) < 1;
            });

            AddStep("resize to wide cell", () => host!.Size = new Vector2(1000, 512));
            AddWaitStep("wait for wide layout", 3);
            AddAssert("wide content keeps design width", () =>
            {
                var panel = screen!.ChildrenOfType<LegacyRankingPanel>().Single();
                Vector2 topLeft = panel.ToScreenSpace(Vector2.Zero);
                Vector2 bottomRight = panel.ToScreenSpace(new Vector2(panel.DrawWidth, panel.DrawHeight));

                return Math.Abs(bottomRight.X - topLeft.X - 1366f * 512 / 768) < 1;
            });
            AddAssert("wide header reaches cell edges", () =>
            {
                Drawable header = legacyHeader(screen!);
                Vector2 topLeft = header.ToScreenSpace(Vector2.Zero);
                Vector2 bottomRight = header.ToScreenSpace(new Vector2(header.DrawWidth, header.DrawHeight));
                Vector2 hostTopLeft = host!.ToScreenSpace(Vector2.Zero);

                return Math.Abs(bottomRight.X - topLeft.X - host!.DrawWidth) < 1
                       && Math.Abs(topLeft.Y - hostTopLeft.Y) < 1;
            });

            AddStep("resize to narrow cell", () => host!.Size = new Vector2(600, 512));
            AddWaitStep("wait for narrow layout", 3);
            AddAssert("narrow content keeps design width", () =>
            {
                var panel = screen!.ChildrenOfType<LegacyRankingPanel>().Single();
                Vector2 topLeft = panel.ToScreenSpace(Vector2.Zero);
                Vector2 bottomRight = panel.ToScreenSpace(new Vector2(panel.DrawWidth, panel.DrawHeight));
                Vector2 hostTopLeft = host!.ToScreenSpace(Vector2.Zero);

                return Math.Abs(bottomRight.X - topLeft.X - 600) < 1
                       && topLeft.Y > hostTopLeft.Y + 1
                       && bottomRight.Y < hostTopLeft.Y + host.DrawHeight - 1;
            });
            AddAssert("narrow header reaches cell edges", () =>
            {
                Drawable header = legacyHeader(screen!);
                Vector2 topLeft = header.ToScreenSpace(Vector2.Zero);
                Vector2 bottomRight = header.ToScreenSpace(new Vector2(header.DrawWidth, header.DrawHeight));
                Vector2 hostTopLeft = host!.ToScreenSpace(Vector2.Zero);

                return Math.Abs(bottomRight.X - topLeft.X - host.DrawWidth) < 1
                       && Math.Abs(topLeft.Y - hostTopLeft.Y) < 1;
            });
        }

        private static Drawable legacyHeader(LegacyResultsScreen screen)
            => screen.ChildrenOfType<Drawable>().Single(d => d.GetType().Name == "LegacyHeaderStrip");

        private static List<HitEvent> createHitEvents()
        {
            var events = new List<HitEvent>();

            for (int i = 0; i < 4; i++)
            {
                var hitObject = new HitCircle { StartTime = i * 1000 };
                events.Add(new HitEvent(0, 1, i == 3 ? HitResult.Miss : HitResult.Great, hitObject, null, null));
            }

            return events;
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
                // Mixed stable/lazer mods force the ModDisplay fallback.
                Mods = new Mod[] { new OsuModDoubleTime(), new OsuModBlinds() },
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
