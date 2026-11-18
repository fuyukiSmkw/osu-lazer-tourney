// Copyright (c) 2026 fuyukiS <fuyukiS@outlook.jp>. Licensed under the MIT License.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.LazerTourney.Startup;
using osu.Game.Rulesets.LazerTourney.Tournament.Components;
using osu.Game.Rulesets.LazerTourney.Tournament.IO;
using osu.Game.Screens.Menu;
using osuTK;

#nullable enable

namespace osu.Game.Rulesets.LazerTourney.UI;

public partial class LazerTourneySettingsSubsection : RulesetSettingsSubsection
{
    [Resolved]
    private OsuGame game { get; set; } = null!;

    [Resolved(canBeNull: true)]
    private INotificationOverlay? notifications { get; set; }

    private Storage storage = null!;
    private FormDropdown<string> tournamentDropdown = null!;
    private SettingsOverlay? settingsOverlay;
    private Action<ValueChangedEvent<Visibility>>? settingsVisibilityChanged;

    private readonly Bindable<string> selectedTournament = new Bindable<string>();
    private bool suppressSelectionChange;

    public LazerTourneySettingsSubsection(Ruleset ruleset)
        : base(ruleset)
    {
    }

    [BackgroundDependencyLoader]
    private void load(Storage storage)
    {
        this.storage = storage;
        selectedTournament.Value = TournamentStorage.GetCurrentTournament(storage);

        Children = new Drawable[]
        {
            new SettingsItemV2(tournamentDropdown = new FormDropdown<string>
            {
                Caption = "Current tournament",
                HintText = "The selected tournament is used the next time the tournament client is opened.",
                Current = selectedTournament,
                Items = getTournamentItems(),
            })
            {
                ShowRevertToDefaultButton = false,
            },
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Padding = SettingsPanel.CONTENT_PADDING,
                Children = new Drawable[]
                {
                    new CreateTournamentButton(storage, onTournamentCreated)
                    {
                        Padding = new MarginPadding { Right = 2.5f },
                        RelativeSizeAxes = Axes.X,
                        Width = 1 / 3f,
                    },
                    new SettingsButtonV2
                    {
                        Text = "Open folder",
                        Action = openTournamentFolder,
                        Padding = new MarginPadding { Horizontal = 2.5f },
                        RelativeSizeAxes = Axes.X,
                        Width = 1 / 3f,
                    },
                    new SettingsButtonV2
                    {
                        Text = "Refresh",
                        Action = refreshTournaments,
                        Padding = new MarginPadding { Left = 2.5f },
                        RelativeSizeAxes = Axes.X,
                        Width = 1 / 3f,
                    },
                },
            },
            new SettingsButtonV2
            {
                Text = "Open tournament client",
                BackgroundColour = new OsuColour().Blue3,
                Action = () =>
                {
                    if (game.ScreenStack.CurrentScreen is not MainMenu)
                    {
                        notifications?.Post(new TournamentNotification
                        {
                            Text = "The tournament client can only be opened from the main menu.",
                            Transient = true,
                        });
                        return;
                    }

                    TournamentLauncher.TryLaunch(game);
                }
            },
            new OsuTextFlowContainer(t =>
            {
                t.Font = t.Font.With(size: 12);
                t.Colour = OsuColour.Gray(0.6f);
            })
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Margin = new MarginPadding
                {
                    Horizontal = SettingsPanel.CONTENT_MARGINS,
                    Top = 4,
                },
                Text = "Starting the tournament client disables osu!'s crash reporting for the rest of this session.\nRestart osu! to re-enable it.",
            },
        };

        selectedTournament.BindValueChanged(selected =>
        {
            if (suppressSelectionChange || string.IsNullOrEmpty(selected.NewValue) || selected.NewValue == selected.OldValue)
                return;

            TournamentStorage.SetCurrentTournament(storage, selected.NewValue);
        });
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        settingsOverlay = this.FindClosestParent<SettingsOverlay>();

        if (settingsOverlay != null)
        {
            settingsVisibilityChanged = state =>
            {
                if (state.NewValue == Visibility.Visible)
                    refreshTournaments();
            };

            settingsOverlay.State.BindValueChanged(settingsVisibilityChanged);
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        if (settingsOverlay != null && settingsVisibilityChanged != null)
            settingsOverlay.State.ValueChanged -= settingsVisibilityChanged;

        base.Dispose(isDisposing);
    }

    private List<string> getTournamentItems()
    {
        string current = selectedTournament.Value;
        List<string> tournaments = TournamentStorage.ListTournaments(storage).ToList();

        // The current tournament may not have been created on disk yet (first launch uses "default").
        if (!tournaments.Contains(current, StringComparer.Ordinal))
            tournaments.Insert(0, current);

        return tournaments;
    }

    private void refreshTournaments()
    {
        string current = TournamentStorage.GetCurrentTournament(storage);
        List<string> tournaments = TournamentStorage.ListTournaments(storage).ToList();

        if (!tournaments.Contains(current, StringComparer.Ordinal))
            tournaments.Insert(0, current);

        // Changing Items can also change Current (when the old selection disappeared), and
        // changing Current would write the ini again. Synchronise both without persisting.
        suppressSelectionChange = true;

        tournamentDropdown.Items = tournaments;
        selectedTournament.Value = current;

        suppressSelectionChange = false;
    }

    private void openTournamentFolder()
    {
        // Matches the setup screen: open the folder of the selected tournament.
        string current = TournamentStorage.GetCurrentTournament(storage);
        TournamentStorage.GetAllTournamentsStorage(storage).GetStorageForDirectory(current).PresentExternally();
    }

    private void onTournamentCreated(string name)
    {
        // Set the selection first (while suppressed) so the dropdown items and the persisted value agree,
        // then persist it explicitly. This avoids writing the old on-disk value back in between.
        suppressSelectionChange = true;

        selectedTournament.Value = name;
        tournamentDropdown.Items = getTournamentItems();

        suppressSelectionChange = false;

        TournamentStorage.SetCurrentTournament(storage, name);
    }

    private partial class CreateTournamentButton : SettingsButtonV2, IHasPopover
    {
        private readonly Storage allTournaments;
        private readonly Action<string> tournamentCreated;

        public CreateTournamentButton(Storage storage, Action<string> tournamentCreated)
        {
            allTournaments = TournamentStorage.GetAllTournamentsStorage(storage);
            this.tournamentCreated = tournamentCreated;

            Text = "New";
            Action = this.ShowPopover;
        }

        public Popover GetPopover() => new CreateTournamentPopover(allTournaments, tournamentCreated);
    }

    private partial class CreateTournamentPopover : OsuPopover
    {
        private readonly Storage allTournaments;
        private readonly Action<string> tournamentCreated;

        private readonly FocusedTextBox textBox;
        private readonly RoundedButton createButton;
        private readonly OsuTextFlowContainer errorText;

        public CreateTournamentPopover(Storage allTournaments, Action<string> tournamentCreated)
        {
            this.allTournaments = allTournaments;
            this.tournamentCreated = tournamentCreated;

            AutoSizeAxes = Axes.Both;
            Origin = Anchor.TopCentre;

            Child = new FillFlowContainer
            {
                Direction = FillDirection.Vertical,
                AutoSizeAxes = Axes.Y,
                Width = 250,
                Spacing = new Vector2(10),
                Children = new Drawable[]
                {
                    textBox = new FocusedTextBox
                    {
                        PlaceholderText = "Tournament name",
                        FontSize = OsuFont.DEFAULT_FONT_SIZE,
                        RelativeSizeAxes = Axes.X,
                        SelectAllOnFocus = true,
                    },
                    errorText = new OsuTextFlowContainer(t =>
                    {
                        t.Font = OsuFont.GetFont(size: 12);
                        t.Colour = Colour4.FromHex(@"FF6666");
                    })
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                    },
                    createButton = new RoundedButton
                    {
                        Height = 40,
                        RelativeSizeAxes = Axes.X,
                        MatchingFilter = true,
                        Text = "Create",
                    },
                },
            };

            createButton.Action += createTournament;
            textBox.OnCommit += (_, _) => createTournament();

            textBox.Current.BindValueChanged(_ =>
            {
                errorText.Text = string.Empty;
                createButton.Enabled.Value = !string.IsNullOrWhiteSpace(textBox.Text);
            }, true);
        }

        protected override void PopIn()
        {
            textBox.Text = string.Empty;
            errorText.Text = string.Empty;
            createButton.Enabled.Value = false;
            textBox.TakeFocus();

            base.PopIn();
        }

        private void createTournament()
        {
            string name = textBox.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                errorText.Text = "Enter a tournament name.";
                return;
            }

            if (name is "." or ".." || name != name.GetValidFilename() || name.EndsWith('.'))
            {
                errorText.Text = "The name contains characters that are not allowed in a folder name.";
                return;
            }

            if (allTournaments.GetDirectories(string.Empty).Any(t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase)))
            {
                errorText.Text = "A tournament with this name already exists.";
                return;
            }

            try
            {
                // Deliberately only creates the directory. An empty tournament folder is
                // initialised by the tournament client when it is opened.
                allTournaments.GetStorageForDirectory(name);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Logger.Error(e, $@"Couldn't create tournament folder '{name}'");
                errorText.Text = "Could not create the tournament folder.";
                return;
            }

            tournamentCreated(name);
            this.HidePopover();
        }
    }
}
