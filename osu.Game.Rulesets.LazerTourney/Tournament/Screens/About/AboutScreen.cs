// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.LazerTourney.Online;
using osu.Game.Users.Drawables;
using osu.Game.Rulesets.LazerTourney.Graphics;
using osu.Game.Rulesets.LazerTourney.Tournament.Components;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.About
{
    public partial class AboutScreen : TournamentScreen
    {
        private const float base_icon_scale = 4f;
        private const float icon_box_size = 110f;

        private LazerTourneyIcon bigIcon = null!;
        private float iconScale = base_icon_scale;

        [Resolved]
        private UserLookupCache userLookupCache { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notifications { get; set; }

        private GetGitHubRelease? releaseRequest;

        private FillFlowContainer devFlow = null!;
        private FillFlowContainer thanksFlow = null!;

        private static readonly (int id, string note)[] devs =
        [
            (32657919, "dev"),
            (30284920, "waifu & tester"),
            (30973609, "contributor"),
        ];

        private static readonly (int id, string note)[] special_thanks =
        [
            (13870362, "LLin dev"),
            (17268434, "LLin contributor"),
            (24557481, "LLin contributor"),
        ];

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourProvider.Background5,
                },
                new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(20),
                        Spacing = new Vector2(10),
                        Children = new Drawable[]
                        {
                            sectionHeader("About"),
                            new GridContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                ColumnDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension(),
                                },
                                RowDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.AutoSize),
                                },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        new Container
                                        {
                                            Size = new Vector2(icon_box_size),
                                            Child = bigIcon = new LazerTourneyIcon
                                            {
                                                // Explicit size so the icon has a real draw quad:
                                                // a sizeless container is culled as a whole the moment
                                                // its centre leaves the scroll viewport.
                                                Size = new Vector2(20),
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                Scale = new Vector2(base_icon_scale),
                                            },
                                        },
                                        aboutFlow(),
                                    },
                                },
                            },
                            new OsuSpriteText
                            {
                                Text = "devs and contributors:",
                                Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 20),
                            },
                            devFlow = userFlow(),
                            new OsuSpriteText
                            {
                                Text = "special thanks:",
                                Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 20),
                            },
                            thanksFlow = userFlow(),
                            sectionHeader("Guide"),
                            guideText(),
                            sectionHeader("Whatever"),
                            new OsuSpriteText
                            {
                                Text = "Ciallo~ (∠・ω< )⌒★",
                                Font = OsuFont.TorusAlternate.With(size: 20),
                            },
                        },
                    },
                },
                new ControlPanel
                {
                    Children = new Drawable[]
                    {
                        new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Hi :3",
                            Action = () =>
                            {
                                iconScale += 1;
                                bigIcon.Scale = new Vector2(iconScale);
                                bigIcon.Rotation += 20;
                            },
                        },
                        new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Check for updates",
                            Action = checkForUpdates,
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            loadUsers(devs.Select(d => d.id).Concat(special_thanks.Select(t => t.id)).ToArray());
        }

        public override void Hide()
        {
            base.Hide();

            iconScale = base_icon_scale;

            if (IsLoaded)
                bigIcon.Scale = new Vector2(base_icon_scale);
        }

        private static FillFlowContainer userFlow() => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(10),
        };

        private static OsuSpriteText sectionHeader(string text) => new OsuSpriteText
        {
            Text = text,
            Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 28),
            Margin = new MarginPadding
            {
                Top = 5,
            }
        };

        private async void checkForUpdates()
        {
            GitHubRelease? release = null;

            releaseRequest?.Abort();
            releaseRequest = new GetGitHubRelease();
            releaseRequest.Finished += () => release = releaseRequest.ResponseObject ?? null;
            await releaseRequest.AwaitRequest();

            if (release is null || string.IsNullOrEmpty(release.tagName))
                return;

            Schedule(() =>
            {
                if (release.tagName != LazerTourneyRuleset.Version)
                    notifications?.Post(new UpdateAvailableNotification(release));
                else
                    notifications?.Post(new SimpleNotification { Text = "You are already on the latest version." });
            });
        }

        private void loadUsers(int[] ids)
        {
            var notes = devs.Concat(special_thanks).ToDictionary(d => d.id, d => d.note);

            userLookupCache.GetUsersAsync(ids).ContinueWith(task => Schedule(() =>
            {
                var users = task.GetResultSafely().Where(u => u != null).ToDictionary(u => u!.Id);

                foreach (int id in devs.Select(d => d.id))
                {
                    if (users.TryGetValue(id, out APIUser? user) && user != null)
                        devFlow.Add(new UserCard(user, notes.GetValueOrDefault(id, string.Empty)));
                }

                foreach (int id in special_thanks.Select(t => t.id))
                {
                    if (users.TryGetValue(id, out APIUser? user) && user != null)
                        thanksFlow.Add(new UserCard(user, notes.GetValueOrDefault(id, string.Empty)));
                }
            }));
        }

        private static OsuTextFlowContainer aboutFlow()
        {
            var flow = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 22))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            };

            flow.AddText("An osu!lazer plugin (ruleset) that brings osu! tournament client + osu!tourney to osu!lazer.\n\n");
            flow.AddText($"Current version: {LazerTourneyRuleset.Version}.   GitHub: ");
            flow.AddArbitraryDrawable(new ExternalLinkButton("https://github.com/fuyukiSmkw/osu-lazer-tourney"));

            return flow;
        }

        private static OsuTextFlowContainer guideText()
        {
            var flow = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 18))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };

            flow.AddText("0. If you don't already have a tournament setup folder, create a folder for your new tournament in the `tournament-lazer` subfolder of your lazer folder, and then pick it in Setup screen, and restart the plugin, and adjust `Visual & Audio settings` etc. to make your tournament config.\n\n");
            flow.AddText("1. If you have one, put it inside `tournament-lazer` folder, then pick it in Setup screen, and restart the plugin.\n\n");
            flow.AddText("2. Input your stream resolution height in `Display height` and click the button to set window size to the required minimum width.\n\n");
            flow.AddText("3. Go to the Room screen, create or join the lazer multiplayer room for the match.\n\n");
            flow.AddText("4. Use the Map Pool screen to switch the room's current map; the room follows the pick like a referee.\n\n");
            flow.AddText("5. Open the Gameplay screen to spectate players, with team scores, chat, room operations and results built in.\n\n");
            flow.AddText("6. On the Showcase screen, you can directly drag & drop .osr replay file to instantly play it.\n\n");
            flow.AddText("7. Other stuff are mostly the same as / similar to old osu tournament client, and old `brackets.json` is compatible. More help: ");
            flow.AddArbitraryDrawable(new ExternalLinkButton("https://osu.ppy.sh/wiki/en/osu%21_tournament_client"));
            flow.AddText("\n\n8. Enjoy :)");

            return flow;
        }

        private partial class UserCard : CompositeDrawable
        {
            public UserCard(APIUser user, string note)
            {
                Width = 250;
                Height = 150;
                Masking = true;
                CornerRadius = 8;

                InternalChildren = new Drawable[]
                {
                    new Users.CoverBackground
                    {
                        RelativeSizeAxes = Axes.Both,
                        Model = user,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(0, 0, 0, 0.65f),
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Spacing = new Vector2(4),
                        Children = new Drawable[]
                        {
                            new UpdateableAvatar(user)
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Size = new Vector2(80),
                                Masking = true,
                                CornerRadius = 40,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = user.Username,
                                Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 22),
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = note,
                                Font = OsuFont.GetFont(size: 16),
                                Colour = new Color4(200, 200, 200, 255),
                            },
                        },
                    },
                };
            }
        }
    }
}
