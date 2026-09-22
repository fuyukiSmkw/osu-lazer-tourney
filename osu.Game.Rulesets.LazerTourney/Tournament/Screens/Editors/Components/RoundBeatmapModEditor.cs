// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.OnlinePlay;
using osu.Game.Screens.Play.HUD;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Editors.Components
{
    /// <summary>
    /// Inline gameplay-mod editor for a <see cref="RoundBeatmap"/>.
    /// Hosts freestyle toggle plus required/allowed mod selection backed by shared
    /// <see cref="ModSelectOverlay"/> / <see cref="FreeModSelectOverlay"/> instances
    /// (same pickers as lazer room creation, without leaving the editor).
    /// </summary>
    /// <remarks>
    /// The shared overlays' <c>SelectedMods</c> bindables are never rebound or unbound here:
    /// doing so would sever the overlays' internal subscriptions. Selections are pushed in
    /// via <c>Value</c> assignment and committed back through persistent subscriptions,
    /// guarded by per-row editing flags.
    /// </remarks>
    public partial class RoundBeatmapModEditor : CompositeDrawable
    {
        private readonly RoundBeatmap beatmap;
        private readonly UserModSelectOverlay requiredOverlay;
        private readonly FreeModSelectOverlay allowedOverlay;

        /// <summary>
        /// Invoked after the model mods change (selection committed or freestyle toggled).
        /// </summary>
        public Action? ModsChanged;

        [Resolved]
        private LadderInfo ladder { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        private readonly Bindable<bool> freestyle = new Bindable<bool>();

        // Whether this row currently owns the shared overlay. Only the owning row commits selections.
        private bool editingRequired;
        private bool editingAllowed;

        // Guards the seeding assignment on open, so merely opening the editor never rewrites the model.
        private bool seeding;

        private OsuSpriteText freestyleBadge = null!;
        private ModDisplay requiredDisplay = null!;
        private ModDisplay allowedDisplay = null!;
        private RoundedButton allowedButton = null!;

        private Ruleset? ruleset => ladder.Ruleset.Value?.CreateInstance();

        public RoundBeatmapModEditor(RoundBeatmap beatmap, UserModSelectOverlay requiredOverlay, FreeModSelectOverlay allowedOverlay)
        {
            this.beatmap = beatmap;
            this.requiredOverlay = requiredOverlay;
            this.allowedOverlay = allowedOverlay;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(4),
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(10),
                        Children = new Drawable[]
                        {
                            new OsuCheckbox
                            {
                                RelativeSizeAxes = Axes.None,
                                Width = 200,
                                LabelText = "Freestyle (free mod pick)",
                                Current = freestyle,
                            },
                            freestyleBadge = new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 14),
                                Colour = OsuColour.Gray(0.7f),
                            },
                        }
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(10),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Width = 80,
                                Font = OsuFont.GetFont(size: 14),
                                Text = "Required:",
                            },
                            requiredDisplay = new ModDisplay
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Scale = new Vector2(0.5f),
                                ExpansionMode = ExpansionMode.AlwaysContracted,
                            },
                            new RoundedButton
                            {
                                RelativeSizeAxes = Axes.None,
                                Size = new Vector2(100, 32),
                                Text = "Select",
                                Action = openRequiredEditor,
                            },
                        }
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(10),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Width = 80,
                                Font = OsuFont.GetFont(size: 14),
                                Text = "Allowed:",
                            },
                            allowedDisplay = new ModDisplay
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Scale = new Vector2(0.5f),
                                ExpansionMode = ExpansionMode.AlwaysContracted,
                            },
                            allowedButton = new RoundedButton
                            {
                                RelativeSizeAxes = Axes.None,
                                Size = new Vector2(100, 32),
                                Text = "Select",
                                Action = openAllowedEditor,
                            },
                        }
                    },
                }
            };

            freestyle.Value = beatmap.Freestyle;
            freestyle.BindValueChanged(v => onFreestyleChanged(v.NewValue));

            // Persistent commit subscriptions. Never unbound: unbinding would also remove
            // the overlays' own internal subscriptions and break their buttons/columns.
            requiredOverlay.SelectedMods.BindValueChanged(_ =>
            {
                if (editingRequired && !seeding)
                    commitRequired();
            });
            requiredOverlay.State.BindValueChanged(v =>
            {
                if (v.NewValue == Visibility.Hidden)
                    editingRequired = false;
            });

            allowedOverlay.SelectedMods.BindValueChanged(_ =>
            {
                if (editingAllowed && !seeding)
                    commitAllowed();
            });
            allowedOverlay.State.BindValueChanged(v =>
            {
                if (v.NewValue == Visibility.Hidden)
                    editingAllowed = false;
            });

            refreshDisplays();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            refreshDisplays();
        }

        private Mod[] toMods(APIMod[] apiMods)
        {
            var rulesetInstance = ruleset;
            return rulesetInstance == null ? Array.Empty<Mod>() : RoundBeatmap.InstantiateMods(apiMods, rulesetInstance);
        }

        private WorkingBeatmap? workingBeatmapOrNull()
        {
            var local = beatmaps.QueryBeatmap(b => b.OnlineID == beatmap.ID);
            return local == null ? null : beatmaps.GetWorkingBeatmap(local);
        }

        private void openRequiredEditor()
        {
            if (ruleset == null)
                return;

            editingRequired = true;

            seeding = true;
            requiredOverlay.SelectedMods.Value = toMods(beatmap.SafeRequiredMods);
            seeding = false;

            requiredOverlay.IsValidMod = m => ModUtils.IsValidModForMatch(m, true, MatchType.HeadToHead, freestyle.Value);
            requiredOverlay.Beatmap.Value = workingBeatmapOrNull();
            requiredOverlay.Show();
        }

        private void commitRequired()
        {
            beatmap.RequiredMods = requiredOverlay.SelectedMods.Value.Select(m => new APIMod(m)).ToArray();
            // Strip newly-conflicting free mods with the same gate as the allowed picker
            // (contained in required or incompatible with it): they can no longer be displayed,
            // so the user could never remove them manually afterwards.
            // Mirrors lazer song select's updateValidMods.
            Mod[] required = toMods(beatmap.SafeRequiredMods);
            Mod[] validAllowed = toMods(beatmap.SafeAllowedMods).Where(m => isValidAllowedMod(m, required)).ToArray();

            if (!validAllowed.Select(m => m.Acronym).SequenceEqual(beatmap.SafeAllowedMods.Select(a => a.Acronym)))
                beatmap.AllowedMods = validAllowed.Select(m => new APIMod(m)).ToArray();

            refreshDisplays();
            ModsChanged?.Invoke();
        }

        private void openAllowedEditor()
        {
            if (ruleset == null || freestyle.Value)
                return;

            editingAllowed = true;

            seeding = true;
            allowedOverlay.SelectedMods.Value = toMods(beatmap.SafeAllowedMods);
            seeding = false;

            // Same gate as lazer song select: not required, compatible with required, valid free mod.
            allowedOverlay.IsValidMod = m => isValidAllowedMod(m, toMods(beatmap.SafeRequiredMods));
            allowedOverlay.Beatmap.Value = workingBeatmapOrNull();
            allowedOverlay.Show();
        }

        /// <summary>
        /// Whether a mod may stay selected as an allowed (free) mod alongside the given required mods.
        /// Same predicate as lazer song select's isValidAllowedMod.
        /// </summary>
        private bool isValidAllowedMod(Mod mod, IReadOnlyList<Mod> required)
            => required.All(r => r.Acronym != mod.Acronym)
               && ModUtils.CheckCompatibleSet(required.Append(mod))
               && ModUtils.IsValidModForMatch(mod, false, MatchType.HeadToHead, freestyle.Value);

        private void commitAllowed()
        {
            beatmap.AllowedMods = allowedOverlay.SelectedMods.Value.Select(m => new APIMod(m)).ToArray();
            refreshDisplays();
            ModsChanged?.Invoke();
        }

        private void onFreestyleChanged(bool enabled)
        {
            beatmap.Freestyle = enabled;

            if (enabled)
            {
                // Strip required mods that may not stay required in freestyle (e.g. HD):
                // the picker hides them, so the user could never remove them manually.
                if (ruleset != null)
                    beatmap.RequiredMods = RoundBeatmap.FilterFreestyleRequiredMods(beatmap.SafeRequiredMods, ruleset);

                // Mirrors lazer song select: freestyle clears the allowed list (all mods implied).
                beatmap.AllowedMods = Array.Empty<APIMod>();
            }
            else if (ruleset != null)
            {
                // Mirrors lazer song select: disabling freestyle refills the explicit full list.
                beatmap.AllowedMods = RoundBeatmap.GetAllFreeMods(ruleset);
            }

            refreshDisplays();
            ModsChanged?.Invoke();
        }

        private void refreshDisplays()
        {
            if (!IsLoaded)
                return;

            freestyleBadge.Text = freestyle.Value ? "FREESTYLE" : "LOCKED MODS";
            requiredDisplay.Current.Value = toMods(beatmap.SafeRequiredMods);
            allowedDisplay.Current.Value = freestyle.Value ? Array.Empty<Mod>() : toMods(beatmap.SafeAllowedMods);
            allowedButton.Enabled.Value = !freestyle.Value;
        }
    }
}
