// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;

namespace osu.Game.Rulesets.LazerTourney.Tournament
{
    [Cached]
    public partial class TournamentGame : TournamentGameBase
    {
        private Drawable heightWarning = null!;

        private Bindable<WindowMode> windowMode = null!;
        private readonly BindableSize windowSize = new BindableSize();

        private LoadingSpinner loadingSpinner = null!;

        [Cached(typeof(IDialogOverlay))]
        private readonly DialogOverlay dialogOverlay = new DialogOverlay();

        [Cached]
        private readonly OverlayColourProvider overlayColourProvider = new(OverlayColourScheme.Blue);

        private TournamentSceneManager sceneManager = null!;

        private SaveChangesOverlay saveChangesOverlay = null!;

        private GameHost? host;

        [Resolved]
        private OsuGame game { get; set; } = null!;
        private NotificationOverlayToastTray? toastTray;
        private float originalToastTrayWidth;
        private bool toastTrayNarrowed;

        private bool exitConfirmed;

        public override void ExitTournament(Action? beforeExit = null)
        {
            if (!exitConfirmed && saveChangesOverlay?.HasUnsavedChanges == true)
            {
                if (dialogOverlay.CurrentDialog is TournamentExitDialog exitDialog)
                {
                    exitDialog.Flash();
                    return;
                }

                dialogOverlay.Push(new TournamentExitDialog(
                    () =>
                    {
                        saveChangesOverlay.Save();
                        exitConfirmed = true;
                        ExitTournament(beforeExit);
                    },
                    () =>
                    {
                        exitConfirmed = true;
                        ExitTournament(beforeExit);
                    }));

                return;
            }

            // Restore OS cursor hiding (set by OsuGame at startup); lazer cursor
            // visibility is restored automatically via CursorVisible.
            if (host?.Window != null)
                host.Window.CursorState |= CursorState.Hidden;

            restoreToastTray();

            base.ExitTournament(beforeExit);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            // Any exit path that does not go through the save prompt must not silently
            // discard unsaved bracket changes.
            if (!exitConfirmed && saveChangesOverlay?.HasUnsavedChanges == true)
                return true;

            return base.OnExiting(e);
        }

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager frameworkConfig, GameHost host)
        {

            frameworkConfig.BindWith(FrameworkSetting.WindowedSize, windowSize);

            windowMode = frameworkConfig.GetBindable<WindowMode>(FrameworkSetting.WindowMode);

            AddInternal(loadingSpinner = new LoadingSpinner(true, true)
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Margin = new MarginPadding(40),
            });

            // Loaded up front (instead of with the tournament UI) so startup validation can prompt
            // before any tournament screen is constructed. Dialogs must stay in front of everything
            // added later, hence the explicit depth.
            dialogOverlay.Depth = float.MinValue;
            AddInternal(dialogOverlay);

            // in order to have the OS mouse cursor visible, relative mode needs to be disabled.
            // can potentially be removed when https://github.com/ppy/osu-framework/issues/4309 is resolved.
            var mouseHandler = host.AvailableInputHandlers.OfType<MouseHandler>().FirstOrDefault();

            mouseHandler?.UseRelativeMode.Value = false;

            // Disabling relative mode alone doesn't show the OS cursor: OsuGame hides it
            // at startup via CursorState.Hidden (lazer draws its own everywhere).
            // Clear the flag on enter; ExitTournament restores it.
            this.host = host;

            host.Window?.CursorState &= ~CursorState.Hidden;

            loadingSpinner.Show();

