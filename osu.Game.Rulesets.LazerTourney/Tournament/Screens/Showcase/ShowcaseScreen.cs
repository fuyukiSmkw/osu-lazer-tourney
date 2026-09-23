// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.LazerTourney.Tournament.Components;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Online;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay.Components;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.Timing;
using osu.Game.Screens.Play.PlayerSettings;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Showcase
{
    /// <summary>
    /// Showcase screen: plays local <c>.osr</c> replays dropped onto the window.
    /// Idle shows a spectate-style idle view; a dropped replay is imported silently
    /// (downloading its beatmap first when missing) and played in an embedded
    /// <see cref="OsuScreenStack"/> via <see cref="TournamentReplayPlayerLoader"/>.
    /// The result screen is kept until the next replay is dropped or reset is pressed.
    /// </summary>
    /// <remarks>
    /// Drag-drop interception mirrors <c>SkinEditor</c>: registered as an import handler
    /// (first in line) only while this screen is shown. <c>OsuGameBase.Import</c> awaits
    /// handlers sequentially, so the trailing <c>ScoreImporter</c> run always sees our
    /// already-imported duplicate and skips (aside from a brief completion toast it posts itself).
    /// </remarks>
    public partial class ShowcaseScreen : BeatmapInfoScreen, ICanAcceptFiles
    {
        public IEnumerable<string> HandledExtensions => new[] { ".osr" };

        [Resolved]
        private OsuGame? osuGame { get; set; }

        [Resolved]
        private ScoreManager scores { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private TournamentOnlineState onlineState { get; set; } = null!;

        [Resolved]
        private LadderInfo ladder { get; set; } = null!;

        [Resolved]
        private TournamentGameBase tournamentGame { get; set; } = null!;

        [Resolved]
        private MusicController music { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        private readonly ShowcaseReplayController replayController = new ShowcaseReplayController();

        private BeatmapModelDownloader beatmapDownloader = null!;
        private SeasonalBackgroundLoader backgroundLoader = null!;

        private Container idleLayer = null!;
        private Container playerLayer = null!;
        private Container? playerContainer;
        private OsuScreenStack? playerStack;
        private FillFlowContainer transportSection = null!;

        private int importGeneration;
        private IDisposable? beatmapArrivalSubscription;
        private TaskCompletionSource<bool>? pendingArrivalSource;

        [BackgroundDependencyLoader]
        private void load()
        {
            // Silent downloader: PostNotification is never set (RankedPlay precedent).
            beatmapDownloader = new BeatmapModelDownloader(beatmaps, api);

            var backgrounds = backgroundLoader = new SeasonalBackgroundLoader();

            AddRangeInternal(new Drawable[]
            {
                new TournamentLogo(),
                new TourneyVideo("showcase")
                {
                    Loop = true,
                    RelativeSizeAxes = Axes.Both,
                },
                new Container
                {
                    Padding = new MarginPadding { Bottom = SongBar.HEIGHT },
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        idleLayer = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = new SpectateCell(backgrounds)
                            {
                                // Idle look exactly like a gameplay spectate cell with no play.
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                            },
                        },
                        playerLayer = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                    }
                },
                backgroundLoader,
                new ControlPanel
                {
                    Children = new Drawable[]
                    {
                        new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Reset",
                            Action = resetToIdle,
                        },
                        new EditorSidebarSection("Replay")
                        {
                            Children = new Drawable[]
                            {
                                transportSection = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 5f),
                                    Children = new Drawable[]
                                    {
                                        new SettingsSlider<double>
                                        {
                                            LabelText = "Playback rate",
                                            Current = replayController.Rate,
                                        },
                                        new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Horizontal,
                                            Spacing = new Vector2(3, 0),
                                            Children = new Drawable[]
                                            {
                                                new SeekButton
                                                {
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    Icon = FontAwesome.Solid.FastBackward,
                                                    Action = () => replayController.SeekSeconds(-5),
                                                    TooltipText = "-5s",
                                                },
                                                new SeekButton
                                                {
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    Icon = FontAwesome.Solid.Backward,
                                                    Action = () => replayController.SeekSeconds(-1),
                                                    TooltipText = "-1s",
                                                },
                                                new SeekButton
                                                {
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    Icon = FontAwesome.Solid.StepBackward,
                                                    Action = () => replayController.StepFrame(-1),
                                                    TooltipText = "-1 frame",
                                                },
                                                new IconButton
                                                {
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    Scale = new Vector2(1.4f),
                                                    IconScale = new Vector2(1.4f),
                                                    Icon = FontAwesome.Regular.PauseCircle,
                                                    Action = () => replayController.TogglePause(),
                                                },
                                                new SeekButton
                                                {
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    Icon = FontAwesome.Solid.StepForward,
                                                    Action = () => replayController.StepFrame(1),
                                                    TooltipText = "+1 frame",
                                                },
                                                new SeekButton
                                                {
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    Icon = FontAwesome.Solid.Forward,
                                                    Action = () => replayController.SeekSeconds(1),
                                                    TooltipText = "+1s",
                                                },
                                                new SeekButton
                                                {
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    Icon = FontAwesome.Solid.FastForward,
                                                    Action = () => replayController.SeekSeconds(5),
                                                    TooltipText = "+5s",
                                                },
                                            },
                                        }
                                        /*
                                        new TourneyButton
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Text = "Pause / resume",
                                            Action = () => replayController.TogglePause(),
                                        },
                                        new TourneyButton
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Text = "-10s",
                                            Action = () => replayController.SeekSeconds(-10),
                                        },
                                        new TourneyButton
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Text = "-1s",
                                            Action = () => replayController.SeekSeconds(-1),
                                        },
                                        new TourneyButton
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Text = "+1s",
                                            Action = () => replayController.SeekSeconds(1),
                                        },
                                        new TourneyButton
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Text = "+10s",
                                            Action = () => replayController.SeekSeconds(10),
                                        },*/
                                    },
                                },
                            },
                        },
                        new VisualSettings(),
                        new AudioSettings(),
                    },
                },
            });

            replayController.HasPlayer.BindValueChanged(v => transportSection.Alpha = v.NewValue ? 1 : 0.4f, true);
        }

        private partial class SeekButton : IconButton
        {
            public SeekButton()
            {
                AddInternal(new RepeatingButtonBehaviour(this));
            }
        }

        protected override void CurrentMatchChanged(ValueChangedEvent<TournamentMatch?> match)
        {
            // showcase screen doesn't care about a match being selected.
            // base call intentionally omitted to not show match warning.
        }

        public override void Show()
        {
            base.Show();
            osuGame?.RegisterImportHandler(this);
        }

        public override void Hide()
        {
            osuGame?.UnregisterImportHandler(this);

            // Abandon any in-flight import/download; pause playback (hidden subtrees freeze clocks).
            Interlocked.Increment(ref importGeneration);
            cancelPendingArrival();
            replayController.Player?.Pause();

            base.Hide();
        }

        public Task Import(params string[] paths) => handleDropAsync(paths);

        public Task Import(ImportTask[] tasks, ImportParameters parameters = default) =>
            handleDropAsync(tasks.Select(t => t.Path));

        /// <summary>
        /// Handles dropped files: first <c>.osr</c> wins and supersedes anything in progress.
        /// Runs on a background thread (awaited sequentially before ScoreImporter's own run).
        /// </summary>
        private Task handleDropAsync(IEnumerable<string> paths)
        {
            string? osr = paths.FirstOrDefault(p =>
                Path.GetExtension(p).Equals(".osr", StringComparison.OrdinalIgnoreCase) && File.Exists(p));

            if (osr == null)
                return Task.CompletedTask;

            int generation = Interlocked.Increment(ref importGeneration);
            cancelPendingArrival();

            return Task.Run(async () => await importAndPlayAsync(osr, generation).ConfigureAwait(false));
        }

        private async Task importAndPlayAsync(string path, int generation)
        {
            byte[] bytes;

            try
            {
                bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                notify($"Could not read replay file: {ex.Message}");
                return;
            }

            bool stale() => generation != importGeneration;

            // Parse first: succeeds only when the beatmap is present locally.
            ScoreInfo? info = null;
            string? missingHash = null;

            try
            {
                info = parseScore(bytes);
            }
            catch (LegacyScoreDecoder.BeatmapNotFoundException ex)
            {
                missingHash = ex.Hash;
            }
            catch (Exception ex)
            {
                notify($"Could not read replay file: {ex.Message}");
                return;
            }

            if (missingHash != null)
            {
                // Beatmap missing: download silently, then re-parse (mirror MissingBeatmapNotification, minus notifications).
                if (!await downloadBeatmapAsync(missingHash, generation).ConfigureAwait(false))
                    return;

                if (stale())
                    return;

                try
                {
                    info = parseScore(bytes);
                }
                catch (Exception ex)
                {
                    notify($"Could not read replay file: {ex.Message}");
                    return;
                }
            }

            if (stale() || info == null)
                return;

            // Silent database import with our own never-posted notification.
            // NOTE: the returned Live<> handles are only dereferenced on the update thread below;
            // realm-managed objects throw when touched from a background thread.
            Live<ScoreInfo>? live;

            try
            {
                using (var stream = new MemoryStream(bytes, false))
                {
                    var ownNotification = new ProgressNotification { State = ProgressNotificationState.Active };
                    var lives = await scores.Import(ownNotification, new[] { new ImportTask(stream, Path.GetFileName(path)) }).ConfigureAwait(false);
                    live = lives.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                notify($"Could not import replay: {ex.Message}");
                return;
            }

            if (stale() || live == null)
            {
                if (!stale())
                    notify("Could not import replay.");
                return;
            }

            Schedule(() =>
            {
                if (generation != importGeneration)
                    return;

                ScoreInfo? imported;

                try
                {
                    imported = live.Value;
                }
                catch (Exception ex)
                {
                    notify($"Could not import replay: {ex.Message}");
                    return;
                }

                if (imported == null)
                {
                    notify("Could not import replay.");
                    return;
                }

                var score = scores.GetScore(imported);

                if (score?.Replay == null)
                {
                    notify("Could not load replay data.");
                    return;
                }

                playScore(score);
            });
        }

        private ScoreInfo? parseScore(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes, false))
                return new DatabasedLegacyScoreDecoder(rulesets, beatmaps).Parse(stream).ScoreInfo;
        }

        /// <summary>
        /// Looks up the beatmap set for <paramref name="md5Hash"/> and downloads it silently,
        /// waiting until it arrives locally. Returns false when superseded or on failure.
        /// </summary>
        private async Task<bool> downloadBeatmapAsync(string md5Hash, int generation)
        {
            bool stale() => generation != importGeneration;

            var lookupSource = new TaskCompletionSource<APIBeatmap?>();
            var lookup = new GetBeatmapRequest(new BeatmapInfo { MD5Hash = md5Hash });
            lookup.Success += res => lookupSource.TrySetResult(res);
            lookup.Failure += _ => lookupSource.TrySetResult(null);
            api.Queue(lookup);

            var beatmap = await lookupSource.Task.ConfigureAwait(false);

            if (stale())
                return false;

            var set = beatmap?.BeatmapSet ?? (beatmap != null ? new APIBeatmapSet { OnlineID = beatmap.OnlineBeatmapSetID } : null);

            if (set == null)
            {
                notify("Could not find the replay's beatmap online.");
                return false;
            }

            var arrivalSource = new TaskCompletionSource<bool>();
            pendingArrivalSource = arrivalSource;

            if (isBeatmapPresent(md5Hash))
                return true;

            IDisposable? subscription = null;

            subscription = realm.RegisterForNotifications<BeatmapSetInfo>(
                r => r.All<BeatmapSetInfo>().Where(s => !s.DeletePending),
                (sender, changes) =>
                {
                    if (changes?.InsertedIndices == null)
                        return;

                    if (sender.Any(s => s.Beatmaps.Any(b => b.MD5Hash == md5Hash)))
                    {
                        subscription?.Dispose();
                        beatmapArrivalSubscription = null;
                        arrivalSource.TrySetResult(true);
                    }
                });

            beatmapArrivalSubscription = subscription;
            beatmapDownloader.DownloadFailed += onDownloadFailed;

            // ModelDownloader must be driven from the update thread (official callers all do).
            Schedule(() =>
            {
                if (!stale())
                    beatmapDownloader.Download(set);
            });

            bool arrived = await arrivalSource.Task.ConfigureAwait(false);

            beatmapDownloader.DownloadFailed -= onDownloadFailed;

            if (stale())
                return false;

            if (!arrived)
                notify("Could not download the replay's beatmap.");

            return arrived;

            void onDownloadFailed(ArchiveDownloadRequest<IBeatmapSetInfo> _)
            {
                subscription?.Dispose();
                beatmapArrivalSubscription = null;
                arrivalSource.TrySetResult(false);
            }
        }

        private bool isBeatmapPresent(string md5Hash) =>
            realm.Run(r => r.All<BeatmapSetInfo>().Any(s => !s.DeletePending && s.Beatmaps.Any(b => b.MD5Hash == md5Hash)));

        private void cancelPendingArrival()
        {
            beatmapArrivalSubscription?.Dispose();
            beatmapArrivalSubscription = null;
            pendingArrivalSource?.TrySetResult(false);
            pendingArrivalSource = null;
        }

        /// <summary>
        /// Starts playback: points globals at the score (mirroring OsuGame.PresentScore),
        /// then pushes the loader onto the embedded stack with an idle crossfade.
        /// </summary>
        private void playScore(Score score)
        {
            teardownPlayer();

            var rulesetInfo = rulesets.GetRuleset(score.ScoreInfo.Ruleset.OnlineID);

            if (rulesetInfo != null)
                tournamentGame.Ruleset.Value = rulesetInfo;

            var local = beatmaps.QueryBeatmap(b => b.MD5Hash == score.ScoreInfo.BeatmapHash)
                        ?? beatmaps.QueryBeatmap(b => b.OnlineID == score.ScoreInfo.BeatmapInfo.OnlineID);

            if (local != null)
                tournamentGame.Beatmap.Value = beatmaps.GetWorkingBeatmap(local);

            tournamentGame.Mods.Value = score.ScoreInfo.Mods;

            var working = beatmaps.GetWorkingBeatmap(score.ScoreInfo.BeatmapInfo);

            var isolation = new PlayerIsolationContainer(working, score.ScoreInfo.Ruleset, score.ScoreInfo.Mods)
            {
                RelativeSizeAxes = Axes.Both,
                Child = playerStack = new OsuScreenStack(),
            };

            var container = playerContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                Child = isolation,
                Masking = true,
            };

            playerLayer.Add(container);

            playerStack.Push(new TournamentReplayPlayerLoader(score, replayController));

            idleLayer.FadeOut(300);
            container.FadeIn(300);
        }

        /// <summary>
        /// Resets to idle: drops any in-flight import/download, clears the player,
        /// and restores global beatmap/music (room item when joined, menu default otherwise).
        /// </summary>
        private void resetToIdle()
        {
            Interlocked.Increment(ref importGeneration);
            cancelPendingArrival();
            teardownPlayer();

            idleLayer.FadeIn(300);

            var item = onlineState.RoomJoined.Value ? onlineState.CurrentPlaylistItem.Value : null;

            if (item == null)
            {
                tournamentGame.Beatmap.Value = beatmaps.GetWorkingBeatmap(null);
                music.EnsurePlayingSomething();
                return;
            }

            var rulesetInfo = rulesets.GetRuleset(item.RulesetID) ?? ladder.Ruleset.Value;

            if (rulesetInfo != null)
            {
                tournamentGame.Ruleset.Value = rulesetInfo;
                tournamentGame.Mods.Value = RoundBeatmap.InstantiateMods(item.RequiredMods.ToArray(), rulesetInfo.CreateInstance());
            }

            var local = beatmaps.QueryBeatmap(b => b.OnlineID == item.BeatmapID);
            tournamentGame.Beatmap.Value = local == null ? beatmaps.GetWorkingBeatmap(null) : beatmaps.GetWorkingBeatmap(local);
            music.EnsurePlayingSomething();
        }

        private void teardownPlayer()
        {
            if (playerContainer != null)
            {
                // FadeOut-then-Expire (GameplayScreen rebuildLayout precedent).
                playerContainer.FadeOut(200);
                playerContainer.Expire();
                playerContainer = null;
            }

            playerStack = null;
        }

        private void notify(string text)
        {
            Schedule(() =>
            {
                if (notifications != null)
                    notifications.Post(new SimpleNotification { Text = text });
                else
                    Logger.Log(text, LoggingTarget.Runtime, LogLevel.Important);
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            importGeneration++;
            cancelPendingArrival();

            if (osuGame != null)
                osuGame.UnregisterImportHandler(this);
        }
    }
}
