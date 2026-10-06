// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.LazerTourney.Tournament.Components;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay.Components;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Countdown
{
    public partial class CountdownScreen : BeatmapInfoScreen
    {
        [Resolved]
        private CountdownTimerService timerService { get; set; } = null!;

        private OsuSpriteText countdownText = null!;
        private TourneyButton startPauseButton = null!;
        private int lastDisplayedSecond = -1;

        [BackgroundDependencyLoader]
        private void load()
        {
            AddRangeInternal(new Drawable[]
            {
                new TourneyVideo("countdown")
                {
                    Loop = true,
                    RelativeSizeAxes = Axes.Both,
                },
                new MatchHeader
                {
                    ShowLogo = true,
                    ShowScores = false,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = countdownText = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = OsuFont.TorusAlternate.With(size: 100, weight: FontWeight.Bold, fixedWidth: true),
                    },
                },
                new ControlPanel
                {
                    Children = new Drawable[]
                    {
                        new CountdownTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            Current = timerService.RemainingSeconds,
                        },
                        startPauseButton = new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Start",
                            Action = timerService.Toggle,
                        },
                    },
                },
            });

            timerService.Running.BindValueChanged(v => startPauseButton.Text = v.NewValue ? "Pause" : "Start", true);
            updateText(true);

            SongBar.Expanded = true;
        }

        public override void Show()
        {
            base.Show();
            // Snap to live progress after being away: hidden screens skip updates.
            updateText(true);
        }

        protected override void Update()
        {
            base.Update();
            updateText(false);
        }

        private void updateText(bool force)
        {
            int second = (int)Math.Ceiling(timerService.RemainingSeconds.Value);

            if (!force && second == lastDisplayedSecond)
                return;

            lastDisplayedSecond = second;
            countdownText.Text = $"{second / 60:D2}:{second % 60:D2}";
        }

        /// <summary>
        /// Edits the remaining time. Displays mm:ss, accepts mm:ss or total seconds.
        /// </summary>
        private partial class CountdownTextBox : SettingsTextBox
        {
            public new BindableDouble? Current
            {
                get;
                set
                {
                    if (value == null)
                        return;

                    field.UnbindBindings();
                    field.BindTo(value);
                }
            } = new BindableDouble(CountdownTimerService.DEFAULT_SECONDS);

            public CountdownTextBox()
            {
                base.Current = new Bindable<string>(string.Empty);

                Current.BindValueChanged(_ => showCurrent(), true);

                ((OsuTextBox)Control).OnCommit += (sender, _) =>
                {
                    if (tryParse(sender.Text, out double seconds))
                        Current.Value = Math.Max(0, seconds);
                    else
                        showCurrent();
                };
            }

            private void showCurrent()
            {
                // Never overwrite while the user is editing.
                if (((OsuTextBox)Control).HasFocus)
                    return;

                int second = (int)Math.Ceiling(Current.Value);
                base.Current.Value = $"{second / 60:D2}:{second % 60:D2}";
            }

            private static bool tryParse(string text, out double seconds)
            {
                seconds = 0;

                text = text.Trim();

                if (text.Contains(':'))
                {
                    string[] parts = text.Split(':');

                    if (parts.Length != 2
                        || !double.TryParse(parts[0], out double minutes)
                        || !double.TryParse(parts[1], out double secs)
                        || minutes < 0 || secs < 0 || secs >= 60)
                        return false;

                    seconds = minutes * 60 + secs;
                    return true;
                }

                return double.TryParse(text, out seconds) && seconds >= 0;
            }
        }
    }
}
