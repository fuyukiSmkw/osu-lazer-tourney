// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Platform;
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
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
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
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private VisualAudioSettingsController visualAudioSettings { get; set; } = null!;

        [Resolved]
        private Clipboard? clipboard { get; set; }

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; } = null!;

        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();

        /// <summary>
        /// Whether the quit button is armed (first press done, awaiting confirmation press).
        /// Reset when navigating away from this screen.
        /// </summary>
        private readonly BindableBool quitArmed = new();

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
            if (quitArmed.Value)
            {
                quitArmed.Value = false;

                if (IsLoaded)
                    Schedule(reload);
            }
        }

        private void reload()
        {
            bool inRoom = onlineState.RoomJoined.Value;

            var left = new List<Drawable>(coreSettingsSection(inRoom));
            left.AddRange(refereeApiSection());
            left.AddRange(webSocketSection());

            var right = new List<Drawable>(visualAudioSettingsSection());
            right.AddRange(rulesetSettingsSection());

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
                Description = "Decides what stats are displayed and which ranks are retrieved for players. Only rulesets available online (usable in multiplayer rooms) are listed. This requires a restart to reload data for an existing bracket.",
                // OnlineID < 0 means local-only (e.g. custom rulesets): the server cannot resolve them in multiplayer rooms.
                // This intentionally does not hardcode the official rulesets, so private servers with more rulesets keep working.
                Items = rulesets.AvailableRulesets.Where(r => r.OnlineID >= 0),
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

            const string quit_text = "Quit";
            const string quit_confirmation_text = "Click again to quit";

            var quitButton = new ActionableInfo
            {
                Label = "Quit lazer!tourney",
                ButtonText = quit_text,
                ButtonColour = colours.Red3,
                Action = onQuitPressed,
            };
            yield return quitButton;

            quitArmed.BindValueChanged(e =>
            {
                if (!e.OldValue)
                    quitButton?.ButtonText = quit_confirmation_text;
                else
                    quitButton?.ButtonText = quit_text;
            });
        }

        /// <summary>
        /// Builds the Visual &amp; Audio settings section. Controls bind the lazer settings directly,
        /// so edits apply live; values are persisted to the bracket file on save.
        /// The result screen style is a tournament-owned setting, so it is not backed up/restored.
        /// </summary>
        private IEnumerable<Drawable> visualAudioSettingsSection()
        {
            yield return new OsuSpriteText
            {
                Text = "Visual & Audio settings",
                Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 24),
            };

            yield return sectionHeader("Result screen");

            yield return new LabelledDropdown<ResultScreenStyle>(padded: true)
            {
                Label = "Result screen",
                Description = "Which result screen is shown after a play or replay finishes. Applies to spectated match cells and showcase replays.",
                Items = Enum.GetValues<ResultScreenStyle>(),
                Current = LadderInfo.ResultScreen,
                DropdownWidth = 0.5f,
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

            yield return new SettingsSlider<double>
            {
                LabelText = "Master volume (window inactive)",
                Current = config.GetBindable<double>(OsuSetting.VolumeInactive),
                KeyboardStep = 0.01f,
                DisplayAsPercentage = true,
            };

            yield return new SettingsSlider<double>
            {
                LabelText = "Music volume",
                Current = audio.VolumeTrack,
                KeyboardStep = 0.01f,
                DisplayAsPercentage = true,
            };

            yield return new SettingsSlider<double>
            {
                LabelText = "Effect volume",
                Current = audio.VolumeSample,
                KeyboardStep = 0.01f,
                DisplayAsPercentage = true,
            };

            yield return new SettingsSlider<double>
            {
                LabelText = "Hitsound volume",
                Current = config.GetBindable<double>(OsuSetting.HitsoundVolume),
                KeyboardStep = 0.01f,
                DisplayAsPercentage = true,
            };

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
        /// Builds the per-ruleset gameplay settings cell below Visual &amp; Audio.
        /// Only the selected ruleset's settings are shown; the other rulesets'
        /// settings are still saved, loaded and restored, just hidden.
        /// Controls bind the lazer settings directly, so edits apply live;
        /// values are persisted to the bracket file on save.
        /// </summary>
        private IEnumerable<Drawable> rulesetSettingsSection()
        {
            yield return new OsuSpriteText
            {
                Text = "Ruleset settings",
                Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 24),
            };

            foreach (var drawable in rulesetSettingsControls())
                yield return drawable;
        }

        private IEnumerable<Drawable> rulesetSettingsControls()
        {
            var settings = RulesetSettingDescriptors.ForShortName(LadderInfo.Ruleset.Value?.ShortName);

            if (settings == null)
            {
                yield return new OsuSpriteText
                {
                    Text = "No configurable settings for this ruleset.",
                    Font = OsuFont.GetFont(size: 14),
                };
                yield break;
            }

            object? manager = visualAudioSettings.GetRulesetConfig(LadderInfo.Ruleset.Value?.ShortName ?? string.Empty);

            if (manager == null)
            {
                yield return rulesetUnavailableNote();
                yield break;
            }

            foreach (var setting in settings)
            {
                Drawable? control = buildRulesetSettingControl(manager, setting);

                if (control != null)
                    yield return control;
            }
        }

        private static OsuSpriteText rulesetUnavailableNote() => new OsuSpriteText
        {
            Text = "Ruleset settings are unavailable (official ruleset assemblies could not be loaded).",
            Font = OsuFont.GetFont(size: 14),
        };

        /// <summary>
        /// Builds one control for a setting, or null when anything on the path is missing.
        /// Enum dropdowns are constructed via reflection; no official ruleset type is named.
        /// </summary>
        private static Drawable? buildRulesetSettingControl(object manager, RulesetSettingDescriptor setting)
        {
            object? bindable = RulesetSettingDescriptors.GetBindable(manager, setting);

            if (bindable == null)
                return null;

            switch (setting.Kind)
            {
                case RulesetSettingKind.Bool:
                    return new LabelledSwitchButton
                    {
                        Label = setting.Label,
                        Current = (Bindable<bool>)bindable,
                    };

                case RulesetSettingKind.Double:
                    return new SettingsSlider<double>
                    {
                        LabelText = setting.Label,
                        Current = (Bindable<double>)bindable,
                        KeyboardStep = setting.SliderStep,
                    };

                case RulesetSettingKind.Enum:
                    return buildEnumDropdown(bindable, setting);

                default:
                    return null;
            }
        }

        private static Drawable? buildEnumDropdown(object bindable, RulesetSettingDescriptor setting)
        {
            Type? enumType = bindable.GetType().GetGenericArguments().FirstOrDefault();

            if (enumType == null || !enumType.IsEnum)
                return null;

            Type dropdownType;
            object dropdown;

            try
            {
                dropdownType = typeof(LabelledDropdown<>).MakeGenericType(enumType);
                dropdown = Activator.CreateInstance(dropdownType, new object[] { true })!;
            }
            catch (ArgumentException)
            {
                return null;
            }

            dropdownType.GetProperty("Label")?.SetValue(dropdown, new LocalisableString(setting.Label));
            dropdownType.GetProperty("Items")?.SetValue(dropdown, Enum.GetValues(enumType));
            dropdownType.GetProperty("Current")?.SetValue(dropdown, bindable);

            return (Drawable)dropdown;
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
                Action = () =>
                {
                    if (refereeAuth.HasValidToken.Value)
                        refereeAuth.Revoke();
                    else
                    {
                        refereeAuth.AuthorizeAsync()
                                   .ContinueWith(_ => Schedule(() => updateTokenRow()));
                    }
                },
                Value = refereeAuth.StatusText.Value,
                Failing = !refereeAuth.HasValidToken.Value,
            };

            yield return connectionRow = new ActionableInfo
            {
                Label = "Hub connection",
                ButtonText = "Connect",
                Action = () =>
                {
                    if (refereeAuth.ConnectionState.Value == RefereeConnectionState.Connected)
                        refereeAuth.DisconnectAsync().ContinueWith(_ => { });
                    else
                    {
                        refereeAuth.ConnectAsync().ContinueWith(_ => { });
                    }
                },
                Value = refereeAuth.ConnectionStatusText.Value,
                ButtonEnabled = refereeAuth.HasValidToken.Value,
            };

            refereeAuth.ConnectionState.BindValueChanged(_ => Schedule(() =>
            {
                if (!IsDisposed)
                    updateConnectionRow();
            }));

            refereeAuth.RefreshStatusAsync()
                       .ContinueWith(_ => Schedule(() =>
                       {
                           if (!IsDisposed)
                           {
                               updateTokenRow();
                               updateConnectionRow();
                           }
                       }));
        }

        private ActionableInfo tokenRow = null!;
        private ActionableInfo connectionRow = null!;

        /// <summary>
        /// Builds the tosu-compatible WebSocket data feed section: explains how to point an
        /// existing tosu ws v1 overlay at the plugin's stream and offers a Copy URL button,
        /// mirroring the referee callback URL row above.
        /// </summary>
        private IEnumerable<Drawable> webSocketSection()
        {
            string wsUrl = $"ws://127.0.0.1:{TourneyDataWebSocketServer.PORT}{TourneyDataWebSocketServer.PATH}";
            string backgroundUrl = $"http://127.0.0.1:{TourneyDataWebSocketServer.PORT}{TourneyDataWebSocketServer.BACKGROUND_PATH}";

            var guide = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 18))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };

            guide.AddText($"While the tournament plugin is open, it hosts a tosu-compatible WebSocket v1 data stream at {wsUrl}.\n\n");
            guide.AddText($"To reuse an existing tosu overlay, point its WebSocket URL from tosu's default (ws://127.0.0.1:24050/ws) to {wsUrl}: the field layout (tourney.manager, tourney.ipcClients, menu.bm, …) follows tosu's v1 schema, so usually only the URL changes.\n\n");
            guide.AddText($"The current beatmap's background image is additionally served at {backgroundUrl}.\n\n");
            guide.AddText("Note: the stream covers the fields stream overlays commonly use; some tosu-specific fields are still placeholders.\n\n");
            guide.AddText("A minimal sample overlay: ");
            guide.AddArbitraryDrawable(new ExternalLinkButton(TourneyDataWebSocketServer.SAMPLE_OVERLAY_URL));

            yield return new OsuSpriteText
            {
                Text = "WebSocket data feed (tosu-compatible)",
                Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 24),
            };

            yield return guide;

            yield return new ActionableInfo
            {
                Label = "WebSocket URL",
                ButtonText = "Copy URL",
                Action = () => clipboard?.SetText(wsUrl),
                Value = wsUrl,
            };
        }

        private void updateTokenRow()
        {
            tokenRow.Value = refereeAuth.StatusText.Value;
            tokenRow.Failing = !refereeAuth.HasValidToken.Value;
            tokenRow.ButtonText = refereeAuth.HasValidToken.Value ? "Revoke" : "Authorise";
            updateConnectionRow();
        }

        private void updateConnectionRow()
        {
            connectionRow.Value = refereeAuth.ConnectionStatusText.Value;

            bool connected = refereeAuth.ConnectionState.Value == RefereeConnectionState.Connected;
            connectionRow.ButtonText = connected ? "Disconnect" : "Connect";
            connectionRow.ButtonEnabled = connected || refereeAuth.HasValidToken.Value;
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
            if (!quitArmed.Value)
            {
                quitArmed.Value = true;
                // reload();
                return;
            }

            quitArmed.Value = false;

            if (saveChanges.HasUnsavedChanges)
            {
                // reload();
                dialogOverlay.Push(new ConfirmDialog("You have unsaved changes. Quit without saving?", () => sceneManager?.ExitTournament()));
            }
            else
                sceneManager?.ExitTournament();
        });
    }
}
