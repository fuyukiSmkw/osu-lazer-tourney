// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.LazerTourney.Tournament.IO;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay.Components;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Utils;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Online
{
    /// <summary>
    /// Minimal WebSocket server exposing tournament spectate data in a tosu-compatible format.
    /// The port is configurable (default 50727, avoiding tosu's default 24050).
    /// </summary>
    public partial class TourneyDataWebSocketServer : Component
    {
        public const int DEFAULT_PORT = 50727;
        public const string PATH = "/ws";

        /// <summary>
        /// URL of the repository's sample ws v1 overlay (linked from Setup and About).
        /// </summary>
        public const string SAMPLE_OVERLAY_URL = "https://github.com/fuyukiSmkw/osu-lazer-tourney/tree/main/tourney-overlay-test";

        /// <summary>
        /// Serves the current beatmap's background image, mirroring tosu's endpoint
        /// that overlays build via <c>{host}/backgroundImage?mapset=&amp;dummyData=&amp;…</c>.
        /// Query parameters are accepted but ignored; the current global beatmap is served.
        /// </summary>
        public const string BACKGROUND_PATH = "/backgroundImage";

        private const int broadcast_interval_ms = 100; // 10Hz update rate

        private HttpListener? httpListener;
        private readonly ConcurrentDictionary<string, WebSocket> clients = new();
        private CancellationTokenSource? cancellationTokenSource;
        private ScheduledDelegate? broadcastDelegate;
        private ScheduledDelegate? restartDelegate;
        private int listeningPort;
        private bool broadcasting;

        [Resolved]
        private TournamentStorage tournamentStorage { get; set; } = null!;

        [Resolved]
        private TournamentOnlineState onlineState { get; set; } = null!;

        [Resolved]
        private SpectateSession spectateSession { get; set; } = null!;

        [Resolved]
        private Bindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> globalMods { get; set; } = null!;

        [Resolved]
        private LadderInfo ladder { get; set; } = null!;

        [Resolved]
        private TournamentMusicController musicController { get; set; } = null!;

        [Resolved]
        private MusicController music { get; set; } = null!;

        [Resolved]
        private ChannelManager? channelManager { get; set; }

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        private Channel? chatChannel;
        private long chatChannelId = -1;

        private string? difficultyCacheKey;
        private double cachedStarRating;
        private CancellationTokenSource? difficultyCancellation;

        /// <summary>
        /// Per-player live values (PP/UR/hit errors). Every broadcast tick snapshots each
        /// player's processor cheaply and only runs the expensive performance calculation
        /// for players whose inputs moved — mirroring the in-game PP counter, which hooks
        /// the processor directly. The per-player live PP therefore keeps pace with the
        /// score instead of trailing it.
        /// </summary>
        private sealed class PlayerLiveData
        {
            public PerformanceCalculator? Calculator;
            public Ruleset? RulesetInstance;
            public readonly ScoreInfo Snapshot = new ScoreInfo();
            public double PP;
            public double FcPP;
            public double MaxPP;
            public double UR;
            public double[] HitErrors = Array.Empty<double>();
            /// <summary>Cloned mods used for the snapshot and difficulty lookups.</summary>
            public Mod[] Mods = Array.Empty<Mod>();
            // Change-detection fingerprints from the last processed tick.
            public long LastScore = -1;
            public double LastAccuracy = -1;
            public int LastCombo = -1;
            public int LastMaxCombo = -1;
            public int LastHitEventCount = -1;
            /// <summary>Miss count the maxThisPlay estimate was last computed for.</summary>
            public int LastMaxPPMiss = -1;
            /// <summary>Whether the full-combo estimate has been computed for this entry.</summary>
            public bool FcComputed;
        }

        private readonly Dictionary<int, PlayerLiveData> liveData = new Dictionary<int, PlayerLiveData>();
        private object? liveDataProvider;

        private readonly Dictionary<string, List<TimedDifficultyAttributes>> timedAttributesCache = new Dictionary<string, List<TimedDifficultyAttributes>>();
        private readonly HashSet<string> timedAttributesRequested = new HashSet<string>();

        private readonly object backgroundLock = new object();
        private string? backgroundCacheKey;
        private byte[]? backgroundCacheBytes;
        private string? backgroundCacheContentType;

        // Property names are written exactly as declared below, mirroring tosu's
        // field names (notably the all-caps beatmap stats CS/AR/OD/HP/SR/BPM,
        // which a camelCase policy would mangle into cs/sr/bpm and break overlays).
        private readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            broadcasting = true;
            startServer(normalisePort(tournamentStorage.WebSocketPort.Value));

            tournamentStorage.WebSocketPort.BindValueChanged(onWebSocketPortChanged);

            // Start periodic broadcast.
            broadcastDelegate = Scheduler.AddDelayed(broadcastLoop, broadcast_interval_ms);
        }

        private void onWebSocketPortChanged(ValueChangedEvent<int> e)
        {
            if (IsDisposed)
                return;

            Schedule(() =>
            {
                if (IsDisposed)
                    return;

                // Debounce so typing a port does not repeatedly tear down the server.
                restartDelegate?.Cancel();
                restartDelegate = Scheduler.AddDelayed(() => restartServer(normalisePort(e.NewValue)), 750);
            });
        }

        private void broadcastLoop()
        {
            if (!broadcasting)
                return;

            BroadcastState();

            broadcastDelegate = Scheduler.AddDelayed(broadcastLoop, broadcast_interval_ms);
        }

        private void restartServer(int port)
        {
            if (port == listeningPort && httpListener?.IsListening == true)
                return;

            stopServer();
            startServer(port);
        }

        private void startServer(int port)
        {
            HttpListener? listener = null;

            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                listener.Start();

                var cts = new CancellationTokenSource();

                httpListener = listener;
                cancellationTokenSource = cts;
                listeningPort = port;

                _ = Task.Run(() => acceptLoop(listener, cts.Token));

                Logger.Log($"TourneyDataWebSocketServer started on http://127.0.0.1:{port}{PATH}");
            }
            catch (Exception ex)
            {
                try { listener?.Close(); } catch { }

                Logger.Error(ex, $"Failed to start TourneyDataWebSocketServer on port {port}");
                stopServer();
            }
        }

        private void stopServer()
        {
            var cts = cancellationTokenSource;
            cancellationTokenSource = null;
            cts?.Cancel();
            cts?.Dispose();

            var listener = httpListener;
            httpListener = null;
            listeningPort = 0;

            try { listener?.Stop(); } catch { }
            try { listener?.Close(); } catch { }

            foreach (var (_, ws) in clients)
            {
                try { ws.Abort(); } catch { }
                try { ws.Dispose(); } catch { }
            }

            clients.Clear();
        }

        public static int ResolvePort(int port) => normalisePort(port);

        private static int normalisePort(int port) => port is > 0 and <= 65535 ? port : DEFAULT_PORT;

        private async Task acceptLoop(HttpListener listener, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && listener.IsListening)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    string? absolutePath = context.Request.Url?.AbsolutePath;

                    if (context.Request.IsWebSocketRequest && absolutePath == PATH)
                    {
                        var wsContext = await context.AcceptWebSocketAsync(null);
                        _ = Task.Run(() => handleClient(wsContext.WebSocket, ct));
                    }
                    else if (absolutePath == BACKGROUND_PATH)
                    {
                        serveBackgroundImage(context);
                    }
                    else
                    {
                        context.Response.StatusCode = 404;
                        context.Response.Close();
                    }
                }
                catch (ObjectDisposedException) { break; }
                catch (HttpListenerException) { break; }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error in WebSocket accept loop");
                }
            }
        }

        private async Task handleClient(WebSocket webSocket, CancellationToken ct)
        {
            string clientId = Guid.NewGuid().ToString();
            clients.TryAdd(clientId, webSocket);

            Logger.Log($"Tourney WS client connected: {clientId}");

            byte[] buffer = new byte[4096];
            try
            {
                while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await webSocket.ReceiveAsync(buffer, ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnected", ct);
                        break;
                    }
                    // We don't expect client messages, but keep connection alive
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error handling WS client {clientId}");
            }
            finally
            {
                clients.TryRemove(clientId, out _);
                webSocket.Dispose();
                Logger.Log($"Tourney WS client disconnected: {clientId}");
            }
        }

        private void serveBackgroundImage(HttpListenerContext context)
        {
            try
            {
                var (bytes, contentType) = getBackgroundBytes();

                if (bytes == null)
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    return;
                }

                context.Response.StatusCode = 200;
                context.Response.ContentType = contentType ?? "application/octet-stream";
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.Close();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error serving background image");

                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch
                {
                    // Response already closed; nothing to do.
                }
            }
        }

        /// <summary>
        /// Resolves the current global beatmap's background file, cached per beatmap.
        /// Must run on the update thread (touches live realm objects).
        /// </summary>
        private (byte[]? bytes, string? contentType) getBackgroundBytes()
        {
            var working = beatmap.Value;

            if (working?.BeatmapInfo is not BeatmapInfo info || info.BeatmapSet == null)
                return (null, null);

            string key = !string.IsNullOrEmpty(info.MD5Hash) ? info.MD5Hash : info.OnlineID.ToString();

            lock (backgroundLock)
            {
                if (key == backgroundCacheKey)
                    return (backgroundCacheBytes, backgroundCacheContentType);

                backgroundCacheKey = key;
                backgroundCacheBytes = null;
                backgroundCacheContentType = null;

                try
                {
                    string? backgroundFile = findBackgroundFilename(working, info);

                    if (backgroundFile == null)
                        return (null, null);

                    string? storagePath = info.BeatmapSet.GetPathForFile(backgroundFile);

                    if (storagePath == null)
                        return (null, null);

                    using (var stream = working.GetStream(storagePath))
                    {
                        if (stream == null)
                            return (null, null);

                        using (var copy = new MemoryStream())
                        {
                            stream.CopyTo(copy);
                            backgroundCacheBytes = copy.ToArray();
                        }
                    }

                    backgroundCacheContentType = backgroundFile.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                        ? "image/png"
                        : "image/jpeg";

                    return (backgroundCacheBytes, backgroundCacheContentType);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error resolving background image");
                    return (null, null);
                }
            }
        }

        private static string? findBackgroundFilename(WorkingBeatmap working, BeatmapInfo info)
        {
            try
            {
                var set = info.BeatmapSet;

                if (set == null)
                    return null;

                // Prefer this difficulty's own .osu: backgrounds are per-difficulty events.
                var osuUsage = (!string.IsNullOrEmpty(info.Path) ? set.GetFile(info.Path) : null)
                               ?? set.Files.FirstOrDefault(f => f.Filename.EndsWith(".osu", StringComparison.OrdinalIgnoreCase));

                if (osuUsage == null)
                    return null;

                using (var stream = working.GetStream(osuUsage.File.GetStoragePath()))
                {
                    if (stream == null)
                        return null;

                    using (var reader = new StreamReader(stream))
                    {
                        bool inEvents = false;
                        string? line;

                        while ((line = reader.ReadLine()) != null)
                        {
                            line = line.Trim();

                            if (line.StartsWith('['))
                            {
                                inEvents = line.Equals("[Events]", StringComparison.OrdinalIgnoreCase);
                                continue;
                            }

                            if (!inEvents || line.StartsWith("//"))
                                continue;

                            // Background event: 0,0,"filename",x,y
                            if (line.StartsWith("0,0,\"", StringComparison.Ordinal))
                            {
                                int end = line.IndexOf('"', 5);

                                if (end > 5)
                                {
                                    string filename = line.Substring(5, end - 5);
                                    // Strip an absolute/relative directory prefix; the set lookup matches the bare name.
                                    filename = filename.Replace('\\', '/');
                                    int slash = filename.LastIndexOf('/');

                                    if (slash >= 0)
                                        filename = filename.Substring(slash + 1);

                                    if (set.GetFile(filename) != null)
                                        return filename;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error parsing background filename");
            }

            return null;
        }

        /// <summary>
        /// Broadcasts the current tournament state to all connected clients.
        /// Called periodically from the update loop or on significant state changes.
        /// </summary>
        public void BroadcastState()
        {
            if (clients.IsEmpty)
                return;

            object payload = buildPayload();
            string json = JsonSerializer.Serialize(payload, jsonOptions);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(bytes);

            foreach (var (id, ws) in clients)
            {
                if (ws.State == WebSocketState.Open)
                {
                    try
                    {
                        _ = ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Error sending to WS client {id}");
                    }
                }
            }
        }

        private object buildPayload()
        {
            var room = onlineState.Room;
            var working = beatmap.Value;

            // Determine if we're in a "playing" state (spectating a match)
            bool isPlaying = room is { State: MultiplayerRoomState.WaitingForLoad or MultiplayerRoomState.Playing }
                             && spectateSession.ClocksByUser.Any();

            // Build ipcClients from spectate session
            var ipcClients = buildIpcClients(out Dictionary<string, string> teamsByName);

            // Left/right total scores from leaderboard provider
            var (leftScore, rightScore) = getTeamScores();

            // Current map info (playback position in milliseconds, matching tosu units)
            object mapInfo = buildMapInfo(working, getPlaybackTime());

            return new
            {
                tourney = new
                {
                    manager = new
                    {
                        ipcState = isPlaying ? 0 : 1, // 0 = playing (stable convention), 1 = idle/lobby
                        bestOF = 0,
                        teamName = new { left = "", right = "" },
                        stars = new { left = 0.0, right = 0.0 },
                        bools = new
                        {
                            scoreVisible = isPlaying,
                            starsVisible = false
                        },
                        chat = buildChat(teamsByName),
                        gameplay = new
                        {
                            score = new { left = leftScore, right = rightScore }
                        }
                    },
                    ipcClients
                },
                menu = new
                {
                    // tosu GameState: menu = 0, lobby = 11 (see tosu packages/common/enums/osu.ts).
                    state = onlineState.RoomJoined.Value ? 11 : 0,
                    gameMode = ruleset.Value?.OnlineID ?? 0,
                    mods = new
                    {
                        num = 0,
                        str = modAcronyms(globalMods.Value),
                    },
                    bm = mapInfo
                }
            };
        }

        /// <summary>
        /// Current playback position in milliseconds for <c>menu.bm.time.current</c>.
        /// While spectating this is the spectator master clock (frozen with the spectated
        /// item, so overlays keep working through result screens); otherwise it follows
        /// the global preview track. Overlays treat current in (0, full) plus numeric
        /// player scores as "a match is playing".
        /// </summary>
        private double getPlaybackTime()
        {
            if (musicController.State.Value == TournamentMusicState.Spectating && spectateSession.MasterClockTime is double masterTime)
                return Math.Max(0, masterTime);

            try
            {
                return Math.Max(0, music.CurrentTrack.CurrentTime);
            }
            catch
            {
                return 0;
            }
        }

        private static string modAcronyms(IEnumerable<Mod> mods) => string.Concat(mods.Select(m => m.Acronym));

        /// <summary>
        /// Snapshots every spectated player's processor each broadcast tick: UR / hit errors
        /// refresh whenever inputs moved, and live PP recalculates in the same tick for the
        /// players whose score/accuracy/combo changed (a full PP calculation is only a few
        /// ms, so refreshing all moved players in one tick is affordable) — keeping PP on
        /// the same beat as the score, like the in-game PP counter.
        /// </summary>
        private void updateLiveData(IReadOnlyList<(int userId, ScoreProcessor processor, GameplayState? state, ScoreInfo scoreInfo)> players)
        {
            if (!ReferenceEquals(spectateSession.LeaderboardProvider, liveDataProvider))
            {
                liveDataProvider = spectateSession.LeaderboardProvider;
                liveData.Clear();
            }

            if (players.Count == 0)
                return;

            var active = new HashSet<int>(players.Select(p => p.userId));

            foreach (int stale in liveData.Keys.Where(k => !active.Contains(k)).ToList())
                liveData.Remove(stale);

            foreach (var player in players)
                updatePlayerLive(player.userId, player.processor, player.state, player.scoreInfo);
        }

        private void updatePlayerLive(int userId, ScoreProcessor processor, GameplayState? state, ScoreInfo scoreInfo)
        {
            if (scoreInfo.Ruleset == null)
                return;

            if (!liveData.TryGetValue(userId, out PlayerLiveData? entry))
                liveData[userId] = entry = new PlayerLiveData();

            try
            {
                if (entry.Calculator == null || !ReferenceEquals(entry.RulesetInstance?.RulesetInfo, scoreInfo.Ruleset))
                {
                    entry.RulesetInstance = scoreInfo.Ruleset.CreateInstance();
                    entry.Calculator = entry.RulesetInstance.CreatePerformanceCalculator();

                    // The gameplay state owns the live mod instances; clone them once per
                    // play exactly like the in-game PP counter does.
                    entry.Mods = state != null
                        ? state.Mods.Select(m => m.DeepClone()).ToArray()
                        : scoreInfo.Mods.Select(m => m.DeepClone()).ToArray();
                    entry.FcComputed = false;
                    entry.LastMaxPPMiss = -1;
                }

                if (entry.Calculator == null || entry.RulesetInstance == null)
                    return;

                entry.Snapshot.BeatmapInfo = scoreInfo.BeatmapInfo;
                entry.Snapshot.Ruleset = scoreInfo.Ruleset;
                entry.Snapshot.Mods = entry.Mods;

                string timedKey = timedKeyFor(state, scoreInfo, entry.Mods);

                if (!timedAttributesCache.TryGetValue(timedKey, out List<TimedDifficultyAttributes>? timed) || timed.Count == 0)
                {
                    // Not ready yet: the fetch is started once per key — by the prefetch of
                    // the room's current map before the match, or here on first sight. PP
                    // keeps its last value (0 before the first calculation) until it lands;
                    // UR below is refreshed regardless.
                    if (timedAttributesRequested.Add(timedKey))
                        startTimedAttributesFetch(timedKey, state, scoreInfo, entry);

                    timed = null;
                }

                // Cheap snapshot of the inputs the in-game PP counter also reads.
                processor.PopulateScore(entry.Snapshot);
                int miss = entry.Snapshot.Statistics.GetValueOrDefault(HitResult.Miss);
                int hitEventCount = processor.HitEvents.Count;

                bool changed = entry.LastHitEventCount != hitEventCount
                               || entry.LastScore != entry.Snapshot.TotalScore
                               || Math.Abs(entry.LastAccuracy - entry.Snapshot.Accuracy) > 1e-9
                               || entry.LastCombo != entry.Snapshot.Combo
                               || entry.LastMaxCombo != entry.Snapshot.MaxCombo;

                if (changed)
                {
                    entry.LastScore = entry.Snapshot.TotalScore;
                    entry.LastAccuracy = entry.Snapshot.Accuracy;
                    entry.LastCombo = entry.Snapshot.Combo;
                    entry.LastMaxCombo = entry.Snapshot.MaxCombo;
                    entry.LastHitEventCount = hitEventCount;

                    // UR and the error bar are almost free; refresh whenever anything moved.
                    double[] errors = processor.HitEvents
                                          .Where(e => e.Result != HitResult.Miss)
                                          .Select(e => e.TimeOffset)
                                          .ToArray();

                    entry.UR = errors.Length >= 2 ? standardDeviation(errors) * 10 : 0;
                    entry.HitErrors = errors.Length > 30 ? errors[(errors.Length - 30)..] : errors;
                }

                if (timed == null)
                    return;

                bool justReady = !entry.FcComputed;

                if (!changed && !justReady)
                    return;

                double now = getPlaybackTime();
                int index = timed.BinarySearch(new TimedDifficultyAttributes(now, null));
                if (index < 0)
                    index = ~index - 1;

                DifficultyAttributes current = timed[Math.Clamp(index, 0, timed.Count - 1)].Attributes;
                DifficultyAttributes final = timed[timed.Count - 1].Attributes;

                entry.PP = entry.Calculator.Calculate(entry.Snapshot, current)?.Total ?? 0;

                // The full-combo estimate only depends on the map/mods: compute once per play.
                if (!entry.FcComputed)
                {
                    entry.FcComputed = true;
                    entry.FcPP = calculateHypothetical(entry, final, final.MaxCombo, 0);
                }

                // maxThisPlay only moves on misses.
                if (entry.LastMaxPPMiss != miss)
                {
                    entry.LastMaxPPMiss = miss;
                    entry.MaxPP = calculateHypothetical(entry, final, final.MaxCombo, miss);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error updating live data for user {userId}");
            }
        }

        private static string timedKeyFor(GameplayState? state, ScoreInfo scoreInfo, IReadOnlyList<Mod> mods)
        {
            IBeatmapInfo beatmapInfo = state?.Beatmap != null ? state.Beatmap.BeatmapInfo : scoreInfo.BeatmapInfo;
            IRulesetInfo rulesetInfo = state?.Ruleset.RulesetInfo ?? scoreInfo.Ruleset!;

            return $"{beatmapInfo.MD5Hash ?? beatmapInfo.OnlineID.ToString()}|{rulesetInfo.OnlineID}|{modAcronyms(mods)}";
        }

        /// <summary>
        /// Kicks off the one-off full-map difficulty computation for a key. When gameplay is
        /// already loaded, the parsed playable beatmap is handed to calculator directly (the
        /// same trick the in-game PP counter uses), skipping any re-read of the beatmap file.
        /// </summary>
        private void startTimedAttributesFetch(string timedKey, GameplayState? state, ScoreInfo scoreInfo, PlayerLiveData entry)
        {
            try
            {
                IWorkingBeatmap working;
                Ruleset ruleset;

                if (state?.Beatmap != null && state.Ruleset != null)
                {
                    working = new PlayableBeatmapWorkingBeatmap(state.Beatmap);
                    ruleset = state.Ruleset;
                }
                else
                {
                    working = resolveWorkingFor(scoreInfo);
                    ruleset = entry.RulesetInstance!;
                }

                difficultyCache.GetTimedDifficultyAttributesAsync(working, ruleset, entry.Mods).ContinueWith(task => Schedule(() =>
                {
                    var result = task.GetResultSafely();

                    if (result != null && result.Count > 0)
                        timedAttributesCache[timedKey] = result;
                    else
                        timedAttributesRequested.Remove(timedKey);
                }));
            }
            catch (Exception ex)
            {
                timedAttributesRequested.Remove(timedKey);
                Logger.Error(ex, "Error starting timed difficulty attributes fetch");
            }
        }

        /// <summary>
        /// PP estimate keeping <paramref name="miss"/> misses and perfecting everything else.
        /// <c>miss = 0</c> is the full-combo value.
        /// </summary>
        private double calculateHypothetical(PlayerLiveData entry, DifficultyAttributes final, int maxCombo, int miss)
        {
            if (entry.Calculator == null || maxCombo <= 0)
                return 0;

            var snapshot = entry.Snapshot;
            var perfect = new ScoreInfo(snapshot.BeatmapInfo, snapshot.Ruleset)
            {
                Mods = snapshot.Mods,
                Combo = maxCombo,
                MaxCombo = maxCombo,
            };
            perfect.Statistics[HitResult.Great] = Math.Max(0, maxCombo - miss);
            perfect.Statistics[HitResult.Miss] = miss;

            try
            {
                return entry.Calculator.Calculate(perfect, final)?.Total ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private static double standardDeviation(double[] values)
        {
            double mean = values.Average();
            double sum = 0;

            foreach (double v in values)
                sum += (v - mean) * (v - mean);

            return Math.Sqrt(sum / values.Length);
        }

        /// <summary>
        /// Wraps an already-parsed playable beatmap for the difficulty calculator, mirroring
        /// the in-game PP counter's private <c>GameplayWorkingBeatmap</c>: returning the live
        /// in-memory beatmap means timed attribute computation never re-reads the beatmap file.
        /// </summary>
        private sealed class PlayableBeatmapWorkingBeatmap : WorkingBeatmap
        {
            private readonly IBeatmap playableBeatmap;

            public PlayableBeatmapWorkingBeatmap(IBeatmap playableBeatmap)
                : base(playableBeatmap.BeatmapInfo, null)
            {
                this.playableBeatmap = playableBeatmap;
            }

            public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken)
                => playableBeatmap;

            protected override IBeatmap GetBeatmap() => playableBeatmap;

            // Only the playable beatmap is ever needed for difficulty calculation.
            public override Texture GetBackground() => throw new NotImplementedException();

            protected override Track GetBeatmapTrack() => throw new NotImplementedException();

            protected override ISkin GetSkin() => throw new NotImplementedException();

            public override Stream GetStream(string storagePath) => throw new NotImplementedException();
        }

        private WorkingBeatmap resolveWorkingFor(ScoreInfo scoreInfo)
        {
            try
            {
                var global = beatmap.Value;

                if (global?.BeatmapInfo != null && global.BeatmapInfo.OnlineID == scoreInfo.BeatmapInfo.OnlineID && scoreInfo.BeatmapInfo.OnlineID > 0)
                    return global;

                var local = beatmaps.QueryBeatmap(b => b.OnlineID == scoreInfo.BeatmapInfo.OnlineID);

                if (local != null)
                    return beatmaps.GetWorkingBeatmap(local);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error resolving working beatmap for PP calculation");
            }

            return beatmap.Value;
        }

        private List<object> buildIpcClients(out Dictionary<string, string> teamsByName)
        {
            var clients = new List<object>();
            teamsByName = new Dictionary<string, string>(StringComparer.Ordinal);

            var scoresByUser = spectateSession.ScoresByUser;
            var redCells = spectateSession.RedCells ?? new List<SpectateCell>();
            var blueCells = spectateSession.BlueCells ?? new List<SpectateCell>();

            var allCells = redCells.Concat(blueCells).ToList();
            int half = allCells.Count / 2;

            var livePlayers = new List<(int userId, ScoreProcessor processor, GameplayState? state, ScoreInfo scoreInfo)>();

            foreach (var cell in allCells)
            {
                int? userId = cell.PlayArea?.UserId;

                if (userId == null)
                    continue;

                if (!scoresByUser.TryGetValue(userId.Value, out Score? score) || score?.ScoreInfo == null)
                    continue;

                ScoreProcessor? processor = cell.PlayArea?.ScoreProcessor;

                if (processor == null)
                    continue;

                livePlayers.Add((userId.Value, processor, cell.PlayArea?.GameplayStatePublic, score.ScoreInfo));
            }

            updateLiveData(livePlayers);

            for (int i = 0; i < allCells.Count; i++)
            {
                var cell = allCells[i];
                int? userId = cell.PlayArea?.UserId;
                if (userId == null)
                    continue;

                if (!scoresByUser.TryGetValue(userId.Value, out var score))
                    continue;

                var scoreProcessor = cell.PlayArea?.ScoreProcessor;
                if (scoreProcessor == null)
                    continue;

                string team = i < half ? "left" : "right";
                var scoreInfo = score.ScoreInfo;

                if (scoreInfo == null)
                    continue;

                liveData.TryGetValue(userId.Value, out PlayerLiveData? live);

                // tosu v1 hit keys (numeric strings), not friendly names.
                var hits = new Dictionary<string, object?>
                {
                    ["300"] = scoreInfo.Statistics.TryGetValue(HitResult.Great, out int g) ? g : 0,
                    ["geki"] = scoreInfo.Statistics.TryGetValue(HitResult.Perfect, out int p) ? p : 0,
                    ["100"] = scoreInfo.Statistics.TryGetValue(HitResult.Ok, out int o) ? o : 0,
                    ["katu"] = scoreInfo.Statistics.TryGetValue(HitResult.Good, out int gd) ? gd : 0,
                    ["50"] = scoreInfo.Statistics.TryGetValue(HitResult.Meh, out int m) ? m : 0,
                    ["0"] = scoreInfo.Statistics.TryGetValue(HitResult.Miss, out int ms) ? ms : 0,
                    ["sliderBreaks"] = 0,
                    ["grade"] = new
                    {
                        current = scoreProcessor.Rank.Value.ToString().ToLower(),
                        maxThisPlay = ""
                    },
                    ["unstableRate"] = live?.UR ?? 0.0,
                    ["hitErrorArray"] = live?.HitErrors ?? Array.Empty<double>(),
                };

                string username = scoreInfo.User?.Username ?? $"Player {userId}";

                if (!teamsByName.ContainsKey(username))
                    teamsByName[username] = team;

                clients.Add(new
                {
                    team,
                    // "user" mirrors the stable-reader shape some overlays read
                    // (ipcClient.user.name) alongside tosu's "spectating" block.
                    user = new
                    {
                        name = username,
                        username,
                    },
                    spectating = new
                    {
                        name = username,
                        country = scoreInfo.User?.CountryCode.ToString() ?? "XX",
                        userID = scoreInfo.User?.Id ?? userId.Value,
                        accuracy = scoreProcessor.Accuracy.Value,
                        rankedScore = 0L,
                        playCount = 0,
                        globalRank = 0,
                        totalPP = 0.0
                    },
                    gameplay = new
                    {
                        gameMode = (int?)scoreInfo.Ruleset?.OnlineID ?? 0,
                        name = scoreInfo.User?.Username ?? $"Player {userId}",
                        score = scoreProcessor.TotalScore.Value,
                        accuracy = scoreProcessor.Accuracy.Value,
                        combo = new
                        {
                            current = scoreProcessor.Combo.Value,
                            max = scoreProcessor.HighestCombo.Value
                        },
                        hp = new
                        {
                            normal = cell.PlayArea?.HealthProcessorPublic?.Health.Value ?? 0,
                            smooth = cell.PlayArea?.HealthProcessorPublic?.Health.Value ?? 0
                        },
                        hits,
                        pp = new
                        {
                            current = live?.PP ?? 0.0,
                            fc = live?.FcPP ?? 0.0,
                            maxThisPlay = live?.MaxPP ?? 0.0,
                        },
                        mods = new
                        {
                            num = scoreInfo.Mods.Length,
                            str = modAcronyms(scoreInfo.Mods),
                        }
                    },

                });
            }

            return clients;
        }

        private (long left, long right) getTeamScores()
        {
            var provider = spectateSession.LeaderboardProvider;
            if (provider?.TeamScores.Count == 2)
            {
                var scores = provider.TeamScores.Values.ToList();
                return (scores[0].Value, scores[1].Value);
            }
            return (0, 0);
        }

        /// <summary>
        /// Room chat history (last 50 messages) in tosu's shape.
        /// Team is resolved by matching the sender against the current
        /// spectated players, mirroring tosu's own mapping.
        /// </summary>
        private List<object> buildChat(Dictionary<string, string> teamsByName)
        {
            var messages = new List<object>();

            try
            {
                var room = onlineState.Room;

                if (!onlineState.RoomJoined.Value || room == null || channelManager == null)
                    return messages;

                if (chatChannel == null || chatChannelId != room.ChannelID)
                {
                    chatChannelId = room.ChannelID;
                    chatChannel = channelManager.JoinChannel(new Channel
                    {
                        Id = room.ChannelID,
                        Type = ChannelType.Multiplayer,
                        Name = $"#lazermp_{room.RoomID}",
                    });
                }

                if (chatChannel == null)
                    return messages;

                foreach (var message in chatChannel.Messages.TakeLast(50))
                {
                    string name = message.Sender?.Username ?? "BanchoBot";
                    string team = teamsByName.TryGetValue(name, out string? mapped) ? mapped
                        : name == "BanchoBot" ? "bot"
                        : "unknown";

                    messages.Add(new
                    {
                        team,
                        time = message.Timestamp.ToString("o"),
                        name,
                        messageBody = message.DisplayContent ?? message.Content ?? string.Empty,
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error building chat payload");
            }

            return messages;
        }

        /// <summary>
        /// Maps lazer beatmap online statuses to tosu's stable status numbers
        /// (mirrors tosu's lazerToStableStatus table).
        /// </summary>
        private static int toRankedStatus(int statusInt) => statusInt switch
        {
            -4 => 1,
            -3 => 1,
            -2 => 2,
            -1 => 2,
            0 => 2,
            1 => 4,
            2 => 5,
            3 => 6,
            4 => 7,
            _ => 0,
        };

        /// <summary>
        /// Refreshes the cached mod-adjusted star rating when the beatmap, ruleset or
        /// mods change. Mirrors solo song select via <see cref="BeatmapDifficultyCache"/>;
        /// the stored rating is used until the async calculation lands.
        /// Also prefetches timed difficulty attributes for the room's upcoming map (see
        /// <see cref="prefetchTimedAttributes"/>) so live PP is ready the moment a match starts.
        /// </summary>
        private void refreshStarRating(IBeatmapInfo info, RulesetInfo? rulesetInfo, Mod[] mods, WorkingBeatmap working)
        {
            string key = $"{info.MD5Hash ?? info.OnlineID.ToString()}|{rulesetInfo?.OnlineID}|{string.Join(',', mods.Select(m => m.Acronym))}";

            if (key == difficultyCacheKey)
                return;

            difficultyCacheKey = key;
            difficultyCancellation?.Cancel();

            // Fall back to the stored (unmodded) rating until the calculation completes.
            cachedStarRating = info.StarRating;

            if (rulesetInfo == null)
                return;

            var cancellation = difficultyCancellation = new CancellationTokenSource();

            difficultyCache.GetDifficultyAsync(info, rulesetInfo, mods).ContinueWith(task => Schedule(() =>
            {
                if (cancellation.IsCancellationRequested)
                    return;

                cachedStarRating = task.GetResultSafely()?.Stars ?? info.StarRating;
            }));

            // While a room is joined the global beatmap follows the room's current playlist
            // item, which is exactly what the next match will play. Computing timed
            // attributes now (before the match starts) mirrors what the in-game PP counter
            // does during each player's loading screen, so PP never starts a match from 0.
            if (onlineState.RoomJoined.Value)
                prefetchTimedAttributes(info, rulesetInfo, mods, working);
        }

        /// <summary>
        /// Prefetches timed difficulty attributes for the upcoming map/mods combination so a
        /// match starting on it gets live PP from the first tick. Skipped when out of a room
        /// (the free menu song would otherwise kick a full difficulty calculation per track).
        /// </summary>
        private void prefetchTimedAttributes(IBeatmapInfo info, RulesetInfo rulesetInfo, Mod[] mods, WorkingBeatmap working)
        {
            try
            {
                string key = $"{info.MD5Hash ?? info.OnlineID.ToString()}|{rulesetInfo.OnlineID}|{modAcronyms(mods)}";

                if (timedAttributesCache.ContainsKey(key) || !timedAttributesRequested.Add(key))
                    return;

                // Note: the working beatmap's playable chart may not be parsed yet; the
                // calculator loads it lazily on the background scheduler, same as the
                // star rating path above.

                difficultyCache.GetTimedDifficultyAttributesAsync(working, rulesetInfo.CreateInstance(), mods).ContinueWith(task => Schedule(() =>
                {
                    var result = task.GetResultSafely();

                    if (result != null && result.Count > 0)
                        timedAttributesCache[key] = result;
                    else
                        timedAttributesRequested.Remove(key);
                }));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error prefetching timed difficulty attributes");
            }
        }

        private object? buildMapInfo(WorkingBeatmap? working, double currentTime)
        {
            if (working?.BeatmapInfo == null)
                return null;

            var info = working.BeatmapInfo;
            var set = info.BeatmapSet;

            var rulesetInfo = ruleset.Value;
            Mod[] mods = globalMods.Value.ToArray();

            refreshStarRating(info, rulesetInfo, mods, working);

            // Base (unmodded) values, kept for overlays that want the original numbers.
            var originalStats = new
            {
                CS = info.Difficulty?.CircleSize ?? 0,
                AR = info.Difficulty?.ApproachRate ?? 0,
                OD = info.Difficulty?.OverallDifficulty ?? 0,
                HP = info.Difficulty?.DrainRate ?? 0,
                SR = info.StarRating,
            };

            // Mod-adjusted values, mirroring SongBar (what players actually play with).
            Ruleset? rulesetInstance = rulesetInfo?.CreateInstance();
            var adjusted = rulesetInstance == null ? info.Difficulty : rulesetInstance.GetAdjustedDisplayDifficulty(info, mods);

            double rate = ModUtils.CalculateRateWithMods(mods);
            double bpm = FormatUtils.RoundBPM(info.BPM, rate);
            double length = rate > 0 ? info.Length / rate : info.Length;

            return new
            {
                id = info.OnlineID > 0 ? (long)info.OnlineID : info.ID.GetHashCode(),
                metadata = new
                {
                    artist = info.Metadata?.Artist ?? "Unknown Artist",
                    artistOriginal = info.Metadata?.ArtistUnicode ?? info.Metadata?.Artist ?? "Unknown Artist",
                    title = info.Metadata?.Title ?? "Unknown Title",
                    titleOriginal = info.Metadata?.TitleUnicode ?? info.Metadata?.Title ?? "Unknown Title",
                    mapper = info.Metadata?.Author?.Username ?? "Unknown Mapper",
                    creator = info.Metadata?.Author?.Username ?? "Unknown Mapper",
                    difficulty = info.DifficultyName ?? "Normal",
                    version = info.DifficultyName ?? "Normal"
                },
                md5 = info.MD5Hash ?? "",
                set = set?.OnlineID ?? 0,
                rankedStatus = toRankedStatus(set?.StatusInt ?? 0),
                time = new
                {
                    // Milliseconds, matching tosu units.
                    full = length > 0 ? (int)Math.Round(length) : 0,
                    current = (int)Math.Round(currentTime),
                },
                mods = Array.Empty<string>(),
                stats = new
                {
                    CS = adjusted?.CircleSize ?? originalStats.CS,
                    AR = adjusted?.ApproachRate ?? originalStats.AR,
                    OD = adjusted?.OverallDifficulty ?? originalStats.OD,
                    HP = adjusted?.DrainRate ?? originalStats.HP,
                    fullSR = cachedStarRating,
                    SR = cachedStarRating,
                    BPM = new
                    {
                        common = bpm,
                        realtime = bpm,
                        BPM = bpm
                    }
                },
                originalStats,
            };
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            tournamentStorage.WebSocketPort.ValueChanged -= onWebSocketPortChanged;

            broadcasting = false;

            restartDelegate?.Cancel();
            restartDelegate = null;

            broadcastDelegate?.Cancel();
            broadcastDelegate = null;

            difficultyCancellation?.Cancel();
            difficultyCancellation?.Dispose();
            difficultyCancellation = null;

            stopServer();
        }
    }
}
