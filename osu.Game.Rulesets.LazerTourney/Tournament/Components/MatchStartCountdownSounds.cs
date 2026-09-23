// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Threading;
using osu.Game.Online.Multiplayer;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// Plays the match-start countdown tick sounds.
    /// Audio-only port of <c>MultiplayerReadyButton</c>'s countdown logic: the tournament
    /// screens don't host that button, so without this the server countdown would be silent.
    /// Cached in <see cref="TournamentSceneManager"/> as a singleton so the ticks play exactly
    /// once no matter which screen is shown.
    /// </summary>
    public partial class MatchStartCountdownSounds : Component
    {
        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        private Sample? countdownTickSample;
        private Sample? countdownWarnSample;
        private Sample? countdownWarnFinalSample;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            countdownTickSample = audio.Samples.Get(@"Multiplayer/countdown-tick");
            countdownWarnSample = audio.Samples.Get(@"Multiplayer/countdown-warn");
            countdownWarnFinalSample = audio.Samples.Get(@"Multiplayer/countdown-warn-final");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            client.RoomUpdated += onRoomUpdated;
            onRoomUpdated();
        }

        private MultiplayerCountdown? countdown;
        private double countdownChangeTime;
        private ScheduledDelegate? countdownUpdateDelegate;

        private void onRoomUpdated() => Scheduler.AddOnce(() =>
        {
            MultiplayerCountdown? newCountdown = client.Room?.ActiveCountdowns.SingleOrDefault(c => c is MatchStartCountdown);

            if (newCountdown != countdown)
            {
                countdown = newCountdown;
                countdownChangeTime = Time.Current;
            }

            scheduleNextCountdownUpdate();
        });

        private void scheduleNextCountdownUpdate()
        {
            countdownUpdateDelegate?.Cancel();

            if (countdown != null)
            {
                // The remaining time on a countdown may be at a fractional portion between two seconds.
                // We want to align certain audio/visual cues to the point at which integer seconds change.
                // To do so, we schedule to the next whole second. Note that scheduler invocation isn't
                // guaranteed to be accurate, so this may still occur slightly late, but even in such a case
                // the next invocation will be roughly correct.
                double timeToNextSecond = countdownTimeRemaining.TotalMilliseconds % 1000;

                countdownUpdateDelegate = Scheduler.AddDelayed(onCountdownTick, timeToNextSecond);
            }
            else
            {
                countdownUpdateDelegate?.Cancel();
                countdownUpdateDelegate = null;
            }

            void onCountdownTick()
            {
                int secondsRemaining = (int)countdownTimeRemaining.TotalSeconds;

                playTickSound(secondsRemaining);

                if (secondsRemaining > 0)
                    scheduleNextCountdownUpdate();
            }
        }

        private void playTickSound(int secondsRemaining)
        {
            if (secondsRemaining < 10) countdownTickSample?.Play();

            if (secondsRemaining <= 3)
            {
                if (secondsRemaining > 0)
                    countdownWarnSample?.Play();
                else
                    countdownWarnFinalSample?.Play();
            }
        }

        private TimeSpan countdownTimeRemaining
        {
            get
            {
                Debug.Assert(countdown != null);

                double timeElapsed = Time.Current - countdownChangeTime;
                TimeSpan remaining;

                if (timeElapsed > countdown.TimeRemaining.TotalMilliseconds)
                    remaining = TimeSpan.Zero;
                else
                    remaining = countdown.TimeRemaining - TimeSpan.FromMilliseconds(timeElapsed);

                return remaining;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (client.IsNotNull())
                client.RoomUpdated -= onRoomUpdated;
        }
    }
}
