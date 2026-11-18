// Copyright (c) 2026 fuyukiS <fuyukiS@outlook.jp>. Licensed under the MIT License.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Overlays;
using osu.Game.Rulesets.LazerTourney.Tournament;
using osu.Game.Screens.Menu;
using Sentry;

#nullable enable

namespace osu.Game.Rulesets.LazerTourney.Startup;

/// <summary>
/// Starts the tournament client on demand from the ruleset's settings section.
/// </summary>
public static class TournamentLauncher
{
    private static int sentry_close_started;

    /// <summary>
    /// Disables Sentry for this session and pushes the tournament screen.
    /// Must be called from the update thread.
    /// </summary>
    /// <remarks>
    /// The plugin's embedded resources are exposed through
    /// <see cref="LazerTourneyRuleset.CreateResourceStore"/> instead of being added to the
    /// host game's global resource store here.
    /// </remarks>
    /// <returns><see langword="true"/> if the tournament screen was pushed.</returns>
    public static bool TryLaunch(OsuGame game)
    {
        if (game.ScreenStack.CurrentScreen is TournamentGame)
            return false;

        if (game.ScreenStack.CurrentScreen is not MainMenu)
            return false;

        closeSentryInBackground();

        try
        {
            // game.ScreenStack.Push(new TournamentGame());
            game.PerformFromScreen(s => s.Push(new TournamentGame()), [typeof(MainMenu)]);
        }
        catch (Exception e)
        {
            Logging.LogError(e, "Failed to push the tournament screen");
            return false;
        }

        game.ChildrenOfType<SettingsOverlay>().FirstOrDefault()?.Hide();

        return true;
    }

    /// <summary>
    /// Closes the Sentry SDK on a background thread.
    /// </summary>
    /// <remarks>
    /// <see cref="SentrySdk.Close"/> swaps the current hub for <c>DisabledHub</c> immediately, so
    /// reporting stops as soon as this starts, but disposing the old hub flushes the queued events
    /// and blocks for up to <c>SentryOptions.FlushTimeout</c> (2 seconds by default). That flush is
    /// what froze the whole game for 1–2 s when it ran on the update thread, so it is moved to a
    /// background thread here. Sentry cannot be re-enabled through the public API (the DSN/options
    /// live inside osu.Game's SentryLogger), so this intentionally lasts until the game is restarted.
    /// </remarks>
    private static void closeSentryInBackground()
    {
        if (Interlocked.Exchange(ref sentry_close_started, 1) != 0)
            return;

        Task.Run(() =>
        {
            try
            {
                SentrySdk.Close();
            }
            catch (Exception e)
            {
                Logging.LogError(e, "Failed to disable Sentry for the tournament client");
            }
        });
    }
}
