// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Specialized;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.Mods;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    public partial class TournamentBeatmapPanel : CompositeDrawable
    {
        public readonly IBeatmapInfo? Beatmap;

        /// <summary>
        /// The round map this panel was built from, if any. Used by referee operations
        /// (room playlist edits need freestyle/required/allowed mods, which only exist on <see cref="RoundBeatmap"/>).
        /// </summary>
        public readonly RoundBeatmap? RoundMap;

        private readonly string mod;

        public const float HEIGHT = 50;

        private readonly Bindable<TournamentMatch?> currentMatch = new Bindable<TournamentMatch?>();

        private Box flash = null!;

        private Drawable? categoryIcon;
        private TournamentModDisplay? requiredDisplay;
        private Mod[]? requiredModsOverride;

        public TournamentBeatmapPanel(IBeatmapInfo? beatmap, string mod = "", RoundBeatmap? roundMap = null)
        {
            Beatmap = beatmap;
            this.mod = mod;
            RoundMap = roundMap;

            Width = 400;
            Height = HEIGHT;
        }

        [BackgroundDependencyLoader]
        private void load(LadderInfo ladder, IRulesetStore rulesets)
        {
            currentMatch.BindValueChanged(matchChanged);
            currentMatch.BindTo(ladder.CurrentMatch);

            Masking = true;

            // Online covers only work when the beatmap itself carries online set info
            // (e.g. TournamentBeatmap from the mappool). Local beatmaps (room downloads,
            // globally playing beatmaps, scores) need the local background sprite instead:
            // the cast below yields null for them and the online cover stays empty.
            // NOTE: TournamentBeatmap must stay on the online branch: its IBeatmapInfo.BeatmapSet
            // getter throws, which the local component below would touch.
            Drawable cover = Beatmap is IBeatmapSetOnlineInfo onlineInfo
                ? new NoUnloadBeatmapSetCover
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = OsuColour.Gray(0.5f),
                    OnlineInfo = onlineInfo,
                }
                : new NoUnloadBeatmapBackground
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = OsuColour.Gray(0.5f),
                    Beatmap = { Value = Beatmap },
                };

            AddRangeInternal(new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                },
                cover,
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Padding = new MarginPadding(15),
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        new TournamentSpriteText
                        {
                            Text = Beatmap?.GetDisplayTitleRomanisable(false, false) ?? (LocalisableString)@"unknown",
                            Font = OsuFont.Torus.With(weight: FontWeight.Bold),
                        },
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Children = new Drawable[]
                            {
                                new TournamentSpriteText
                                {
                                    Text = "mapper",
                                    Padding = new MarginPadding { Right = 5 },
                                    Font = OsuFont.Torus.With(weight: FontWeight.Regular, size: 14)
                                },
                                new TournamentSpriteText
                                {
                                    Text = Beatmap?.Metadata.Author.Username ?? "unknown",
                                    Padding = new MarginPadding { Right = 20 },
                                    Font = OsuFont.Torus.With(weight: FontWeight.Bold, size: 14)
                                },
                                new TournamentSpriteText
                                {
                                    Text = "difficulty",
                                    Padding = new MarginPadding { Right = 5 },
                                    Font = OsuFont.Torus.With(weight: FontWeight.Regular, size: 14)
                                },
                                new TournamentSpriteText
                                {
                                    Text = Beatmap?.DifficultyName ?? "unknown",
                                    Font = OsuFont.Torus.With(weight: FontWeight.Bold, size: 14)
                                },
                            }
                        }
                    },
                },
                flash = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Gray,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                },
            });

            FillFlowContainer modIconsContainer = null;
            AddInternal(modIconsContainer = new FillFlowContainer
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Margin = new MarginPadding(10),
                RelativeSizeAxes = Axes.Y,
                AutoSizeAxes = Axes.X,
                Direction = FillDirection.Horizontal,
                Spacing = Vector2.One,
            });

            var requiredModsMargin = new MarginPadding(10);
            if (!string.IsNullOrEmpty(mod))
            {
                modIconsContainer.Add(categoryIcon = new TournamentModIcon(mod)
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Width = 60,
                    RelativeSizeAxes = Axes.Y,
                });
                requiredModsMargin = new MarginPadding(0);
            }

            // Required gameplay mods, displayed left of the category icon.
            var ruleset = rulesets.GetRuleset(ladder.Ruleset.Value?.OnlineID ?? 0)?.CreateInstance();
            Mod[] required = ruleset == null || RoundMap == null
                ? Array.Empty<Mod>()
                : RoundBeatmap.InstantiateMods(RoundMap.SafeRequiredMods, ruleset);

            modIconsContainer.Add(requiredDisplay = new TournamentModDisplay
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Margin = requiredModsMargin,
                Current = { Value = requiredModsOverride ?? required },
            });

            updateModVisibility(ladder.DisplayCategoryModIcon.Value, ladder.DisplayRequiredMods.Value);

            ladder.DisplayCategoryModIcon.BindValueChanged(_ => updateModVisibility(ladder.DisplayCategoryModIcon.Value, ladder.DisplayRequiredMods.Value));
            ladder.DisplayRequiredMods.BindValueChanged(_ => updateModVisibility(ladder.DisplayCategoryModIcon.Value, ladder.DisplayRequiredMods.Value));
        }

        /// <summary>
        /// Overrides the required mods row (e.g. with room playlist item mods).
        /// Defaults to the round map's required mods. Visibility still follows the ladder switches.
        /// </summary>
        public void SetRequiredMods(Mod[] mods)
        {
            requiredModsOverride = mods;

            requiredDisplay?.Current.Value = mods;
        }

        private void updateModVisibility(bool showCategory, bool showRequired)
        {
            categoryIcon?.Alpha = showCategory ? 1 : 0;
            requiredDisplay?.Alpha = showRequired ? 1 : 0;
        }

        private void matchChanged(ValueChangedEvent<TournamentMatch?> match)
        {
            match.OldValue?.PicksBans.CollectionChanged -= picksBansOnCollectionChanged;
            match.NewValue?.PicksBans.CollectionChanged += picksBansOnCollectionChanged;

            Scheduler.AddOnce(updateState);
        }

        private void picksBansOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            => Scheduler.AddOnce(updateState);

        private BeatmapChoice? choice;

        private void updateState()
        {
            if (currentMatch.Value == null)
            {
                return;
            }

            var newChoice = currentMatch.Value.PicksBans.FirstOrDefault(p => p.BeatmapID == Beatmap?.OnlineID);

            bool shouldFlash = newChoice != choice;

            if (newChoice != null)
            {
                if (shouldFlash)
                    flash.FadeOutFromOne(500).Loop(0, 10);

                BorderThickness = 6;

                BorderColour = TournamentColours.GetTeamColour(newChoice.Team);

                switch (newChoice.Type)
                {
                    case ChoiceType.Pick:
                        Colour = Color4.White;
                        Alpha = 1;
                        break;

                    case ChoiceType.Ban:
                        Colour = Color4.Gray;
                        Alpha = 0.5f;
                        break;
                }
            }
            else
            {
                Colour = Color4.White;
                BorderThickness = 0;
                Alpha = 1;
            }

            choice = newChoice;
        }

        private partial class NoUnloadBeatmapSetCover : UpdateableOnlineBeatmapSetCover
        {
            // As covers are displayed on stream, we want them to load as soon as possible.
            protected override double LoadDelay => 0;

            // Use DelayedLoadWrapper to avoid content unloading when switching away to another screen.
            protected override DelayedLoadWrapper CreateDelayedLoadWrapper(Func<Drawable> createContentFunc, double timeBeforeLoad)
                => new DelayedLoadWrapper(createContentFunc(), timeBeforeLoad);
        }

        private partial class NoUnloadBeatmapBackground : UpdateableBeatmapBackgroundSprite
        {
            public NoUnloadBeatmapBackground()
            {
                // Same stream rationale as above: load immediately, ...
                BackgroundLoadDelay = 0;
            }

            // ... and never unload when switching away to another screen.
            protected override DelayedLoadWrapper CreateDelayedLoadWrapper(Func<Drawable> createContentFunc, double timeBeforeLoad)
                => new DelayedLoadWrapper(createContentFunc(), timeBeforeLoad);
        }
    }
}
