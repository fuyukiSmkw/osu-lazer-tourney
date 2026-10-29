// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.LazerTourney.Tournament.IO;
using osuTK;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Setup
{
    internal partial class TournamentSwitcher : CompositeDrawable
    {
        private FormDropdown<string> dropdown = null!;
        private RoundedButton folderButton = null!;
        private RoundedButton reloadTournamentsButton = null!;

        private readonly Bindable<string> selectedTournament = new Bindable<string>();
        private bool suppressSelectionChange;

        [Resolved]
        private TournamentSceneManager? sceneManager { get; set; }

        [Resolved]
        private IDialogOverlay dialogOverlay { get; set; } = null!;

        public TournamentSwitcher()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load(TournamentStorage storage)
        {
            selectedTournament.Default = storage.CurrentTournament.Default;
            selectedTournament.Value = storage.CurrentTournament.Value;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    new SettingsItemV2(dropdown = new FormDropdown<string>
                    {
                        Caption = "Current tournament",
                        HintText = "This requires a restart to apply changes.",
                        Current = selectedTournament,
                        Items = storage.ListTournaments(),
                    }),
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Left = 12, Right = 22, Top = 2, Bottom = 5 },
                        Child = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Horizontal,
                            // Spacing = new Vector2(2, 0),
                            Children = new Drawable[]
                            {
                                folderButton = new SettingsButtonV2
                                {
                                    Text = "Open folder",
                                    // Size = new Vector2(120, 40),
                                    Width = 0.5f,
                                },
                                reloadTournamentsButton = new SettingsButtonV2
                                {
                                    Text = "Refresh",
                                    // Size = new Vector2(100, 40),
                                    Width = 0.5f,
                                },
                            },
                        },
                    },
                },
            };

            selectedTournament.BindValueChanged(v =>
            {
                if (suppressSelectionChange)
                {
                    suppressSelectionChange = false;
                    return;
                }

                if (v.NewValue == v.OldValue)
                    return;

                string requestedTournament = v.NewValue;

                dialogOverlay.Push(new ConfirmDialog(
                    "Changing the current tournament requires a restart. Restart lazer!tourney now?",
                    () => sceneManager?.ExitTournament(() =>
                    {
                        storage.CurrentTournament.Value = requestedTournament;
                        storage.SaveIni(); // Without this, the restarted tourney stays in the old tournament
                    }, restart: true),
                    () =>
                    {
                        suppressSelectionChange = true;
                        selectedTournament.Value = v.OldValue;
                    }));
            });

            folderButton.Action = () => storage.PresentExternally();
            reloadTournamentsButton.Action = () => dropdown.Items = storage.ListTournaments();
        }
    }
}
