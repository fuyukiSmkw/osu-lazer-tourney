// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;

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

        public override void ExitTournament()
        {
            // Restore OS cursor hiding (set by OsuGame at startup); lazer cursor
            // visibility is restored automatically via CursorVisible.
            if (host?.Window != null)
                host.Window.CursorState |= CursorState.Hidden;

            base.ExitTournament();
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

            // in order to have the OS mouse cursor visible, relative mode needs to be disabled.
            // can potentially be removed when https://github.com/ppy/osu-framework/issues/4309 is resolved.
            var mouseHandler = host.AvailableInputHandlers.OfType<MouseHandler>().FirstOrDefault();

            mouseHandler?.UseRelativeMode.Value = false;

            // Disabling relative mode alone doesn't show the OS cursor: OsuGame hides it
            // at startup via CursorState.Hidden (lazer draws its own everywhere).
            // Clear the flag on enter; ExitTournament restores it.
            this.host = host;

            if (host.Window != null)
                host.Window.CursorState &= ~CursorState.Hidden;

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

                // Created and cached before async load: LoadComponentsAsync resolves dependencies
                // during background load, which happens before the completion callback runs.
                saveChangesOverlay = new SaveChangesOverlay
                {
                    Depth = float.MinValue,
                };

                // Make the overlay resolvable for screens (e.g. SetupScreen quit confirmation).
                CacheDependency(saveChangesOverlay);

                LoadComponentsAsync(new Drawable[]
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
                    dialogOverlay
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
                });
            }));
        }
    }
}
