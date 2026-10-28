// Copyright (c) 2026 fuyukiS <fuyukiS@outlook.jp>. Licensed under the MIT License.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using osu.Framework.Graphics;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
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
    private static readonly ConditionalWeakTable<OsuGame, object> resource_store_added = new();

    /// <summary>
    /// Adds the plugin's embedded resources (once per game instance), disables Sentry for this session
    /// and pushes the tournament screen. Must be called from the update thread.
    /// </summary>
    /// <returns><see langword="true"/> if the tournament screen was pushed.</returns>
    public static bool TryLaunch(OsuGame game)
    {
        if (game.ScreenStack.CurrentScreen is TournamentGame)
            return false;

        if (game.ScreenStack.CurrentScreen is not MainMenu)
            return false;

        try
        {
            resource_store_added.GetValue(game, _ =>
            {
                game.Resources.AddStore(new DllResourceStore(typeof(LazerTourneyRuleset).Assembly));
                return new object();
            });
        }
        catch (Exception e)
        {
            Logging.LogError(e, "Failed to add tournament client resources");
            return false;
        }

        try
        {
            // Sentry cannot be re-enabled through the public API (the DSN/options live inside
            // osu.Game's SentryLogger), so this intentionally lasts until the game is restarted.
            SentrySdk.Close();
        }
        catch (Exception e)
        {
            Logging.LogError(e, "Failed to disable Sentry for the tournament client");
        }

        try
        {
            game.ScreenStack.Push(new TournamentGame());
        }
        catch (Exception e)
        {
            Logging.LogError(e, "Failed to push the tournament screen");
            return false;
        }

        game.ChildrenOfType<SettingsOverlay>().FirstOrDefault()?.Hide();

        return true;
    }
}
