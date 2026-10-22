// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.LazerTourney.Online;
using osu.Game.Rulesets.LazerTourney.Tournament.Online;
using osu.Game.Users.Drawables;
using osu.Game.Rulesets.LazerTourney.Graphics;
using osu.Game.Rulesets.LazerTourney.Tournament.Components;
using osu.Game.Rulesets.LazerTourney.Tournament.Models.ModMultiplier;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.Editors.Components;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
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

        // Private mod picker backing the multiplier playground below (same picker as room creation).
        private readonly UserModSelectOverlay playgroundOverlay = new UserModSelectOverlay();
        private readonly Bindable<string> playgroundRule = new Bindable<string>(string.Empty);
        private Mod[] playgroundMods = [];
        private ModDisplay playgroundModDisplay = null!;
        private OsuTextFlowContainer playgroundResult = null!;

        private static readonly (int id, string note)[] devs =
        [
            (32657919, "dev"),
            (30284920, "waifu & tester"),
            (30973609, "contributor"),
        ];

        private static readonly (int id, string note)[] special_thanks =
        [
            (13870362, "LLin dev"),
            (30732590, "g0v0 dev (V2 results)"),
            (30656658, "g0v0 dev (V2 results)"),
            (5382216, "Torii dev (Legacy results)"),
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
                            new GridContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                ColumnDimensions = new[]
                                {
                                    new Dimension(),
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
                                        new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Padding = new MarginPadding { Right = 10 },
                                            Spacing = new Vector2(10),
                                            Children = new Drawable[]
                                            {
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
                                        new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Padding = new MarginPadding { Left = 10 },
                                            Spacing = new Vector2(10),
                                            Children = new Drawable[]
                                            {
                                                sectionHeader("Mod multiplier rules"),
                                                modMultiplierGuideText(),
                                                new OsuSpriteText
                                                {
                                                    Text = "Playground",
                                                    Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 20),
                                                },
                                                new ModMultiplierRuleEditor
                                                {
                                                    LabelText = "Try a rule",
                                                    EmptyHint = "Empty = x1.",
                                                    ShowHelpIcon = false,
                                                    Current = playgroundRule,
                                                },
                                                new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Horizontal,
                                                    Spacing = new Vector2(10),
                                                    Children = new Drawable[]
                                                    {
                                                        new RoundedButton
                                                        {
                                                            RelativeSizeAxes = Axes.None,
                                                            Size = new Vector2(120, 40),
                                                            Text = "Select mods",
                                                            Action = openPlaygroundPicker,
                                                        },
                                                        playgroundModDisplay = new ModDisplay
                                                        {
                                                            Anchor = Anchor.CentreLeft,
                                                            Origin = Anchor.CentreLeft,
                                                            Scale = new Vector2(0.5f),
                                                            ExpansionMode = ExpansionMode.AlwaysContracted,
                                                        },
                                                    },
                                                },
                                                playgroundResult = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 18))
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                },
                                            },
                                        },
                                    },
                                },
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
                playgroundOverlay,
            };

            LadderInfo.Ruleset.BindValueChanged(ruleset => playgroundOverlay.Ruleset.Value = ruleset.NewValue, true);
            playgroundOverlay.State.BindValueChanged(v =>
            {
                // Refresh on close rather than on every tweak: setting changes mutate the shared
                // instances in place without changing the SelectedMods list, and the playground
                // sits behind the open overlay anyway, so close-time refresh loses no visible feedback.
                if (v.NewValue == Visibility.Hidden)
                {
                    playgroundMods = playgroundOverlay.SelectedMods.Value.ToArray();
                    refreshPlayground();
                }
            });
            playgroundRule.BindValueChanged(_ => refreshPlayground(), true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            loadUsers(devs.Select(d => d.id).Concat(special_thanks.Select(t => t.id)).ToArray());
            refreshPlayground();
        }

        private void openPlaygroundPicker()
        {
            playgroundOverlay.SelectedMods.Value = playgroundMods;
            playgroundOverlay.Show();
        }

        private void refreshPlayground() => Schedule(() =>
        {
            if (!IsLoaded)
                return;

            playgroundModDisplay.Current.Value = playgroundMods;

            double extra = 1;

            if (ModMultRule.TryParse(playgroundRule.Value, out var rule, out _) && rule != null)
                extra = rule.Evaluate(playgroundMods);

            double original = originalMultiplier(playgroundMods);
            double total = original * extra;
            long finalScore = (long)Math.Round(total * 1_000_000);

            playgroundResult.Clear();
            GuideTextFormatter.AddFormattedText(playgroundResult,
                $"Original multiplier: **x{original:0.###}**    Custom multiplier: **x{extra:0.###}**\n" +
                $"Final multiplier: **x{total:0.###}**    Max score: **{finalScore:N0}**");
        });

        /// <summary>
        /// Official score multiplier for the given mods, via the same path as gameplay scoring.
        /// Evaluated against a neutral reference difficulty since the playground has no beatmap;
        /// only difficulty-relative mods (e.g. DA) are affected by this choice.
        /// </summary>
        private double originalMultiplier(Mod[] mods)
        {
            try
            {
                var ruleset = LadderInfo.Ruleset.Value?.CreateInstance();

                if (ruleset == null)
                    return 1;

                return ruleset.CreateScoreMultiplierCalculator(new ScoreMultiplierContext(new BeatmapDifficulty())).CalculateFor(mods);
            }
            catch
            {
                return 1;
            }
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

            GuideTextFormatter.AddFormattedText(flow, "An osu!lazer plugin (ruleset) that brings osu! tournament client + osu!tourney to osu!lazer.\n\n");
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

            GuideTextFormatter.AddFormattedText(flow, "0. If you don't already have a tournament setup folder, create a folder for your new tournament in the `tournament-lazer` subfolder of your lazer folder, and then pick it in Setup screen, and **restart the plugin**, and adjust `Visual & Audio settings` etc. to make your tournament config.\n\n");
            GuideTextFormatter.AddFormattedText(flow, "1. If you have one, put it inside `tournament-lazer` folder, then pick it in Setup screen, and **restart the plugin**.\n\n");
            GuideTextFormatter.AddFormattedText(flow, "2. Input your stream resolution height in `Display height` and click the button to set window size to the **required minimum width**.\n\n");
            GuideTextFormatter.AddFormattedText(flow, "3. Go to the Room screen, create or join the lazer multiplayer room for the match.\n\n");
            GuideTextFormatter.AddFormattedText(flow, "4. Use the Map Pool screen to switch the room's current map; the room follows the pick **like a referee**.\n\n");
            GuideTextFormatter.AddFormattedText(flow, "5. Open the Gameplay screen to **spectate players**, with team scores, chat, room operations and results built in.\n\n");
            GuideTextFormatter.AddFormattedText(flow, "6. On the Showcase screen, you can directly drag & drop `.osr` replay file to instantly play it.\n\n");
            GuideTextFormatter.AddFormattedText(flow, "7. Other stuff are mostly the same as / similar to old osu tournament client, and old `brackets.json` is **compatible**. More help: ");
            flow.AddArbitraryDrawable(new ExternalLinkButton("https://osu.ppy.sh/wiki/en/osu%21_tournament_client"));
            GuideTextFormatter.AddFormattedText(flow, "\n\n8. **Referee hub API**: the room can also be controlled programmatically (from bots or external tools) through the referee hub API; get credentials and authorise in the Setup screen (`Referee hub API settings`).\n\n");
            GuideTextFormatter.AddFormattedText(flow, "9. **WebSocket data feed**: while the plugin is open it hosts a tosu-compatible WebSocket v1 stream; to reuse an existing tosu v1 overlay, just point it from tosu's `ws://127.0.0.1:24050/ws` to `ws://127.0.0.1:50727/ws` (see the Setup screen's `WebSocket data feed`). Sample overlay: ");
            flow.AddArbitraryDrawable(new ExternalLinkButton(TourneyDataWebSocketServer.SAMPLE_OVERLAY_URL));
            GuideTextFormatter.AddFormattedText(flow, "\n\n10. Enjoy :)");

            return flow;
        }

        private static OsuTextFlowContainer modMultiplierGuideText()
        {
            var flow = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 18))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };

            GuideTextFormatter.AddFormattedText(flow, "Custom mod multipliers let organisers define their own score multipliers instead of the official ones. **Each score** is multiplied by the rule result, evaluated against **that player's own mods**; team totals sum the adjusted scores.\n\n");
            GuideTextFormatter.AddFormattedText(flow, "Every round has a **default rule** (Rounds editor), and each map can **fully override** it with its own rule. An empty rule means **x1**; maps with an empty rule **fall back** to the round default.\n\n");
            GuideTextFormatter.AddFormattedText(flow, "**Basics:** a bare mod acronym tests whether it is enabled. `EZ ? 2` means x2 with EZ and x1 otherwise (a missing `?:` else **defaults to `1`**). Combine factors with `*`: `(EZ ? 2) * (HD ? 1.05)`.\n\n");
            GuideTextFormatter.AddFormattedText(flow, "**Conditions:** `&&`, `||`, `!`, comparisons `< <= > >= == !=` (texts compare case-insensitively), and parentheses. `has(A, B)` is true when any listed mod is enabled, `all(A, B)` when every listed one is, `only(A, B)` when the enabled set is exactly the listed set ignoring settings, and `count()` is the number of enabled mods. **Example:** `only(AD, BU) ? 1.5 : only(AD, BU, TC) ? 1.6 : only(AD, TC) ? 1.1`.\n\n");
            GuideTextFormatter.AddFormattedText(flow, "**Settings:** `MOD.setting` reads a setting value, e.g. `DT.speed_change`. Names ignore case and underscores; enum values compare as text, e.g. `AD.style == \"Gravity\"`. A setting of an absent (or unset) mod is **null**: use `??` for a fallback, or test with `== null`. **Example:** `(DA ? (9 - (DA.approach_rate ?? 9)) * 0.2 + 1 : 1) * (TC ? 1.1 : 1)`.\n\n");
            GuideTextFormatter.AddFormattedText(flow, "**Numbers:** `+ - * / %`, unary `-`, and helpers `min, max, clamp(x, lo, hi), abs, floor, ceil, round(x)` / `round(x, digits)`. `//` and `#` start line comments.\n\n");
            GuideTextFormatter.AddFormattedText(flow, "**Invalid rules** are ignored as **x1** (the editor shows the error live). Results must be **finite numbers >= 0**; anything else falls back to x1.");

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
