// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24,
// via the GooGuTeam/g0v0 fork.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Ranking;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.UI.Results.Legacy
{
    /// <summary>
    /// The osu!stable-style results score panel, laid out from stable's Ranking.cs layout
    /// (LoadScore / InitializeRankingPanel / LoadRanking / LoadMods) with 480-space coordinates
    /// scaled by 1.6 into the legacy design space (1366x768 at its widest, narrowing to 1024x768
    /// for 4:3 cells). Includes the stable entrance animation, the rotating background rays, the
    /// ranking-graph frame with the play's health graph, and the total score / hit counts / combo /
    /// accuracy / grade / mod displays.
    /// </summary>
    /// <remarks>
    /// The retry and watch-replay buttons from the original stable panel are intentionally omitted:
    /// tournament cells are display-only and must not push screens onto the host stack.
    /// </remarks>
    public partial class LegacyRankingPanel : CompositeDrawable
    {
        private readonly ScoreInfo score;
        private readonly bool animate;

        private const int textx1 = 80;
        private const int imgx1 = 40;
        private const int textx2 = 280;
        private const int imgx2 = 240;
        private const int row1 = 160;
        private const int row2 = 220;
        private const int row3 = 280;
        private const int row4 = 320;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private readonly List<(Drawable image, bool shrink, Drawable? text, double time, float targetScale)> entranceElements = new List<(Drawable, bool, Drawable?, double, float)>();
        private Sprite? grade;
        private Sprite? gradeGhost;
        private readonly List<Sprite> modSprites = new List<Sprite>();
        private TextureStore? modIconTextureStore;
        private Box? whiteFlash;
        private double gradeTime;

        public LegacyRankingPanel(ScoreInfo score, bool animate = false)
        {
            this.score = score;
            this.animate = animate;
        }

        protected override void Dispose(bool isDisposing)
        {
            modIconTextureStore?.Dispose();
            base.Dispose(isDisposing);
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, GameHost host)
        {
            RelativeSizeAxes = Axes.Both;

            // osu-resources' classic skin no longer ships stable's mod icons, so keep a local
            // high-DPI fallback for the legacy result panel.
            modIconTextureStore = new TextureStore(
                host.Renderer,
                host.CreateTextureLoaderStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(LegacyRankingPanel).Assembly), @"Resources/Textures/Legacy")),
                useAtlas: false,
                scaleAdjust: 2);

            Texture? tex(string name, bool includeBundledModIcon = false)
            {
                var texture = skin.GetTexture(name)
                              ?? skin.GetTexture($"{name}-0")
                              ?? skins.DefaultClassicSkin.GetTexture(name)
                              ?? skins.DefaultClassicSkin.GetTexture($"{name}-0");

                if (texture != null || !includeBundledModIcon)
                    return texture;

                return modIconTextureStore?.Get($@"{name}@2x");
            }

            bool isLegacyScore = score.IsLegacyScore;

            Texture? stableModTexture(Mod mod)
            {
                // Classic is a lazer-only mod; legacy scores carry a synthetic copy that must be hidden.
                if (mod is ModClassic)
                    return null;

                string name = mod.GetType().Name;
                int idx = name.IndexOf(@"Mod", StringComparison.Ordinal);
                if (idx < 0)
                    return null;

                return tex($@"selection-mod-{mapModTextureName(name[(idx + 3)..].ToLowerInvariant())}", includeBundledModIcon: true);
            }

            // Legacy scores use the stable mod icons. Lazer scores only use them when every mod
            // has a stable icon; otherwise the whole row falls back to lazer's ModDisplay.
            bool useStableModIcons = isLegacyScore || score.Mods.All(mod => stableModTexture(mod) != null);

            var children = new List<Drawable>();

            children.Add(new Sprite
            {
                Position = new Vector2(0, 64 * 1.6f),
                Texture = tex(@"ranking-panel"),
            });

            var graphTex = tex(@"ranking-graph");

            children.Add(new Sprite
            {
                Position = new Vector2(160, 380) * 1.6f,
                Texture = graphTex,
            });

            if (score.HitEvents.Count > 0)
            {
                children.Add(new LegacyHealthGraph(score, animate)
                {
                    Position = new Vector2(165, 385) * 1.6f,
                });
            }

            var scoreText = new LegacySpriteText(LegacyFont.Score)
            {
                Origin = Anchor.Centre,
                Position = new Vector2(220, 94) * 1.6f,
                Scale = new Vector2(1.3f),
                Alpha = animate ? 0 : 1,
                Text = score.TotalScore <= 9999999 ? $"{score.TotalScore:0000000}" : $"{score.TotalScore:00000000}",
            };
            children.Add(scoreText);

            double t = 300;
            entranceElements.Add((scoreText, false, null, t, 1));

            foreach (var (image, position, count) in hitRows())
            {
                t += 300;

                var img = new Sprite
                {
                    Origin = Anchor.Centre,
                    Position = position * 1.6f,
                    Texture = tex(image),
                    Alpha = animate ? 0 : 1,
                    Scale = animate ? new Vector2(2) : Vector2.One,
                };
                var txt = new LegacySpriteText(LegacyFont.Score)
                {
                    Position = new Vector2(position.X + 40, position.Y - 16) * 1.6f,
                    Scale = new Vector2(1.12f),
                    Alpha = animate ? 0 : 1,
                    Text = $"{count}x",
                };
                children.Add(img);
                children.Add(txt);
                entranceElements.Add((img, true, txt, t, 1));
            }

            t += 300;

            var comboImg = new Sprite
            {
                Position = new Vector2(imgx1 - 35, row4 - 20) * 1.6f,
                Texture = tex(@"ranking-maxcombo"),
                Alpha = animate ? 0 : 1,
            };
            var comboTxt = new LegacySpriteText(LegacyFont.Score)
            {
                Position = new Vector2(textx1 - 65, row4 + 10) * 1.6f,
                Scale = new Vector2(1.12f),
                Alpha = animate ? 0 : 1,
                Text = $"{score.MaxCombo}x",
            };
            children.Add(comboImg);
            children.Add(comboTxt);
            entranceElements.Add((comboImg, false, comboTxt, t, 1));

            t += 300;

            var accImg = new Sprite
            {
                Position = new Vector2(imgx2 - 58, row4 - 20) * 1.6f,
                Texture = tex(@"ranking-accuracy"),
                Alpha = animate ? 0 : 1,
            };
            var accTxt = new LegacySpriteText(LegacyFont.Score)
            {
                Position = new Vector2(textx2 - 86, row4 + 10) * 1.6f,
                Scale = new Vector2(1.12f),
                Alpha = animate ? 0 : 1,
                Text = $"{score.Accuracy * 100:0.00}%",
            };
            children.Add(accImg);
            children.Add(accTxt);
            entranceElements.Add((accImg, false, accTxt, t, 1));

            gradeTime = t + 300;

            var gradeTex = tex($@"ranking-{score.Rank}") ?? tex(@"ranking-D");

            grade = new Sprite
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-120, 200) * 1.6f,
                Texture = gradeTex,
                Alpha = animate ? 0 : 1,
                Scale = animate ? new Vector2(2) : Vector2.One,
            };
            children.Add(grade);

            if (animate && score.Rank >= ScoreRank.B)
            {
                gradeGhost = new Sprite
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-120, 200) * 1.6f,
                    Texture = gradeTex,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                };
                children.Add(gradeGhost);
            }

            int modIndex = 0;

            if (useStableModIcons)
            {
                foreach (var mod in score.Mods)
                {
                    var modTex = stableModTexture(mod);
                    if (modTex == null)
                        continue;

                    var modSprite = new Sprite
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.Centre,
                        Position = new Vector2(-(40 + modIndex * 20) * 1.6f, 260 * 1.6f),
                        Texture = modTex,
                        Alpha = animate ? 0 : 1,
                        Scale = animate ? new Vector2(2) : Vector2.One,
                    };
                    children.Add(modSprite);
                    modSprites.Add(modSprite);
                    modIndex++;
                }
            }
            else if (score.Mods.Length > 0)
            {
                var modDisplay = new ModDisplay(showExtendedInformation: false)
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(-14 * 1.6f, 245 * 1.6f),
                    ExpansionMode = ExpansionMode.AlwaysContracted,
                    Alpha = animate ? 0 : 1,
                };
                modDisplay.Current.Value = score.Mods;
                children.Add(modDisplay);

                if (animate)
                    entranceElements.Add((modDisplay, false, null, 300, 1));
            }

            if (isPerfect)
            {
                gradeTime += 300;

                var perfectTex = tex(@"ranking-perfect");
                float perfectScale = 1;

                if (perfectTex != null && graphTex != null)
                    perfectScale = Math.Min(1, graphTex.DisplayWidth * 0.95f / perfectTex.DisplayWidth);

                var perfect = new Sprite
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(260, 430) * 1.6f,
                    Texture = perfectTex,
                    Alpha = animate ? 0 : 1,
                    Scale = new Vector2(animate ? perfectScale * 2 : perfectScale),
                };
                children.Add(perfect);
                entranceElements.Add((perfect, true, null, gradeTime, perfectScale));

                gradeTime += 300;
            }

            if (animate)
            {
                whiteFlash = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                };
                children.Add(whiteFlash);
            }

            InternalChildren = children.ToArray();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (!animate)
                return;

            foreach (var (image, shrink, text, time, targetScale) in entranceElements)
            {
                image.Delay(time).FadeIn(300, Easing.In);

                if (shrink)
                    image.Delay(time).ScaleTo(targetScale, 300, Easing.In);

                if (text != null)
                {
                    float finalX = text.X;
                    text.X = finalX - 40 * 1.6f;
                    text.Delay(time + 200).MoveToX(finalX, 300, Easing.Out);
                    text.Delay(time + 200).FadeIn(300, Easing.Out);
                }
            }

            grade?.Delay(gradeTime).FadeIn(1000, Easing.In);
            grade?.Delay(gradeTime).ScaleTo(1, 1000, Easing.In);

            if (gradeGhost != null)
            {
                gradeGhost.Delay(gradeTime + 1000).FadeTo(1).Then().FadeOut(2400, Easing.Out);
                gradeGhost.Delay(gradeTime + 1000).ScaleTo(1.05f, 2400, Easing.Out);
            }

            if (whiteFlash != null)
                whiteFlash.Delay(gradeTime + 1000).FadeTo(0.5f).Then().FadeOut(1400);

            for (int i = 0; i < modSprites.Count; i++)
            {
                modSprites[i].Delay(300 + i * 500).FadeIn(400, Easing.Out);
                modSprites[i].Delay(300 + i * 500).ScaleTo(1, 400, Easing.Out);
            }
        }

        private static string mapModTextureName(string name) => name switch
        {
            @"autopilot" => @"relax2",
            @"targetpractice" => @"target",
            @"daycore" => @"halftime",
            _ => name,
        };

        private bool isPerfect => stat(HitResult.Miss) == 0 && stat(HitResult.LargeTickMiss) == 0 && stat(HitResult.ComboBreak) == 0;

        private int stat(HitResult result) => score.Statistics.GetValueOrDefault(result);

        private int maxStat(HitResult result) => score.MaximumStatistics.GetValueOrDefault(result);

        private IEnumerable<(string image, Vector2 position, int count)> hitRows()
        {
            switch (score.Ruleset.ShortName)
            {
                default:
                    yield return (@"hit300", new Vector2(imgx1, row1), stat(HitResult.Great));
                    yield return (@"hit100", new Vector2(imgx1, row2), stat(HitResult.Ok));
                    yield return (@"hit50", new Vector2(imgx1, row3), stat(HitResult.Meh));
                    yield return (@"hit300g", new Vector2(imgx2, row1), stat(HitResult.LargeTickHit));
                    yield return (@"hit100k", new Vector2(imgx2, row2), Math.Max(0, maxStat(HitResult.SliderTailHit) - stat(HitResult.SliderTailHit)));
                    yield return (@"hit0", new Vector2(imgx2, row3), stat(HitResult.Miss));
                    break;

                case @"taiko":
                    yield return (@"taiko-hit300", new Vector2(imgx1, row1), stat(HitResult.Great));
                    yield return (@"taiko-hit100", new Vector2(imgx1, row2), stat(HitResult.Ok));
                    yield return (@"taiko-hit0", new Vector2(imgx1, row3), stat(HitResult.Miss));
                    yield return (@"taiko-hit300g", new Vector2(imgx2, row1), stat(HitResult.LargeBonus));
                    yield return (@"taiko-hit100k", new Vector2(imgx2, row2), stat(HitResult.SmallBonus));
                    break;

                case @"fruits":
                    yield return (@"fruit-orange", new Vector2(imgx1, row1), stat(HitResult.Great));
                    yield return (@"fruit-drop", new Vector2(imgx1, row2), stat(HitResult.LargeTickHit));
                    yield return (@"fruit-drop", new Vector2(imgx1, row3), stat(HitResult.SmallTickHit));
                    yield return (@"hit0", new Vector2(imgx2, row1), stat(HitResult.Miss));
                    break;

                case @"mania":
                    yield return (@"mania-hit300", new Vector2(imgx1, row1), stat(HitResult.Great));
                    yield return (@"mania-hit200", new Vector2(imgx1, row2), stat(HitResult.Good));
                    yield return (@"mania-hit50", new Vector2(imgx1, row3), stat(HitResult.Meh));
                    yield return (@"mania-hit300g", new Vector2(imgx2, row1), stat(HitResult.Perfect));
                    yield return (@"mania-hit100", new Vector2(imgx2, row2), stat(HitResult.Ok));
                    yield return (@"mania-hit0", new Vector2(imgx2, row3), stat(HitResult.Miss));
                    break;
            }
        }
    }
}
