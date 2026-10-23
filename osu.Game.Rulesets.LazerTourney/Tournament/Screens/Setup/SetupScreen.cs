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

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private LoginOverlay? loginOverlay { get; set; }

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

        private ActionableInfo? quitButton;

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

            quitArmed.BindValueChanged(e =>
            {
                quitButton?.ButtonText = e.NewValue ? "Click again to quit" : "Quit";
            });

            reload();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            refereeAuth.ConnectionState.BindValueChanged(_ => Schedule(() =>
            {
                if (!IsDisposed)
                    updateConnectionRow();
            }));

            refereeAuth.HasValidToken.BindValueChanged(_ => Schedule(() =>
            {
                if (!IsDisposed)
                    updateTokenRow();
            }));

            onlineState.RoomJoined.BindValueChanged(_ => Schedule(reload), true);

            refereeAuth.RefreshStatusAsync().ContinueWith(_ => Schedule(() =>
            {
                if (!IsDisposed)
                {
                    updateTokenRow();
                    updateConnectionRow();
                }
            }));
        }

        public override void Hide()
        {
            base.Hide();

            // Leaving this screen cancels a pending quit confirmation.
            quitArmed.Value = false;
        }

        private void reload()
        {
            bool inRoom = onlineState.RoomJoined.Value;

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
                            columnFlow(new Drawable[]
                            {
                                coreSettingsSection(inRoom),
                                refereeApiSection(),
                                webSocketSection(),
                            }),
                            columnFlow(new Drawable[]
                            {
                                visualAudioSettingsSection(),
                                rulesetSettingsSection(),
                            }),
                        },
                    },
                },
            };
        }

        private static FillFlowContainer columnFlow(Drawable[] children) => new FillFlowContainer
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
        private Drawable coreSettingsSection(bool inRoom)
        {
            quitButton = new ActionableInfo
            {
                Label = "Quit lazer!tourney",
                ButtonText = quitArmed.Value ? "Click again to quit" : "Quit",
                ButtonColour = colours.Red3,
                Action = onQuitPressed,
            };

            return new SetupSection("General settings", prominent: true)
            {
                Children = new Drawable[]
                {
                    new ActionableInfo
                    {
                        Label = "Current user",
                        ButtonText = "Change sign-in",
                        Action = () => loginOverlay?.Show(),
                        Value = api.LocalUser.Value.Username,
                        Failing = api.IsLoggedIn != true,
                        Description = "In order to access the API and display metadata, signing in is required."
                    },
                    new TournamentSwitcher(),
                    dropdown(
                        "Ruleset",
                        LadderInfo.Ruleset,
                        // OnlineID < 0 means local-only (e.g. custom rulesets): the server cannot resolve them in multiplayer rooms.
                        // This intentionally does not hardcode the official rulesets, so private servers with more rulesets keep working.
                        rulesets.AvailableRulesets.Where(r => r.OnlineID >= 0).Select(r => (RulesetInfo?)r),
                        "Decides what stats are displayed and which ranks are retrieved for players. Only rulesets available online (usable in multiplayer rooms) are listed. This requires a restart to reload data for an existing bracket.",
                        showRevertToDefaultButton: false),
                    new ResolutionSelector
                    {
                        Action = applyResolution,
                    },
                    new ActionableInfo
                    {
                        Label = "Multiplayer room",
                        ButtonText = inRoom ? "Manage" : "Select room",
                        Action = () => sceneManager?.SetScreen(typeof(Room.RoomScreen)),
                        Value = inRoom ? onlineState.RoomName.Value : "Not joined",
                        Failing = !inRoom,
                        Description = "Create or join a lazer multiplayer room as the data source. The local user stays spectating."
                    },
                    checkbox(
                        "Auto advance screens",
                        LadderInfo.AutoProgressScreens,
                        "Screens will progress automatically from gameplay -> results -> map pool"),
                    checkbox(
                        "Display team seeds",
                        LadderInfo.DisplayTeamSeeds,
                        "Team seeds will display alongside each team at the top in gameplay/map pool screens."),
                    checkbox(
                        "Automatically download missing beatmaps",
                        config.GetBindable<bool>(OsuSetting.AutomaticallyDownloadMissingBeatmaps),
                        "Mirrors lazer's Online setting: downloads beatmaps missing locally when joining rooms."),
                    quitButton,
                },
            };
        }

        /// <summary>
        /// Builds the Visual &amp; Audio settings section. Controls bind the lazer settings directly,
        /// so edits apply live; values are persisted to the bracket file on save.
        /// The result screen style is a tournament-owned setting, so it is not backed up/restored.
        /// </summary>
        private Drawable visualAudioSettingsSection()
        {
            var inputSettings = new List<Drawable>
            {
                slider(
                    "Gameplay cursor size",
                    config.GetBindable<float>(OsuSetting.GameplayCursorSize),
                    displayAsPercentage: false),
                checkbox(
                    "Auto cursor size",
                    config.GetBindable<bool>(OsuSetting.AutoCursorSize)),
                checkbox(
                    "Gameplay cursor during touch",
                    config.GetBindable<bool>(OsuSetting.GameplayCursorDuringTouch)),
            };

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
            {
                inputSettings.Add(checkbox(
                    "Disable Windows key during gameplay",
                    config.GetBindable<bool>(OsuSetting.GameplayDisableWinKey)));
            }

            return new SetupSection("Visual & Audio settings", prominent: true)
            {
                Children = new Drawable[]
                {
                    subSection("Result screen",
                        enumDropdown(
                            "Result screen",
                            LadderInfo.ResultScreen,
                            "Which result screen is shown after a play or replay finishes. Applies to spectated match cells and showcase replays.")),
                    subSection("General",
                        enumDropdown(
                            "Score display mode",
                            config.GetBindable<ScoringMode>(OsuSetting.ScoreDisplayMode)),
                        checkbox(
                            "Hit lighting",
                            config.GetBindable<bool>(OsuSetting.HitLighting)),
                        checkbox(
                            "Star fountains",
                            config.GetBindable<bool>(OsuSetting.StarFountains))),
                    subSection("Audio",
                        slider(
                            "Master volume (window inactive)",
                            config.GetBindable<double>(OsuSetting.VolumeInactive)),
                        slider(
                            "Music volume",
                            audio.VolumeTrack),
                        slider(
                            "Effect volume",
                            audio.VolumeSample),
                        slider(
                            "Hitsound volume",
                            config.GetBindable<double>(OsuSetting.HitsoundVolume)),
                        slider(
                            "Positional hitsounds",
                            config.GetBindable<float>(OsuSetting.PositionalHitsoundsLevel)),
                        checkbox(
                            "Always play first combo break",
                            config.GetBindable<bool>(OsuSetting.AlwaysPlayFirstComboBreak))),
                    subSection("Background",
                        slider(
                            "Background dim",
                            config.GetBindable<double>(OsuSetting.DimLevel)),
                        slider(
                            "Background blur",
                            config.GetBindable<double>(OsuSetting.BlurLevel)),
                        checkbox(
                            "Lighten during breaks",
                            config.GetBindable<bool>(OsuSetting.LightenDuringBreaks)),
                        checkbox(
                            "Fade playfield when health low",
                            config.GetBindable<bool>(OsuSetting.FadePlayfieldWhenHealthLow))),
                    subSection("Beatmap",
                        checkbox(
                            "Beatmap skins",
                            config.GetBindable<bool>(OsuSetting.BeatmapSkins)),
                        checkbox(
                            "Beatmap colours",
                            config.GetBindable<bool>(OsuSetting.BeatmapColours)),
                        checkbox(
                            "Beatmap hitsounds",
                            config.GetBindable<bool>(OsuSetting.BeatmapHitsounds)),
                        checkbox(
                            "Show storyboard",
                            config.GetBindable<bool>(OsuSetting.ShowStoryboard)),
                        slider(
                            "Combo colour normalisation",
                            config.GetBindable<float>(OsuSetting.ComboColourNormalisationAmount))),
                    subSection("HUD",
                        enumDropdown(
                            "HUD visibility mode",
                            config.GetBindable<HUDVisibilityMode>(OsuSetting.HUDVisibilityMode)),
                        checkbox(
                            "Show replay settings overlay",
                            config.GetBindable<bool>(OsuSetting.ReplaySettingsOverlay)),
                        checkbox(
                            "Always show key overlay",
                            config.GetBindable<bool>(OsuSetting.KeyOverlay)),
                        checkbox(
                            "Always show gameplay leaderboard",
                            config.GetBindable<bool>(OsuSetting.GameplayLeaderboard)),
                        checkbox(
                            "Show health display when can't fail",
                            config.GetBindable<bool>(OsuSetting.ShowHealthDisplayWhenCantFail))),
                    subSection("Input", inputSettings.ToArray()),
                    subSection("Mods",
                        checkbox(
                            "Increase first object visibility",
                            config.GetBindable<bool>(OsuSetting.IncreaseFirstObjectVisibility))),
                },
            };
        }

        /// <summary>
        /// Builds the per-ruleset gameplay settings cell below Visual &amp; Audio.
        /// Only the selected ruleset's settings are shown; the other rulesets'
        /// settings are still saved, loaded and restored, just hidden.
        /// Controls bind the lazer settings directly, so edits apply live;
        /// values are persisted to the bracket file on save.
        /// </summary>
        private Drawable rulesetSettingsSection() => new SetupSection("Ruleset settings", prominent: true)
        {
            Children = rulesetSettingsControls().ToArray(),
        };

        private IEnumerable<Drawable> rulesetSettingsControls()
        {
            var settings = RulesetSettingDescriptors.ForShortName(LadderInfo.Ruleset.Value?.ShortName);

            if (settings == null)
            {
                yield return rulesetMessage("No configurable settings for this ruleset.");
                yield break;
            }

            object? manager = visualAudioSettings.GetRulesetConfig(LadderInfo.Ruleset.Value?.ShortName ?? string.Empty);

            if (manager == null)
            {
                yield return rulesetMessage("Ruleset settings are unavailable (official ruleset assemblies could not be loaded).");
                yield break;
            }

            foreach (var setting in settings)
            {
                Drawable? control = buildRulesetSettingControl(manager, setting);

                if (control != null)
                    yield return control;
            }
        }

        private static Drawable rulesetMessage(string text) => new OsuSpriteText
        {
            Text = text,
            Font = OsuFont.GetFont(size: 14),
            Padding = SettingsPanel.CONTENT_PADDING,
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
                    return new SettingsItemV2(new FormCheckBox
                    {
                        Caption = setting.Label,
                        Current = (Bindable<bool>)bindable,
                    });

                case RulesetSettingKind.Double:
                    return new SettingsItemV2(new FormSliderBar<double>
                    {
                        Caption = setting.Label,
                        Current = (Bindable<double>)bindable,
                        KeyboardStep = setting.SliderStep,
                    });

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
                dropdownType = typeof(FormEnumDropdown<>).MakeGenericType(enumType);
                dropdown = Activator.CreateInstance(dropdownType)!;
            }
            catch (ArgumentException)
            {
                return null;
            }

            dropdownType.GetProperty("Caption")?.SetValue(dropdown, new LocalisableString(setting.Label));
            dropdownType.GetProperty("Current")?.SetValue(dropdown, bindable);

            return new SettingsItemV2((IFormControl)dropdown);
        }

        /// <summary>
        /// Builds the Referee hub API section. Client credentials, access tokens and the
        /// local callback port all live in the shared ini file, never in bracket data.
        /// </summary>
        private Drawable refereeApiSection()
        {
            var guide = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 14))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = SettingsPanel.CONTENT_PADDING,
                Colour = ColourProvider.Content2,
            };

            guide.AddText("Control multiplayer rooms through the referee hub API.\n\n");
            guide.AddText("1. Create an OAuth application at https://osu.ppy.sh/home/account/edit (Account settings, OAuth section). "); guide.AddArbitraryDrawable(new ExternalLinkButton("https://osu.ppy.sh/home/account/edit#new-oauth-application"));
            guide.AddText("\n\n2. Set its Application Callback URL to the callback URL shown below (Copy URL button).\n\n");
            guide.AddText("3. Enter the Client ID and Client Secret below.\n\n");
            guide.AddText("4. Press Authorise and approve in the browser.\n\n");
            guide.AddText("Note: unless the application is owned by a bot account, only its owner can complete the authorisation. "
                          + "Credentials, tokens and ports live in tournament-lazer.ini and never leave this machine.");

            refereePortBox = new PortTextBox(
                "Callback port",
                tournamentStorage.RefereeCallbackPort,
                "Port used by the local OAuth callback listener. It must match the OAuth application's callback URL. "
                + "Locked while an access token is valid or the hub is connected.");

            refereePortBox.ValueChanged += () => Schedule(() =>
            {
                if (!IsDisposed)
                    updateCallbackUrlRow();
            });

            var section = new SetupSection("Referee hub API settings", prominent: true)
            {
                Children = new Drawable[]
                {
                    guide,
                    refereePortBox,
                    callbackUrlRow = new ActionableInfo
                    {
                        Label = "Callback URL",
                        ButtonText = "Copy URL",
                        Action = () => clipboard?.SetText(refereeAuth.CallbackUrl),
                        Value = refereeAuth.CallbackUrl,
                    },
                    textBox("Client ID", tournamentStorage.RefereeClientId, showRevertToDefaultButton: false),
                    textBox("Client secret", tournamentStorage.RefereeClientSecret, showRevertToDefaultButton: false),
                    tokenRow = new ActionableInfo
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
                    },
                    connectionRow = new ActionableInfo
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
                    },
                },
            };

            updateTokenRow();

            return section;
        }

        private ActionableInfo? tokenRow;
        private ActionableInfo? connectionRow;
        private ActionableInfo? callbackUrlRow;
        private ActionableInfo? webSocketUrlRow;
        private PortTextBox? refereePortBox;

        /// <summary>
        /// Builds the tosu-compatible WebSocket data feed section: explains how to point an
        /// existing tosu ws v1 overlay at the plugin's stream and offers a Copy URL button,
        /// mirroring the referee callback URL row above.
        /// </summary>
        private Drawable webSocketSection()
        {
            var guide = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 14))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = SettingsPanel.CONTENT_PADDING,
                Colour = ColourProvider.Content2,
            };

            guide.AddText($"While the tournament plugin is open, it hosts a tosu-compatible WebSocket v1 data stream on the configured port (default {TourneyDataWebSocketServer.DEFAULT_PORT}).\n\n");
            guide.AddText("To reuse an existing tosu overlay, point its WebSocket URL from tosu's default (ws://127.0.0.1:24050/ws) to the WebSocket URL shown below: the field layout (tourney.manager, tourney.ipcClients, menu.bm, …) follows tosu's v1 schema, so usually only the URL changes.\n\n");
            guide.AddText("The current beatmap's background image is additionally served at /backgroundImage on the same port.\n\n");
            guide.AddText("Note: the stream covers the fields stream overlays commonly use; some tosu-specific fields are still placeholders.\n\n");
            guide.AddText("A minimal sample overlay: ");
            guide.AddArbitraryDrawable(new ExternalLinkButton(TourneyDataWebSocketServer.SAMPLE_OVERLAY_URL));

            webSocketUrlRow = new ActionableInfo
            {
                Label = "WebSocket URL",
                ButtonText = "Copy URL",
                Action = () => clipboard?.SetText(webSocketUrl()),
                Value = webSocketUrl(),
            };

            var portBox = new PortTextBox(
                "WebSocket port",
                tournamentStorage.WebSocketPort,
                "Port used by the external overlay. The WebSocket server restarts automatically shortly after a valid change.");

            portBox.ValueChanged += () => Schedule(() =>
            {
                if (!IsDisposed)
                    updateWebSocketUrlRow();
            });

            return new SetupSection("WebSocket data feed (tosu-compatible)", prominent: true)
            {
                Children = new Drawable[]
                {
                    guide,
                    portBox,
                    webSocketUrlRow,
                },
            };
        }

        private string webSocketUrl() => $"ws://127.0.0.1:{TourneyDataWebSocketServer.ResolvePort(tournamentStorage.WebSocketPort.Value)}{TourneyDataWebSocketServer.PATH}";

        private void updateTokenRow()
        {
            if (tokenRow == null)
                return;

            tokenRow.Value = refereeAuth.StatusText.Value;
            tokenRow.Failing = !refereeAuth.HasValidToken.Value;
            tokenRow.ButtonText = refereeAuth.HasValidToken.Value ? "Revoke" : "Authorise";
            updateConnectionRow();
        }

        private void updateConnectionRow()
        {
            if (connectionRow == null)
                return;

            connectionRow.Value = refereeAuth.ConnectionStatusText.Value;

            bool connected = refereeAuth.ConnectionState.Value == RefereeConnectionState.Connected;
            bool connecting = refereeAuth.ConnectionState.Value == RefereeConnectionState.Connecting;

            connectionRow.ButtonText = connecting ? "Connecting..." : connected ? "Disconnect" : "Connect";
            connectionRow.ButtonEnabled = !connecting && (connected || refereeAuth.HasValidToken.Value);

            updateRefereePortState();
        }

        private void updateCallbackUrlRow()
        {
            if (callbackUrlRow == null)
                return;

            callbackUrlRow.Value = refereeAuth.CallbackUrl;
        }

        private void updateWebSocketUrlRow()
        {
            if (webSocketUrlRow == null)
                return;

            webSocketUrlRow.Value = webSocketUrl();
        }

        private void updateRefereePortState()
        {
            if (refereePortBox == null)
                return;

            refereePortBox.Disabled = refereeAuth.HasValidToken.Value
                                      || refereeAuth.ConnectionState.Value == RefereeConnectionState.Connected;
        }

        private static Drawable checkbox(string caption, Bindable<bool> current, string? description = null)
            => new SettingsItemV2(new FormCheckBox
            {
                Caption = caption,
                HintText = hintText(description),
                Current = current,
            });

        private static Drawable enumDropdown<T>(string caption, Bindable<T> current, string? description = null)
            where T : struct, Enum
            => new SettingsItemV2(new FormEnumDropdown<T>
            {
                Caption = caption,
                HintText = hintText(description),
                Current = current,
            });

        private static Drawable dropdown<T>(string caption, Bindable<T> current, IEnumerable<T> items, string? description = null, bool showRevertToDefaultButton = true)
            => new SettingsItemV2(new FormDropdown<T>
            {
                Caption = caption,
                HintText = hintText(description),
                Items = items,
                Current = current,
            })
            {
                ShowRevertToDefaultButton = showRevertToDefaultButton,
            };

        private static Drawable slider(string caption, Bindable<double> current, bool displayAsPercentage = true)
            => new SettingsItemV2(new FormSliderBar<double>
            {
                Caption = caption,
                Current = current,
                KeyboardStep = 0.01f,
                DisplayAsPercentage = displayAsPercentage,
            });

        private static Drawable slider(string caption, Bindable<float> current, bool displayAsPercentage = true)
            => new SettingsItemV2(new FormSliderBar<float>
            {
                Caption = caption,
                Current = current,
                KeyboardStep = 0.01f,
                DisplayAsPercentage = displayAsPercentage,
            });

        private static Drawable textBox(string caption, Bindable<string> current, string? description = null, bool showRevertToDefaultButton = true)
            => new SettingsItemV2(new FormTextBox
            {
                Caption = caption,
                HintText = hintText(description),
                Current = current,
            })
            {
                ShowRevertToDefaultButton = showRevertToDefaultButton,
            };

        private static LocalisableString hintText(string? description) =>
            string.IsNullOrEmpty(description) ? default : new LocalisableString(description);

        private static Drawable subSection(string header, params Drawable[] children) => new SetupSection(header)
        {
            Children = children,
        };

        /// <summary>
        /// A settings subsection using the same spacing and typography as lazer's settings overlay.
        /// Top-level sections use the larger section header style.
        /// </summary>
        private partial class SetupSection : SettingsSubsection
        {
            private readonly LocalisableString header;
            private readonly bool prominent;

            protected override LocalisableString Header => header;

            public SetupSection(LocalisableString header, bool prominent = false)
            {
                this.header = header;
                this.prominent = prominent;
            }

            protected override Drawable CreateHeader() => new OsuSpriteText
            {
                Text = Header,
                Font = prominent ? OsuFont.TorusAlternate.With(size: 30) : OsuFont.GetFont(size: 20),
                Margin = new MarginPadding
                {
                    Vertical = prominent ? 8 : VERTICAL_PADDING,
                    Horizontal = prominent ? 0 : 5,
                },
                Padding = SettingsPanel.CONTENT_PADDING,
            };
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
                return;
            }

            quitArmed.Value = false;
            sceneManager?.ExitTournament();
        });
    }
}
