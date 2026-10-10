// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game;
using osu.Game.Online.API;
using osu.Game.Rulesets.LazerTourney.Tournament.IO;
using osu.Game.Rulesets.LazerTourney.Tournament.Online.Referee;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Online
{
    /// <summary>
    /// Connection state of the referee hub SignalR connection.
    /// </summary>
    public enum RefereeConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
    }

    /// <summary>
    /// OAuth state for the referee hub API (authorization code grant).
    /// One user, one client: credentials and tokens live as plain values in the shared ini file,
    /// never in bracket files.
    /// </summary>
    public partial class RefereeAuthController : Component
    {
        public const int CALLBACK_PORT = 60727;

        public static string CallbackUrl => $"http://localhost:{CALLBACK_PORT}/";

        private const string authorize_url = "https://osu.ppy.sh/oauth/authorize";
        private const string token_url = "https://osu.ppy.sh/oauth/token";

        // Scopes needed to drive the referee hub on the user's behalf.
        private const string scopes = "identify multiplayer.write_manage";

        private const int callback_timeout_seconds = 300;

        // Refresh slightly early so a token never dies mid-operation.
        private const long expiry_buffer_seconds = 60;

        [Resolved]
        private TournamentStorage tournamentStorage { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IAPIProvider? api { get; set; }

        [Resolved(canBeNull: true)]
        private OsuGame? game { get; set; }

        [Resolved(canBeNull: true)]
        private Clipboard? clipboard { get; set; }

        /// <summary>
        /// Whether a usable access token exists.
        /// </summary>
        public readonly BindableBool HasValidToken = new BindableBool();

        /// <summary>
        /// Human-readable token state for the settings UI.
        /// </summary>
        public readonly Bindable<string> StatusText = new Bindable<string>("No access token yet.");

        private bool busy;
        private HttpListener? listener;

        /// <summary>
        /// Re-evaluates token state, attempting one silent refresh when expired.
        /// </summary>
        public async Task RefreshStatusAsync()
        {
            if (busy)
                return;

            busy = true;

            try
            {
                string accessToken = tournamentStorage.RefereeAccessToken;
                long expiresAt = tournamentStorage.RefereeExpiresAt;

                if (string.IsNullOrEmpty(accessToken))
                {
                    setStatus(false, "No access token yet.");
                    return;
                }

                if (expiresAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiry_buffer_seconds)
                {
                    setStatus(true, $"Access token valid until {formatExpiry(expiresAt)}.");
                    return;
                }

                string clientId = tournamentStorage.RefereeClientId.Value;
                string clientSecret = tournamentStorage.RefereeClientSecret.Value;

                if (string.IsNullOrEmpty(tournamentStorage.RefereeRefreshToken) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    setStatus(false, "Access token expired; re-authorisation required.");
                    return;
                }

                if (await tryRefreshAsync(clientId, clientSecret).ConfigureAwait(false))
                    setStatus(true, $"Access token valid until {formatExpiry(tournamentStorage.RefereeExpiresAt)}.");
                else
                    setStatus(false, "Token refresh failed; re-authorisation required.");
            }
            finally
            {
                busy = false;
            }
        }

        /// <summary>
        /// Runs the full authorization code flow: opens the browser, serves the localhost
        /// callback, exchanges the code for tokens and stores them. Returns success.
        /// </summary>
        public async Task<bool> AuthorizeAsync()
        {
            if (busy)
                return false;

            string clientId = tournamentStorage.RefereeClientId.Value.Trim();
            string clientSecret = tournamentStorage.RefereeClientSecret.Value.Trim();

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                setStatus(false, "Enter the client ID and client secret first.");
                return false;
            }

            busy = true;
            stopListener();

            try
            {
                string state = Guid.NewGuid().ToString("N");

                string url = $"{authorize_url}?client_id={Uri.EscapeDataString(clientId.Trim())}"
                             + $"&redirect_uri={Uri.EscapeDataString(CallbackUrl)}"
                             + "&response_type=code"
                             + $"&scope={Uri.EscapeDataString(scopes)}"
                             + $"&state={state}";

                listener = new HttpListener();
                listener.Prefixes.Add(CallbackUrl);
                listener.Start();

                try
                {
                    game?.OpenUrlExternally(url);
                }
                catch (Exception ex)
                {
                    setStatus(false, $"Could not open the browser: {ex.Message}");
                    return false;
                }

                string? code = await waitForCodeAsync(state).ConfigureAwait(false);

                if (code == null)
                {
                    setStatus(false, "Authorisation timed out or was denied.");
                    return false;
                }

                var tokens = await exchangeCodeAsync(clientId.Trim(), clientSecret.Trim(), code).ConfigureAwait(false);

                if (tokens == null)
                {
                    setStatus(false, "Token exchange failed; check the client ID and secret.");
                    return false;
                }

                tournamentStorage.RefereeAccessToken = tokens.Value.accessToken;
                tournamentStorage.RefereeRefreshToken = tokens.Value.refreshToken;
                tournamentStorage.RefereeExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + tokens.Value.expiresIn;

                setStatus(true, "Authorised.");
                await RefreshStatusAsync().ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Referee authorisation failed: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                setStatus(false, $"Authorisation failed: {ex.Message}");
                return false;
            }
            finally
            {
                stopListener();
                busy = false;
            }
        }

        /// <summary>
        /// Copies the localhost callback URL for pasting into the OAuth application form.
        /// </summary>
        public void CopyCallbackUrl() => clipboard?.SetText(CallbackUrl);

        /// <summary>
        /// Clears the stored tokens and drops the hub connection. The client ID and secret stay.
        /// </summary>
        public void Revoke() => revokeAndRefreshAsync();

        private async void revokeAndRefreshAsync()
        {
            tournamentStorage.RefereeAccessToken = string.Empty;
            tournamentStorage.RefereeRefreshToken = string.Empty;
            tournamentStorage.RefereeExpiresAt = 0;

            await DisconnectAsync().ConfigureAwait(false);
            await RefreshStatusAsync().ConfigureAwait(false);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            stopListener();
            DisconnectAsync().ContinueWith(_ => { });
        }

        #region Referee hub connection

        /// <summary>
        /// Connection state of the referee hub SignalR connection. Never connected automatically.
        /// </summary>
        public readonly Bindable<RefereeConnectionState> ConnectionState = new Bindable<RefereeConnectionState>(RefereeConnectionState.Disconnected);

        /// <summary>
        /// Human-readable connection state for the settings UI.
        /// </summary>
        public readonly Bindable<string> ConnectionStatusText = new Bindable<string>("Not connected.");

        private HubConnection? hubConnection;

        private string refereeHubUrl()
        {
            string? baseUrl = api?.Endpoints.SpectatorUrl; // spectator.osu.ppy.sh/spectator
            string s = "/spectator";
            if (baseUrl.EndsWith(s))
                baseUrl = baseUrl.Remove(baseUrl.Length - s.Length);
            return string.IsNullOrEmpty(baseUrl) ? "/referee" : baseUrl.TrimEnd('/') + "/referee";
        }

        /// <summary>
        /// Connects to the referee hub with the stored access token. No-op without a valid token.
        /// </summary>
        public async Task ConnectAsync()
        {
            if (ConnectionState.Value != RefereeConnectionState.Disconnected || !HasValidToken.Value)
                return;

            setConnectionState(RefereeConnectionState.Connecting, "Connecting to the referee hub...");

            string url = refereeHubUrl();

            var connection = new HubConnectionBuilder()
                .WithUrl(url, options => options.AccessTokenProvider = () => Task.FromResult<string?>(tournamentStorage.RefereeAccessToken))
                .Build();

            connection.Closed += _ =>
            {
                Schedule(() => setConnectionState(RefereeConnectionState.Disconnected, "Connection closed by the server."));
                return Task.CompletedTask;
            };

            try
            {
                await connection.StartAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Log($"Referee hub connection failed: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                Schedule(() => setConnectionState(RefereeConnectionState.Disconnected, $"Connection failed: {ex.Message}"));

                try
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Best effort only.
                }

                return;
            }

            hubConnection = connection;
            Schedule(() => setConnectionState(RefereeConnectionState.Connected, "Connected to the referee hub."));
        }

        /// <summary>
        /// Drops the referee hub connection, if any.
        /// </summary>
        public async Task DisconnectAsync()
        {
            var connection = hubConnection;
            hubConnection = null;

            if (connection == null)
            {
                Schedule(() => setConnectionState(RefereeConnectionState.Disconnected, "Not connected."));
                return;
            }

            try
            {
                await connection.StopAsync().ConfigureAwait(false);
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Log($"Referee hub disconnect failed: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
            }

            Schedule(() => setConnectionState(RefereeConnectionState.Disconnected, "Not connected."));
        }

        private void setConnectionState(RefereeConnectionState state, string text)
        {
            ConnectionState.Value = state;
            ConnectionStatusText.Value = text;
        }

        /// <summary>
        /// Creates a new multiplayer room via the referee hub.
        /// The caller is automatically joined to the room as a referee.
        /// </summary>
        public async Task<RoomJoinedResponse> MakeRoomAsync(MakeRoomRequest request, CancellationToken cancellationToken = default)
        {
            var connection = hubConnection;

            if (connection == null || ConnectionState.Value != RefereeConnectionState.Connected)
                throw new InvalidOperationException("Not connected to the referee hub.");

            // Refresh the token first so a renewed token is used by the access token provider.
            await RefreshStatusAsync().ConfigureAwait(false);

            return await connection.InvokeAsync<RoomJoinedResponse>("MakeRoom", request, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Joins an existing multiplayer room via the referee hub.
        /// The caller must already be an added referee of the room; no password is required.
        /// </summary>
        public async Task<RoomJoinedResponse> JoinRoomAsync(long roomId, CancellationToken cancellationToken = default)
        {
            var connection = hubConnection;

            if (connection == null || ConnectionState.Value != RefereeConnectionState.Connected)
                throw new InvalidOperationException("Not connected to the referee hub.");

            // Refresh the token first so a renewed token is used by the access token provider.
            await RefreshStatusAsync().ConfigureAwait(false);

            return await connection.InvokeAsync<RoomJoinedResponse>("JoinRoom", roomId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Changes the settings of a room managed via the referee hub.
        /// </summary>
        public async Task ChangeRoomSettingsAsync(long roomId, ChangeRoomSettingsRequest request, CancellationToken cancellationToken = default)
        {
            var connection = hubConnection;

            if (connection == null || ConnectionState.Value != RefereeConnectionState.Connected)
                throw new InvalidOperationException("Not connected to the referee hub.");

            await RefreshStatusAsync().ConfigureAwait(false);

            await connection.InvokeAsync("ChangeRoomSettings", roomId, request, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        private void stopListener()
        {
            try
            {
                listener?.Stop();
                listener?.Close();
            }
            catch
            {
                // Already stopped; nothing to do.
            }

            listener = null;
        }

        private async Task<string?> waitForCodeAsync(string state)
        {
            var current = listener;

            if (current == null)
                return null;

            var deadline = DateTimeOffset.UtcNow.AddSeconds(callback_timeout_seconds);

            while (DateTimeOffset.UtcNow < deadline)
            {
                var remaining = deadline - DateTimeOffset.UtcNow;
                var delay = Task.Delay(remaining < TimeSpan.FromSeconds(30) ? remaining : TimeSpan.FromSeconds(30));

                Task<HttpListenerContext> contextTask = current.GetContextAsync();

                if (await Task.WhenAny(contextTask, delay).ConfigureAwait(false) != contextTask)
                    continue;

                HttpListenerContext context = await contextTask.ConfigureAwait(false);

                try
                {
                    string? code = context.Request.QueryString["code"];
                    string? returnedState = context.Request.QueryString["state"];

                    respondWithResultPage(context, code != null && returnedState == state);

                    // Ignore stray hits (e.g. favicon) and keep waiting for the real callback.
                    if (code != null && returnedState == state)
                        return code;
                }
                catch
                {
                    try
                    {
                        context.Response.Close();
                    }
                    catch
                    {
                        // Best effort only.
                    }
                }
            }

            return null;
        }

        private static void respondWithResultPage(HttpListenerContext context, bool success)
        {
            const string html_success = "<html><body><h2>Authorisation complete.</h2><p>You can close this tab and return to the game.</p></body></html>";
            const string html_failed = "<html><body><h2>Authorisation failed.</h2><p>Please close this tab and try again from the game.</p></body></html>";

            byte[] body = System.Text.Encoding.UTF8.GetBytes(success ? html_success : html_failed);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = body.Length;
            context.Response.OutputStream.Write(body, 0, body.Length);
            context.Response.Close();
        }

        private static async Task<(string accessToken, string refreshToken, long expiresIn)?> exchangeCodeAsync(string clientId, string clientSecret, string code)
        {
            using var http = new HttpClient();
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = CallbackUrl,
                ["code"] = code,
            });

            using HttpResponseMessage response = await http.PostAsync(token_url, content).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            return parseTokenResponse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        }

        private async Task<bool> tryRefreshAsync(string clientId, string clientSecret)
        {
            try
            {
                using var http = new HttpClient();
                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["refresh_token"] = tournamentStorage.RefereeRefreshToken,
                });

                using HttpResponseMessage response = await http.PostAsync(token_url, content).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    return false;

                var tokens = parseTokenResponse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));

                if (tokens == null)
                    return false;

                tournamentStorage.RefereeAccessToken = tokens.Value.accessToken;
                tournamentStorage.RefereeRefreshToken = tokens.Value.refreshToken;
                tournamentStorage.RefereeExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + tokens.Value.expiresIn;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Referee token refresh failed: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
                return false;
            }
        }

        private static (string accessToken, string refreshToken, long expiresIn)? parseTokenResponse(string json)
        {
            JObject obj;

            try
            {
                obj = JObject.Parse(json);
            }
            catch
            {
                return null;
            }

            string? accessToken = obj.Value<string>("access_token");
            string? refreshToken = obj.Value<string>("refresh_token");
            long expiresIn = obj.Value<long?>("expires_in") ?? 0;

            if (string.IsNullOrEmpty(accessToken) || expiresIn <= 0)
                return null;

            return (accessToken, refreshToken ?? string.Empty, expiresIn);
        }

        private void setStatus(bool valid, string text) => Schedule(() =>
        {
            HasValidToken.Value = valid;
            StatusText.Value = text;
        });

        private static string formatExpiry(long unixSeconds)
            => DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }
}
