// Copyright (c) 2025 fuyukiS <fuyukiS@outlook.jp>. Licensed under the MIT License.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Graphics;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.LazerTourney.Graphics;
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

    public partial class UpdateAvailableNotification : SimpleNotification
    {
        private readonly GitHubRelease release;
        protected LazerTourneyIcon icon { get; private set; } = null!;

        public UpdateAvailableNotification(GitHubRelease release)
        {
            this.release = release;
            // Text = $"New version of o!C ruleset available:\n{release.tagName}\n Click to view!";
            Icon = default;
        }

        [BackgroundDependencyLoader]
        private void load(OsuGame? game, OverlayColourProvider colourProvider)
        {
            Activated = () =>
            {
                game?.HandleLink(release.htmlUrl ?? "https://github.com/fuyukiSmkw/osu-lazer-tourney/releases/latest");
                return true;
            };

            TextFlow.AddParagraph("A new version ", s => s.Font = OsuFont.Style.Caption2.With(weight: FontWeight.Bold, size: 14));
            TextFlow.AddText("of lazer!tourney:");
            TextFlow.AddParagraph(release.tagName!, s =>
            {
                s.Colour = colourProvider.Content2;
            });
            TextFlow.AddParagraph("is available! Click to view!");

            IconContent.Masking = true;
            IconContent.CornerRadius = CORNER_RADIUS;
            IconContent.ChangeChildDepth(IconDrawable, float.MinValue);
            LoadComponentAsync(icon = new LazerTourneyIcon()
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            }, IconContent.Add);
        }
    }
}
