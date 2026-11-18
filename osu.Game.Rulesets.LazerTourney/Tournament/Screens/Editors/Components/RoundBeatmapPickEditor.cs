// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Specialized;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Editors.Components
{
    /// <summary>
    /// Editor for <see cref="RoundBeatmap.Pick"/>.
    /// The switch controls whether an explicit pick label is stored: off means <c>null</c> (the text box
    /// shows the inferred label and is read-only), on means a non-null value bound to the text box.
    /// </summary>
    public partial class RoundBeatmapPickEditor : CompositeDrawable
    {
        private readonly TournamentRound round;
        private readonly RoundBeatmap beatmap;

        private readonly BindableBool hasCustomPick = new BindableBool();
        private readonly Bindable<string> pickText = new Bindable<string>(string.Empty);

        private OutlinedTextBox textBox = null!;

        public RoundBeatmapPickEditor(TournamentRound round, RoundBeatmap beatmap)
        {
            this.round = round;
            this.beatmap = beatmap;

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
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    new OsuCheckbox
                    {
                        LabelText = "Pick",
                        Current = hasCustomPick,
                    },
                    textBox = new OutlinedTextBox
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 40,
                    },
                }
            };

            textBox.Current = pickText;

            pickText.Value = beatmap.Pick ?? beatmap.GetInferredPick(round);
            hasCustomPick.Value = beatmap.Pick != null;

            hasCustomPick.BindValueChanged(e =>
            {
                if (e.NewValue)
                {
                    // Switching on always starts from the inferred label; an empty value only
                    // becomes an explicit override after the user clears the text box.
                    pickText.Value = beatmap.GetInferredPick(round);
                    beatmap.Pick = pickText.Value;
                }
                else
                {
                    beatmap.Pick = null;
                    pickText.Value = beatmap.GetInferredPick(round);
                }

                updateTextState();
            });

            pickText.BindValueChanged(text =>
            {
                if (hasCustomPick.Value)
                    beatmap.Pick = text.NewValue;
            });

            updateTextState();

            round.Beatmaps.CollectionChanged += onBeatmapsChanged;
        }

        /// <summary>
        /// Refreshes the displayed inferred label (used when the round order or another map's
        /// classification label changes).
        /// </summary>
        public void Refresh()
        {
            if (!IsLoaded)
                return;

            if (!hasCustomPick.Value)
                pickText.Value = beatmap.GetInferredPick(round);

            updateTextState();
        }

        private void updateTextState()
        {
            textBox.ReadOnly = !hasCustomPick.Value;
            textBox.Alpha = textBox.ReadOnly ? 0.3f : 1;
        }

        private void onBeatmapsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Schedule(Refresh);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
                round.Beatmaps.CollectionChanged -= onBeatmapsChanged;
        }
    }
}