            BracketLoadTask.ContinueWith(t => Schedule(() =>
            {
                if (t.IsFaulted)
                {
                    loadingSpinner.Hide();
                    loadingSpinner.Expire();

                    Logger.Error(t.Exception, "Couldn't load bracket with error");
                    AddInternal(new WarningBox($"Your {BRACKET_FILENAME} file could not be parsed. Please check runtime.log for more details."));

                    return;
                }

                saveChangesOverlay = new SaveChangesOverlay
                {
                    // Above the tournament UI (depth 0), below the dialog overlay (float.MinValue).
                    Depth = -1,
                };

                promptInvalidRoundBeatmaps(() => LoadComponentsAsync(new Drawable[]
                {
                    saveChangesOverlay,
                    heightWarning = new WarningBox("Please make the window wider")
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Margin = new MarginPadding(20),
                    },
                    new OsuContextMenuContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = sceneManager = new TournamentSceneManager()
                    },
                }, drawables =>
                {
                    loadingSpinner.Hide();
                    loadingSpinner.Expire();

                    AddRangeInternal(drawables);

                    windowSize.BindValueChanged(size => ScheduleAfterChildren(() =>
                    {
                        int minWidth = (int)(size.NewValue.Height / 768f * TournamentSceneManager.REQUIRED_WIDTH) - 1;
                        heightWarning.Alpha = size.NewValue.Width < minWidth ? 1 : 0;
                    }), true);

                    windowMode.BindValueChanged(_ => ScheduleAfterChildren(() =>
                    {
                        windowMode.Value = WindowMode.Windowed;
                    }), true);

                    OverlayActivationMode.Value = OverlayActivation.UserTriggered; // hide toolbar
                }));
            }));
        }

        /// <summary>
        /// Prompts for every mappool entry the selected ruleset cannot process before any
        /// tournament screen is built: the round editor processes mappool entries eagerly and
        /// would otherwise crash during load. Deleting clears the beatmap data but keeps the
        /// slot; declining (or dismissing) exits the plugin instead.
        /// </summary>
        private void promptInvalidRoundBeatmaps(Action onComplete)
        {
            InvalidRoundBeatmap[] invalid = InvalidRoundBeatmaps.ToArray();

            if (invalid.Length == 0)
            {
                onComplete();
                return;
            }

            showNext(0);

            void showNext(int index)
            {
                if (index >= invalid.Length)
                {
                    onComplete();
                    return;
                }

                InvalidRoundBeatmap entry = invalid[index];

                dialogOverlay.Push(new InvalidBeatmapDialog(
                    entry.DisplayName,
                    entry.Round.Name.Value,
                    entry.Reason,
                    () =>
                    {
                        RoundBeatmapValidator.ClearBeatmapData(entry.Beatmap);
                        SaveChanges();
                        showNext(index + 1);
                    },
                    () => ExitTournament()));
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            narrowToastTray();

            // Alt+F4 / window close button: route through the normal exit path and restore
            // the user's settings immediately, as the debounced background save might not
            // flush before shutdown. (Update thread by contract of the event.)
            host?.ExitRequested += onHostExitRequested;
        }

        protected override void Dispose(bool isDisposing)
        {
            host?.ExitRequested -= onHostExitRequested;

            base.Dispose(isDisposing);
        }

        private void onHostExitRequested()
        {
            ExitTournament();
        }

        /// <summary>
        /// Narrows the notification toast column while the tournament client is active so popups
        /// never cover the stream area. Only the toast tray is narrowed: shrinking the overlay itself
        /// would detach it from the screen edge while hidden, at which point the framework freezes
        /// its whole subtree as masked-away. Must run on the update thread.
        /// </summary>
        private void narrowToastTray()
        {
            if (game == null || toastTrayNarrowed)
                return;

            toastTray = game.ChildrenOfType<NotificationOverlayToastTray>().FirstOrDefault();

            if (toastTray == null)
                return;

            originalToastTrayWidth = toastTray.Width;
            // The tray is relatively sized, so the width is fractional against the overlay width.
            toastTray.Width = TournamentSceneManager.CONTROL_PANEL_WIDTH / NotificationOverlay.WIDTH;
            toastTrayNarrowed = true;
        }

        private void restoreToastTray()
        {
            if (!toastTrayNarrowed || toastTray == null)
                return;

            toastTrayNarrowed = false;
            toastTray.Width = originalToastTrayWidth;
        }
    }
}
