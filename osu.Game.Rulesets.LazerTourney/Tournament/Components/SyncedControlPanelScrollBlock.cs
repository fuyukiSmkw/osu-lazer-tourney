// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.Countdown;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.OnlinePlay.Multiplayer.Match;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components
{
    /// <summary>
    /// Scrollable block below the existing control-panel content.
    /// One instance is hosted in each of <c>MapPoolScreen</c> and <c>GameplayScreen</c>;
    /// both show the same live room data (participants + beatmap queue) and stay in sync
    /// because they bind to the shared <see cref="osu.Game.Online.Multiplayer.MultiplayerClient"/> state.
    /// </summary>
    /// <remarks>
    /// The lists mirror the corresponding columns of <c>RoomScreen</c>:
    /// <see cref="TournamentParticipantsList"/> and <see cref="TournamentQueueList"/>
    /// (download + delete buttons, no edit, no add button).
    /// Each lives in an <see cref="EditorSidebarSection"/> (the sidebar sections of the editor).
    /// Both lists size to their content; the outer control-panel scroll container handles overflow.
    /// The match buttons mirror <c>MatchStartControl</c>'s referee actions and the countdown
    /// button offers the official start delays; tick sounds come from the shared
    /// <see cref="MatchStartCountdownSounds"/>.
    /// </remarks>
    public partial class SyncedControlPanelScrollBlock : CompositeDrawable
    {
        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        private TourneyButton startButton = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5f),
                Children = new Drawable[]
                {
                    new EditorSidebarSection("Players")
                    {
                        Children = new Drawable[]
                        {
                            new TournamentParticipantsList
                            {
                                RelativeSizeAxes = Axes.X,
                            },
                        },
                    },
                    new EditorSidebarSection("Queue")
                    {
                        Children = new Drawable[]
                        {
                            new TournamentQueueList
                            {
                                RelativeSizeAxes = Axes.X,
                            },
                        },
                    },
                    new EditorSidebarSection("Match")
                    {
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(5),
                                Children = new Drawable[]
                                {
                                    startButton = new TourneyButton
                                    {
                                        Width = 0.8f,
                                        Height = 40,
                                        Text = "",
                                        Action = onStartButtonClick,
                                    },
                                    new MultiplayerCountdownButton
                                    {
                                        Width = 0.2f,
                                        Size = new Vector2(40, 40),
                                        Action = startMatchDelayed,
                                        CancelAction = cancelCountdown,
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        private void onStartButtonClick()
        {
            if (client.Room == null || !(client.IsReferee || client.IsHost))
                return;
            if (client.Room.State is not MultiplayerRoomState.WaitingForLoad and not MultiplayerRoomState.Playing)
            {
                startMatch();

            }
            else
                abortMatch();
        }

        /// <summary>
        /// Starts the match immediately. Mirrors <c>MatchStartControl</c>'s referee start
        /// (same call as the <c>/start</c> chat command).
        /// </summary>
        private void startMatch()
        {
            if (client.Room == null)
                return;

            client.StartMatch().FireAndForget(onError: ex => notify($"Failed to start match: {ex.Message}"));
        }

        /// <summary>
        /// Starts the match after <paramref name="delay"/>. Mirrors <c>MatchStartControl.startCountdown</c>.
        /// </summary>
        private void startMatchDelayed(TimeSpan delay)
        {
            if (client.Room == null || !(client.IsReferee || client.IsHost))
                return;
            if (client.Room.State is MultiplayerRoomState.WaitingForLoad or MultiplayerRoomState.Playing)
                return;

            client.SendMatchRequest(new StartMatchCountdownRequest { Duration = delay })
                  .FireAndForget(onError: ex => notify($"Failed to start match: {ex.Message}"));
        }

        /// <summary>
        /// Cancels the running start countdown. Mirrors <c>MatchStartControl.cancelCountdown</c>.
        /// </summary>
        private void cancelCountdown()
        {
            var countdown = client.Room?.ActiveCountdowns.OfType<MatchStartCountdown>().SingleOrDefault();

            if (client.Room == null || countdown == null)
                return;

            client.SendMatchRequest(new StopCountdownRequest(countdown.ID))
                  .FireAndForget(onError: ex => notify($"Failed to stop countdown: {ex.Message}"));
        }

        /// <summary>
        /// Aborts the running match. Mirrors <c>MatchStartControl</c>'s referee abort
        /// (without the confirm dialog; same call as the <c>/abort</c> chat command).
        /// No-op unless a match is running.
        /// </summary>
        private void abortMatch()
        {
            if (client.Room?.State is not MultiplayerRoomState.WaitingForLoad and not MultiplayerRoomState.Playing)
                return;

            client.AbortMatch().FireAndForget(onError: ex => notify($"Failed to abort match: {ex.Message}"));
        }

        private void notify(string text)
        {
            if (notifications != null)
                notifications.Post(new SimpleNotification { Text = text });
            else
                Logger.Log(text, LoggingTarget.Runtime, LogLevel.Important);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            client.RoomUpdated += updateStartButton;
            updateStartButton();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            client?.RoomUpdated -= updateStartButton;
        }

        private void updateStartButton()
        {
            var room = client?.Room;
            if (room == null)
            {
                startButton.Text = "No room";
                startButton.BackgroundColour = new OsuColour().GreyCarmine;
                return;
            }

            var localUser = client.LocalUser;

            int countReady = room.Users.Count(u => u.Role == MultiplayerRoomUserRole.Player && u.State == MultiplayerUserState.Ready);
            int countTotal = room.Users.Count(u => u.Role == MultiplayerRoomUserRole.Player && u.State != MultiplayerUserState.Spectating);
            MultiplayerCountdown? countdown = room.ActiveCountdowns.SingleOrDefault(c => c is MatchStartCountdown);
            string? countdownText = countdown != null ? $"Starting scheduled" : null;

            if (client.IsReferee || client.IsHost)
            {
                if (room.State == MultiplayerRoomState.Open)
                {
                    startButton.Text = countReady == 0 ? $"Waiting for players..." : $"{countdownText ?? "Start match"}";
                    startButton.BackgroundColour = new OsuColour().Green;
                }
                else
                {
                    startButton.Text = "Abort match";
                    startButton.BackgroundColour = new OsuColour().Red;
                }
            }
            else
            {
                startButton.Text = "You can't start";
                startButton.BackgroundColour = new OsuColour().GreyCarmine;
            }
        }
    }
}
