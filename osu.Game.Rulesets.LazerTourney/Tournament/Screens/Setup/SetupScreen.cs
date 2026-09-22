// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Drawing;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets.LazerTourney.Tournament.Online;
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

            fillFlow.Children = new Drawable[]
            {
                new ActionableInfo
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
                },
                new LabelledDropdown<RulesetInfo?>(padded: true)
                {
                    Label = "Ruleset",
                    Description = "Decides what stats are displayed and which ranks are retrieved for players. This requires a restart to reload data for an existing bracket.",
                    Items = rulesets.AvailableRulesets,
                    Current = LadderInfo.Ruleset,
                    DropdownWidth = 0.5f,
                },
                new TournamentSwitcher
                {
                    Label = "Current tournament",
                    Description = "Changes the background videos and bracket to match the selected tournament. This requires a restart to apply changes.",
                },
                new LabelledSwitchButton
                {
                    Label = "Auto advance screens",
                    Description = "Screens will progress automatically from gameplay -> results -> map pool",
                    Current = LadderInfo.AutoProgressScreens,
                },
                new LabelledSwitchButton
                {
                    Label = "Display team seeds",
                    Description = "Team seeds will display alongside each team at the top in gameplay/map pool screens.",
                    Current = LadderInfo.DisplayTeamSeeds,
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
                new ResolutionSelector
                {
                    Label = "Display height",
                    ButtonText = "Apply",
                    Action = applyResolution,
                    Description = "Sets the window height and the minimum required width for the tournament layout."
                },
                new LabelledSwitchButton
                {
                    Label = "Automatically download missing beatmaps",
                    Description = "Mirrors lazer's Online setting: downloads beatmaps missing locally when joining rooms.",
                    Current = config.GetBindable<bool>(OsuSetting.AutomaticallyDownloadMissingBeatmaps),
                },
                new ActionableInfo
                {
                    Label = "Quit lazer!tourney",
                    ButtonText = quitArmed ? "Click again to quit" : "Quit",
                    ButtonColour = colours.Red3,
                    Action = onQuitPressed,
                }
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
