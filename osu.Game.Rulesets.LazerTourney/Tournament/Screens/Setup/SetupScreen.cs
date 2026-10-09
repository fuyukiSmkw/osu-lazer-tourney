// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Drawing;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.LazerTourney.Tournament.IO;
using osu.Game.Rulesets.LazerTourney.Tournament.Online;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Setup
{
    public partial class SetupScreen : TournamentScreen
    {
        private FillFlowContainer fillFlow = null!;

        private LoginOverlay? loginOverlay;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private TournamentSceneManager sceneManager { get; set; } = null!;

        [Resolved]
        private MultiplayerClient multiplayerClient { get; set; } = null!;

        [Resolved]
        private TournamentOnlineState onlineState { get; set; } = null!;

        [Resolved]
        private TournamentStorage tournamentStorage { get; set; } = null!;

        [Resolved]
        private RefereeAuthController refereeAuth { get; set; } = null!;

        [Resolved]
        private SaveChangesOverlay saveChanges { get; set; } = null!;

        [Resolved]
        private IDialogOverlay dialogOverlay { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; } = null!;

        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();

        /// <summary>
        /// Whether the quit button is armed (first press done, awaiting confirmation press).
        /// Reset when navigating away from this screen.
        /// </summary>
        private bool quitArmed;


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
                    Child = fillFlow = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(10),
                        Spacing = new Vector2(10),
                    },
                },
            };

            localUser.BindTo(api.LocalUser);
            localUser.BindValueChanged(_ => Schedule(reload));
            reload();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            onlineState.RoomJoined.BindValueChanged(_ => Schedule(reload), true);
        }

        public override void Hide()
        {
            base.Hide();

            // Leaving this screen cancels a pending quit confirmation.
            if (quitArmed)
            {
                quitArmed = false;

                if (IsLoaded)
                    Schedule(reload);
            }
        }

        private void reload()
        {
            bool inRoom = onlineState.RoomJoined.Value;

            var left = new List<Drawable>(coreSettingsSection(inRoom));
            left.AddRange(refereeApiSection());

            var right = new List<Drawable>(visualAudioSettingsSection());

            fillFlow.Children = new Drawable[]
            {
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
                            columnFlow(left),
                            columnFlow(right),
                        },
                    },
                },
            };
        }

        private static FillFlowContainer columnFlow(List<Drawable> children) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Padding = new MarginPadding(20),
            Spacing = new Vector2(10),
            Children = children,
        };

        /// <summary>
        /// Builds the core tournament setup section, including the quit button.
        /// </summary>
        private IEnumerable<Drawable> coreSettingsSection(bool inRoom)
        {
            yield return new OsuSpriteText
            {
                Text = "General settings",
                Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 24),
            };

            yield return new ActionableInfo
            {
                Label = "Current user",
                ButtonText = "Change sign-in",
                Action = () =>
                {
                    api.Logout();

                    if (loginOverlay == null)
                    {
                        AddInternal(loginOverlay = new LoginOverlay
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                        });
                    }

                    loginOverlay.State.Value = Visibility.Visible;
                },
                Value = api.LocalUser.Value.Username,
                Failing = api.IsLoggedIn != true,
                Description = "In order to access the API and display metadata, signing in is required."
            };
            yield return new TournamentSwitcher
            {
                Label = "",
                Description = "Current tournament. This requires a restart to apply changes.",
            };
            yield return new LabelledDropdown<RulesetInfo?>(padded: true)
            {
                Label = "Ruleset",
                Description = "Decides what stats are displayed and which ranks are retrieved for players. This requires a restart to reload data for an existing bracket.",
                Items = rulesets.AvailableRulesets,
                Current = LadderInfo.Ruleset,
                DropdownWidth = 0.5f,
            };
            yield return new ResolutionSelector
            {
                Label = "Window height",
                ButtonText = "Apply",
                Action = applyResolution,
                Description = "Sets the window height and the minimum required width for the tournament layout."
            };
            yield return new ActionableInfo
            {
                Label = "Multiplayer room",
                ButtonText = inRoom ? "Manage" : "Select room",
                Action = () => sceneManager?.SetScreen(typeof(Room.RoomScreen)),
                Value = inRoom ? onlineState.RoomName.Value : "Not joined",
                Failing = !inRoom,
                Description = "Create or join a lazer multiplayer room as the data source. The local user stays spectating."
            };
            yield return new LabelledSwitchButton
            {
                Label = "Auto advance screens",
                Description = "Screens will progress automatically from gameplay -> results -> map pool",
                Current = LadderInfo.AutoProgressScreens,
            };
            yield return new LabelledSwitchButton
            {
                Label = "Display team seeds",
                Description = "Team seeds will display alongside each team at the top in gameplay/map pool screens.",
                Current = LadderInfo.DisplayTeamSeeds,
            };
            yield return new LabelledSwitchButton
            {
                Label = "Automatically download missing beatmaps",
                Description = "Mirrors lazer's Online setting: downloads beatmaps missing locally when joining rooms.",
                Current = config.GetBindable<bool>(OsuSetting.AutomaticallyDownloadMissingBeatmaps),
            };
            yield return new ActionableInfo
            {
                Label = "Quit lazer!tourney",
                ButtonText = quitArmed ? "Click again to quit" : "Quit",
                ButtonColour = colours.Red3,
                Action = onQuitPressed,
            };
        }

        /// <summary>
        /// Builds the Visual &amp; Audio settings section. Controls bind the lazer settings directly,
        /// so edits apply live; values are persisted to the bracket file on save.
        /// </summary>
        private IEnumerable<Drawable> visualAudioSettingsSection()
        {
            yield return new OsuSpriteText
            {
                Text = "Visual & Audio settings",
                Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 24),
            };

            yield return sectionHeader("General");

            yield return new LabelledDropdown<ScoringMode>(padded: true)
            {
                Label = "Score display mode",
                Items = Enum.GetValues<ScoringMode>(),
                Current = config.GetBindable<ScoringMode>(OsuSetting.ScoreDisplayMode),
            };

            yield return new LabelledSwitchButton
            {
                Label = "Hit lighting",
                Current = config.GetBindable<bool>(OsuSetting.HitLighting),
            };

            yield return new LabelledSwitchButton
            {
                Label = "Star fountains",
                Current = config.GetBindable<bool>(OsuSetting.StarFountains),
            };

            yield return sectionHeader("Audio");

            yield return new SettingsSlider<float>
            {
                LabelText = "Positional hitsounds",
                Current = config.GetBindable<float>(OsuSetting.PositionalHitsoundsLevel),
                KeyboardStep = 0.01f,
                DisplayAsPercentage = true,
            };

            yield return new LabelledSwitchButton
            {
                Label = "Always play first combo break",
                Current = config.GetBindable<bool>(OsuSetting.AlwaysPlayFirstComboBreak),
            };

            yield return sectionHeader("Background");

            yield return new SettingsSlider<double>
            {
                LabelText = "Background dim",
                Current = config.GetBindable<double>(OsuSetting.DimLevel),
                KeyboardStep = 0.01f,
                DisplayAsPercentage = true,
            };

            yield return new SettingsSlider<double>
            {
                LabelText = "Background blur",
                Current = config.GetBindable<double>(OsuSetting.BlurLevel),
                KeyboardStep = 0.01f,
                DisplayAsPercentage = true,
            };

            yield return new LabelledSwitchButton
            {
                Label = "Lighten during breaks",
                Current = config.GetBindable<bool>(OsuSetting.LightenDuringBreaks),
            };

            yield return new LabelledSwitchButton
            {
                Label = "Fade playfield when health low",
                Current = config.GetBindable<bool>(OsuSetting.FadePlayfieldWhenHealthLow),
            };

            yield return sectionHeader("Beatmap");

            yield return new LabelledSwitchButton
            {
                Label = "Beatmap skins",
                Current = config.GetBindable<bool>(OsuSetting.BeatmapSkins),
            };

            yield return new LabelledSwitchButton
            {
                Label = "Beatmap colours",
                Current = config.GetBindable<bool>(OsuSetting.BeatmapColours),
            };

            yield return new LabelledSwitchButton
            {
                Label = "Beatmap hitsounds",
                Current = config.GetBindable<bool>(OsuSetting.BeatmapHitsounds),
            };

            yield return new LabelledSwitchButton
            {
                Label = "Show storyboard",
                Current = config.GetBindable<bool>(OsuSetting.ShowStoryboard),
            };

            yield return new SettingsSlider<float>
            {
                LabelText = "Combo colour normalisation",
                Current = config.GetBindable<float>(OsuSetting.ComboColourNormalisationAmount),
                KeyboardStep = 0.01f,
                DisplayAsPercentage = true,
            };

            yield return sectionHeader("HUD");

            yield return new LabelledDropdown<HUDVisibilityMode>(padded: true)
            {
                Label = "HUD visibility mode",
                Items = Enum.GetValues<HUDVisibilityMode>(),
                Current = config.GetBindable<HUDVisibilityMode>(OsuSetting.HUDVisibilityMode),
            };

            yield return new LabelledSwitchButton
            {
                Label = "Show replay settings overlay",
                Current = config.GetBindable<bool>(OsuSetting.ReplaySettingsOverlay),
            };

            yield return new LabelledSwitchButton
            {
                Label = "Always show key overlay",
                Current = config.GetBindable<bool>(OsuSetting.KeyOverlay),
            };

            yield return new LabelledSwitchButton
            {
                Label = "Always show gameplay leaderboard",
                Current = config.GetBindable<bool>(OsuSetting.GameplayLeaderboard),
            };

            yield return new LabelledSwitchButton
            {
                Label = "Show health display when can't fail",
                Current = config.GetBindable<bool>(OsuSetting.ShowHealthDisplayWhenCantFail),
            };

            yield return sectionHeader("Input");

            yield return new SettingsSlider<float>
            {
                LabelText = "Gameplay cursor size",
                Current = config.GetBindable<float>(OsuSetting.GameplayCursorSize),
                KeyboardStep = 0.01f,
            };

            yield return new LabelledSwitchButton
            {
                Label = "Auto cursor size",
                Current = config.GetBindable<bool>(OsuSetting.AutoCursorSize),
            };

            yield return new LabelledSwitchButton
            {
                Label = "Gameplay cursor during touch",
                Current = config.GetBindable<bool>(OsuSetting.GameplayCursorDuringTouch),
            };

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
            {
                yield return new LabelledSwitchButton
                {
                    Label = "Disable Windows key during gameplay",
                    Current = config.GetBindable<bool>(OsuSetting.GameplayDisableWinKey),
                };
            }

            yield return sectionHeader("Mods");

            yield return new LabelledSwitchButton
            {
                Label = "Increase first object visibility",
                Current = config.GetBindable<bool>(OsuSetting.IncreaseFirstObjectVisibility),
            };

            static OsuSpriteText sectionHeader(string text) => new OsuSpriteText
            {
                Text = text,
                Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 18),
            };
        }

        /// <summary>
        /// Builds the Referee hub API section. Client credentials live in the bracket file;
        /// access tokens live per tournament in the shared ini file and are never shared.
        /// </summary>
        private IEnumerable<Drawable> refereeApiSection()
        {
            var statusText = new OsuSpriteText
            {
                Font = OsuFont.GetFont(size: 18),
                Text = refereeAuth.StatusText.Value,
            };

            var guide = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 18))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };

            guide.AddText("Control multiplayer rooms through the referee hub API.\n\n");
            guide.AddText("1. Create an OAuth application at https://osu.ppy.sh/home/account/edit (Account settings, OAuth section). "); guide.AddArbitraryDrawable(new ExternalLinkButton("https://osu.ppy.sh/home/account/edit#new-oauth-application"));
            guide.AddText($"\n\n2. Set its Application Callback URL to {RefereeAuthController.CallbackUrl} (Copy callback URL button below).\n\n");
            guide.AddText("3. Enter the Client ID and Client Secret below.\n\n");
            guide.AddText("4. Press Authorise and approve in the browser.\n\n");
            guide.AddText("Note: unless the application is owned by a bot account, only its owner can complete the authorisation. "
                          + "Credentials and tokens live in tournament-lazer.ini and never leave this machine.");

            yield return new OsuSpriteText
            {
                Text = "Referee hub API settings",
                Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 24),
            };

            yield return guide;

            yield return new ActionableInfo
            {
                Label = "Callback URL",
                ButtonText = "Copy URL",
                Action = refereeAuth.CopyCallbackUrl,
                Value = RefereeAuthController.CallbackUrl,
            };

            yield return new LabelledTextBox(padded: true)
            {
                Label = "Client ID",
                Current = tournamentStorage.RefereeClientId,
            };

            yield return new LabelledTextBox(padded: true)
            {
                Label = "Client secret",
                Current = tournamentStorage.RefereeClientSecret,
            };

            yield return tokenRow = new ActionableInfo
            {
                Label = "Access token",
                ButtonText = "Authorise",
                Action = () => refereeAuth.AuthorizeAsync()
                                          .ContinueWith(_ => Schedule(() => updateTokenRow())),
                Value = refereeAuth.StatusText.Value,
                Failing = !refereeAuth.HasValidToken.Value,
            };

            refereeAuth.RefreshStatusAsync()
                       .ContinueWith(_ => Schedule(() =>
                       {
                           if (!IsDisposed)
                               updateTokenRow();
                       }));
        }

        private ActionableInfo tokenRow = null!;

        private void updateTokenRow()
        {
            tokenRow.Value = refereeAuth.StatusText.Value;
            tokenRow.Failing = !refereeAuth.HasValidToken.Value;
        }

        /// <summary>
        /// Applies the given display height and the minimum required width for the tournament layout.
        /// </summary>
        private void applyResolution(int height)
        {
            int width = (int)Math.Ceiling(height / 768f * TournamentSceneManager.REQUIRED_WIDTH);
            frameworkConfig.GetBindable<Size>(FrameworkSetting.WindowedSize).Value = new Size(width, height);
        }

        private void onQuitPressed() => Schedule(() =>
        {
            if (!quitArmed)
            {
                quitArmed = true;
                reload();
                return;
            }

            quitArmed = false;

            if (saveChanges.HasUnsavedChanges)
            {
                reload();
                dialogOverlay.Push(new ConfirmDialog("You have unsaved changes. Quit without saving?", () => sceneManager?.ExitTournament()));
            }
            else
                sceneManager?.ExitTournament();
        });
    }
}
