// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
// Ported from the GooGuTeam/g0v0 legacy results screen.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics.Sprites;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.UI.Results.Legacy
{
    /// <summary>
    /// Hosts the stable-style ranking panel on a 768-high design surface whose width adapts
    /// between the stable 1366-wide layout and a 4:3-friendly 1024-wide layout. The score
    /// content keeps that design width, while the backdrop and header stay full-bleed so
    /// wide or tall cells never show a blank letterbox around them.
    /// </summary>
    public partial class LegacyResultsPanel : CompositeDrawable
    {
        private readonly ScoreInfo score;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        public LegacyResultsPanel(ScoreInfo score)
        {
            this.score = score;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.8f,
                },
                new SkinProvidingContainer(skins.DefaultClassicSkin)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new SkinProvidingContainer(skins.CurrentSkin.Value)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new LegacyBackdrop(),
                            new LegacyDesignSurface
                            {
                                Child = new LegacyRankingPanel(score, animate: true)
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                            },
                            new LegacyHeaderStrip(score),
                        },
                    },
                },
            };
        }

        /// <summary>
        /// Shared responsive layout calculation for the content surface, backdrop and header.
        /// </summary>
        private static class LegacyLayout
        {
            public const float DESIGN_HEIGHT = 768;
            public const float MIN_DESIGN_WIDTH = 1024;
            public const float MAX_DESIGN_WIDTH = 1366;

            public static (float DesignWidth, float Scale) Calculate(Vector2 available)
            {
                if (available.X <= 0 || available.Y <= 0)
                    return (MIN_DESIGN_WIDTH, 1);

                float heightScale = available.Y / DESIGN_HEIGHT;
                float widthAtFullHeight = available.X / heightScale;
                float designWidth = Math.Clamp(widthAtFullHeight, MIN_DESIGN_WIDTH, MAX_DESIGN_WIDTH);
                float scale = Math.Min(available.X / designWidth, available.Y / DESIGN_HEIGHT);

                return (designWidth, scale);
            }
        }

        /// <summary>
        /// The scaled stable layout surface. The score panel, health graph, hit counts, combo,
        /// accuracy, grade and mod icons stay at the adaptive design width; only the backdrop
        /// and header are allowed to extend beyond it.
        /// </summary>
        private partial class LegacyDesignSurface : Container
        {
            public LegacyDesignSurface()
            {
                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;
                Masking = true;
            }

            protected override void Update()
            {
                base.Update();

                if (Parent == null)
                    return;

                Vector2 available = Parent.ChildSize;

                if (available.X <= 0 || available.Y <= 0)
                    return;

                var (designWidth, scale) = LegacyLayout.Calculate(available);

                Size = new Vector2(designWidth, LegacyLayout.DESIGN_HEIGHT);
                Scale = new Vector2(scale);
            }
        }

        /// <summary>
        /// The stable background rays, kept outside the content surface and scaled to the same
        /// visual size so they reach the cell edges instead of ending at a letterbox.
        /// </summary>
        private partial class LegacyBackdrop : CompositeDrawable
        {
            [Resolved]
            private SkinManager skins { get; set; } = null!;

            private Sprite? rays;

            public LegacyBackdrop()
            {
                RelativeSizeAxes = Axes.Both;
                Masking = true;
            }

            [BackgroundDependencyLoader]
            private void load(ISkinSource skin)
            {
                Texture? raysTexture = skin.GetTexture(@"ranking-background-overlay")
                                        ?? skin.GetTexture(@"ranking-background-overlay-0")
                                        ?? skins.DefaultClassicSkin.GetTexture(@"ranking-background-overlay")
                                        ?? skins.DefaultClassicSkin.GetTexture(@"ranking-background-overlay-0");

                if (raysTexture == null)
                    return;

                rays = new Sprite
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Texture = raysTexture,
                    Blending = BlendingParameters.Additive,
                };

                AddInternal(rays);
                rays.OnLoadComplete += d => d.Spin(20000, RotationDirection.Clockwise);
            }

            protected override void Update()
            {
                base.Update();

                if (rays == null || Parent == null)
                    return;

                Vector2 available = Parent.ChildSize;

                if (available.X <= 0 || available.Y <= 0)
                    return;

                float scale = LegacyLayout.Calculate(available).Scale;

                rays.Scale = new Vector2(scale);
                rays.Position = new Vector2(-180, 200) * 1.6f * scale;
            }
        }

        /// <summary>
        /// The fixed stable header strip: map title, mapper and player details over a dark bar,
        /// with the skin's ranking-title texture on the right. It stretches to the cell edges
        /// while its font and bar height follow the content surface scale.
        /// </summary>
        private partial class LegacyHeaderStrip : CompositeDrawable
        {
            private readonly ScoreInfo score;

            [Resolved]
            private SkinManager skins { get; set; } = null!;

            public LegacyHeaderStrip(ScoreInfo score)
            {
                this.score = score;

                Anchor = Anchor.TopLeft;
                Origin = Anchor.TopLeft;
            }

            [BackgroundDependencyLoader]
            private void load(ISkinSource skin)
            {
                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                        Alpha = 0.8f,
                    },
                    new OsuSpriteText
                    {
                        Position = new Vector2(0, 0),
                        Font = LegacyFonts.Get(22 * 1.6f),
                        Shadow = true,
                        Text = $"{score.BeatmapInfo?.Metadata.Artist} - {score.BeatmapInfo?.Metadata.Title} [{score.BeatmapInfo?.DifficultyName}]",
                    },
                    new OsuSpriteText
                    {
                        Position = new Vector2(1.6f, 20 * 1.6f),
                        Font = LegacyFonts.Get(16 * 1.6f),
                        Shadow = true,
                        Text = $"Beatmap by {score.BeatmapInfo?.Metadata.Author.Username}",
                    },
                    new OsuSpriteText
                    {
                        Position = new Vector2(1.6f, 34 * 1.6f),
                        Font = LegacyFonts.Get(16 * 1.6f),
                        Shadow = true,
                        Text = $"Played by {score.User.Username} on {score.Date.ToLocalTime():g}.",
                    },
                    new Sprite
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        X = -20 * 1.6f,
                        Texture = skin.GetTexture(@"ranking-title") ?? skins.DefaultClassicSkin.GetTexture(@"ranking-title"),
                    },
                };
            }

            protected override void Update()
            {
                base.Update();

                if (Parent == null)
                    return;

                Vector2 available = Parent.ChildSize;

                if (available.X <= 0 || available.Y <= 0)
                    return;

                float scale = LegacyLayout.Calculate(available).Scale;

                Scale = new Vector2(scale);
                Size = new Vector2(available.X / scale, 60 * 1.6f);
            }
        }
    }
}
