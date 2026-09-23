// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Threading;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// Shared referee countdown driven by the <c>/timer</c> chat command.
    /// Cached in <see cref="TournamentSceneManager"/> so both control-panel send boxes
    /// share a single countdown: starting a new one preempts the previous run.
    /// Ticks on the update-thread <see cref="Scheduler"/>, never blocking anything else.
    /// Progress is posted as local <see cref="InfoMessage"/>s to the shared room channel
    /// (same mechanism as /roll results), so every chat display shows it.
    /// </summary>
    public partial class ChatTimerService : Component
    {
        [Resolved]
        private ChannelManager? channelManager { get; set; }

        private Channel channel;

        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        private ScheduledDelegate? ticker;
        private double endTime;
        private int lastRemaining = -1;

        public bool IsRunning => ticker?.Completed == false;

        /// <summary>
        /// Remaining seconds of the running countdown, or -1 when there is none.
        /// Bound by the chat send boxes to show a countdown readout.
        /// </summary>
        public readonly BindableInt RemainingSeconds = new BindableInt(-1);

        /// <summary>
        /// Starts a countdown of <paramref name="seconds"/> (seconds &gt; 0),
        /// or just cancels the running one when <paramref name="seconds"/> is 0.
        /// A running countdown is always cancelled first with a <c>Countdown aborted</c> chat message.
        /// </summary>
        public void StartTimer(int seconds, Channel channel)
        {
            this.channel = channel;
            if (IsRunning)
            {
                cancelInternal();
                postToChat("Countdown aborted");
            }

            if (seconds <= 0)
                return;

            postToChat($"Countdown: {seconds / 60:D2}:{seconds % 60:D2}");

            endTime = Time.Current + seconds * 1000;
            lastRemaining = seconds;
            RemainingSeconds.Value = seconds;
            ticker = Scheduler.AddDelayed(tick, 1000, true);
        }

        private void tick()
        {
            int remaining = (int)System.Math.Ceiling((endTime - Time.Current) / 1000);

            if (remaining >= lastRemaining)
                return;

            lastRemaining = remaining;

            if (remaining <= 0)
            {
                cancelInternal();
                postToChat("Countdown finished");
                notify("Countdown finished");
                return;
            }

            RemainingSeconds.Value = remaining;

            if (remaining <= 5 || remaining == 10)
                postToChat($"Countdown: {remaining}");
            else if (remaining % 30 == 0)
                postToChat($"Countdown: {remaining / 60}:{remaining % 60:D2}");
        }

        private void cancelInternal()
        {
            ticker?.Cancel();
            ticker = null;
            RemainingSeconds.Value = -1;
        }

        private void postToChat(string text)
        {
            var room = client.Room;

            if (room == null)
                return;

            channelManager?.PostMessage(text, true, channel);
        }

        private void notify(string text)
        {
            if (notifications != null)
                notifications.Post(new SimpleNotification { Text = text });
            else
                postToChat(text);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            cancelInternal();
        }
    }
}
