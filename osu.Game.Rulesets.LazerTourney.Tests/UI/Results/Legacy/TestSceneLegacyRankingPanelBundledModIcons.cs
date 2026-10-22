// Copyright (c) ppy Pty Ltd <contact@ppy.sh> Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.LazerTourney.Tournament.UI.Results.Legacy;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tests.UI.Results.Legacy
{
    [TestFixture]
    public partial class TestSceneLegacyRankingPanelBundledModIcons : OsuTestScene
    {
        [Test]
        public void TestBundledStableModIconFallback()
        {
            LegacyRankingPanel? panel = null;

            AddStep("show ranking panel", () =>
            {
                var score = createScore();
                score.Mods = new Mod[] { new OsuModDoubleTime() };

                Add(panel = new LegacyRankingPanel(score)
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Size = new Vector2(1024, 768),
                });
            });

            AddUntilStep("ranking panel loaded", () => panel?.IsLoaded == true);
            AddAssert("lazer mod display not used", () => !panel!.ChildrenOfType<ModDisplay>().Any());
            AddAssert("bundled stable mod icon loaded", () =>
            {
                var modSprites = (List<Sprite>)typeof(LegacyRankingPanel)
                                              .GetField("modSprites", BindingFlags.Instance | BindingFlags.NonPublic)!
                                              .GetValue(panel)!;

                return modSprites.Count == 1 && modSprites[0].Texture != null;
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
