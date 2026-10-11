// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Online.Chat;
using osu.Game.Overlays;
using osu.Game.Rulesets.LazerTourney.Tournament.Components;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components.ChatCommands
{
    /// <summary>
    /// Shared notification sink for chat-adjacent components.
    /// Posts to the notification overlay when available, otherwise appends
    /// an error line to the given channel (same fallback as the chat boxes use).
    /// </summary>
    internal static class ChatNotifier
    {
        public static void Notify(INotificationOverlay? notifications, Channel? channel, string text, bool important = false)
        {
            if (notifications != null)
            {
                if (important)
                    notifications.Post(new TournamentNotification { Text = text, IsImportant = true });
                else
                    notifications.Post(new TournamentNotification { Text = text, Transient = true });
            }
            else
                channel?.AddNewMessages(new ErrorMessage(text));
        }
    }
}
