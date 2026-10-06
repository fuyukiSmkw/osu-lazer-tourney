// Copyright (c) 2025 fuyukiS <fuyukiS@outlook.jp>. Licensed under the MIT License.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Overlays;
using osu.Game.Rulesets.LazerTourney.Online;

#nullable enable

namespace osu.Game.Rulesets.LazerTourney.ListenerLoader.Handlers;

public partial class NewVersionNotificationListener : AbstractHandler
{
    [Resolved]
    private INotificationOverlay? notificationOverlay { get; set; } = null!;

    protected override void LoadComplete()
    {
        base.LoadComplete();
        Schedule(checkForUpdates);
    }

    private GetGitHubRelease? releaseRequest;

    private async void checkForUpdates()
    {
        GitHubRelease? release = null;

        releaseRequest?.Abort();
        releaseRequest = new();
        releaseRequest.Finished += () => release = releaseRequest.ResponseObject ?? null;
        await releaseRequest.AwaitRequest();

        if (release is null) return;
        if (string.IsNullOrEmpty(release.tagName)) return;

        if (release.tagName != LazerTourneyRuleset.Version)
            Schedule(() => sendUpdateNotification(release));
    }

    private void sendUpdateNotification(GitHubRelease release)
    {
        notificationOverlay?.Post(new UpdateAvailableNotification(release));
    }
}
