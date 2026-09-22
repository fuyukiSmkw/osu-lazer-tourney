// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Backgrounds;
using osu.Game.Screens.Menu;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay.Components
{
    /// <summary>
    /// A single spectate cell in the tournament gameplay grid.
    /// Shows an idle background (following the main menu background logic, plus a beat-reactive logo)
    /// until gameplay is attached, plus a team-coloured username label always on top.
    /// </summary>
    public partial class SpectateCell : CompositeDrawable
    {
        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        private readonly SeasonalBackgroundLoader backgrounds;

        private OsuSpriteText usernameText = null!;
        private Color4 usernameColour;
        private Container logoContainer = null!;
        private Background? idleBackground;

        private const double idle_fade_in_duration = 300;

        private const float combobreak_peak_scale = 1.3f;
        private const double combobreak_fast_duration = 500;
        private const double combobreak_slow_duration = 2000;

        private Bindable<BackgroundSource> backgroundSource = null!;
        private Bindable<Skin> currentSkin = null!;
        private IBindable<WorkingBeatmap> beatmapCopy = null!;
        private int backgroundVersion;

        /// <summary>
        /// Whether gameplay (or its result/last frame) is currently attached.
        /// </summary>
        public bool HasPlay => PlayArea != null;

        public TournamentPlayerArea? PlayArea { get; private set; }

        public SpectateCell(SeasonalBackgroundLoader backgrounds)
        {
            this.backgrounds = backgrounds;

            RelativeSizeAxes = Axes.Both;

            Masking = true;
            CornerRadius = 6;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                logoContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    Children =
                    [
                        new OsuLogo
                        {
                            Triangles = true,
                            Ripple = true,
                            Scale = new Vector2(0.35f),
                            Origin = Anchor.Centre,
                            Anchor = Anchor.Centre,
                        },
                    ],
                },
                usernameText = new OsuSpriteText
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Margin = new MarginPadding
                    {
                        Horizontal = 5,
                        Vertical = 2,
                    },
                    Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 44),
                    Depth = float.MinValue,
                    Alpha = 0,
                    ShadowColour = new Color4(0, 0, 0, 0.5f),
                },
            };

            backgroundSource = config.GetBindable<BackgroundSource>(OsuSetting.MenuBackgroundSource).GetBoundCopy();
            currentSkin = skins.CurrentSkin.GetBoundCopy();
            beatmapCopy = beatmap.GetBoundCopy();

            backgrounds.SeasonalBackgroundChanged += updateBackgroundRequested;
            backgroundSource.BindValueChanged(_ => updateBackgroundRequested());
            currentSkin.BindValueChanged(_ => updateBackgroundRequested());
            beatmapCopy.BindValueChanged(_ => updateBackgroundRequested());

            updateBackgroundRequested();
        }

        private void updateBackgroundRequested() => Scheduler.AddOnce(updateBackground);

        private void updateBackground() => Schedule(() =>
        {
            if (!IsLoaded || IsDisposed)
                return;

            int version = ++backgroundVersion;

            Background next = createBackground();

            if (idleBackground != null && next.Equals(idleBackground))
            {
                next.Dispose();
                return;
            }

            // Backgrounds (seasonal in particular) carry [LongRunningLoad] and must be loaded
            // via LoadComponentAsync directly, never through AddInternal.
            LoadComponentAsync(next, loaded => Schedule(() =>
            {
                if (version != backgroundVersion || IsDisposed)
                {
                    loaded.Dispose();
                    return;
                }

                var old = idleBackground;
                bool first = old == null;

                old?.FadeOut(400, Easing.OutQuint);
                old?.Expire();

                // Depth must be assigned before adding to the hierarchy.
                loaded.Depth = float.MaxValue;
                AddInternal(idleBackground = loaded);

                // Fade the background in together with the logo so neither pops in alone.
                loaded.FadeInFromZero(idle_fade_in_duration, Easing.OutQuint);

                if (first)
                    logoContainer.FadeInFromZero(idle_fade_in_duration, Easing.OutQuint);
            }));
        });

        /// <summary>
        /// Selects the idle background, mirroring the main menu priority
        /// (<see cref="BackgroundScreenDefault"/>): seasonal first, then the
        /// "Background source" setting, then the default rotation. The seasonal list
        /// arrives asynchronously after login, so early calls fall back to the
        /// setting/default background until the list is available.
        /// </summary>
        private Background createBackground()
        {
            Background? next = backgrounds.LoadNextBackground();

            // Unlike the main menu, the tournament client honours the configured source
            // regardless of supporter status.
            if (next == null)
            {
                switch (backgroundSource.Value)
                {
                    case BackgroundSource.Beatmap:
                    case BackgroundSource.BeatmapWithStoryboard:
                        // No storyboard in cells (performance): plain beatmap background for both.
                        next = new BeatmapBackground(beatmapCopy.Value, defaultTextureName());
                        break;

                    case BackgroundSource.Skin:
                        if (currentSkin.Value is not TrianglesSkin
                            && currentSkin.Value is not ArgonSkin
                            && currentSkin.Value is not DefaultLegacySkin
                            && currentSkin.Value is not RetroSkin)
                            next = new TournamentSkinBackground(currentSkin.Value, defaultTextureName());
                        break;
                }
            }

            next ??= new Background(defaultTextureName());
            return next;
        }

        private static string defaultTextureName() => @$"Menu/menu-background-{RNG.Next(0, 8) + 1}";

        /// <summary>
        /// Assigns a player to this cell. Null/empty username hides the label (idle slot).
        /// </summary>
        public void Assign(string? username, Color4 colour)
        {
            usernameText.FinishTransforms();
            usernameText.Scale = Vector2.One;

            usernameText.Text = username ?? string.Empty;
            usernameText.Colour = usernameColour = colour;
            usernameText.Alpha = string.IsNullOrEmpty(username) ? 0 : 1;
        }

        /// <summary>
        /// Plays the combo-break feedback on the username label: a fast scale-up plus flash
        /// to the combo-break colour, then a slower restore. Mirrors the in-game combo counter
        /// miss feedback (ArgonComboCounter).
        /// </summary>
        public void NotifyComboBreak()
        {
            usernameText.FinishTransforms();

            usernameText.ScaleTo(combobreak_peak_scale, combobreak_fast_duration, Easing.OutQuint)
                        .Then().ScaleTo(1f, combobreak_slow_duration, Easing.OutQuint);

            usernameText.FadeColour(TournamentColours.GetCombobreakNameColour(), combobreak_fast_duration, Easing.OutQuint)
                        .Then().FadeColour(usernameColour, combobreak_slow_duration, Easing.OutQuint);
        }

        /// <summary>
        /// Attaches live gameplay (or its result/last frame) over the idle background.
        /// The username label stays on top via depth.
        /// </summary>
        public void AttachPlayArea(TournamentPlayerArea area)
        {
            PlayArea = area;
            AddInternal(area);
        }

        /// <summary>
        /// Removes gameplay, returning to the idle background.
        /// </summary>
        public void ClearPlay()
        {
            if (PlayArea == null)
                return;

            PlayArea.Expire();
            PlayArea = null;
        }

        /// <summary>
        /// Greys the attached gameplay (quit), keeping the last frame visible.
        /// Matches MultiSpectatorScreen.QuitGameplay which fades to Gray4.
        /// </summary>
        public void FadeGrey()
        {
            PlayArea?.FadeColour(colours.Gray4, 400, Easing.OutQuint);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            backgrounds.SeasonalBackgroundChanged -= updateBackgroundRequested;
            backgroundSource?.UnbindAll();
            currentSkin?.UnbindAll();
            beatmapCopy?.UnbindAll();
        }
    }
}
