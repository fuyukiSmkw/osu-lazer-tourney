// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Models.ModMultiplier;
using osu.Game.Rulesets.LazerTourney.Tournament.Screens.About;
using osu.Game.Rulesets.Mods;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Editors.Components
{
    /// <summary>
    /// Text input for a ModMult custom mod-multiplier rule with live validation feedback.
    /// An empty rule means "no custom multiplier" (maps additionally fall back to the round default).
    /// </summary>
    public partial class ModMultiplierRuleEditor : CompositeDrawable
    {
        /// <summary>
        /// The rule text being edited. Set before load; defaults to a detached bindable.
        /// </summary>
        public Bindable<string> Current { get; set; } = new Bindable<string>(string.Empty);

        public string LabelText { get; set; } = "Mod multiplier rule";

        /// <summary>
        /// Whether to show the (?) help icon jumping to the About screen. Set before load.
        /// </summary>
        public bool ShowHelpIcon { get; set; } = true;

        /// <summary>
        /// Hint shown when the rule is empty (e.g. what the fallback is).
        /// </summary>
        public string EmptyHint { get; set; } = "Empty = x1.";

        [Resolved]
        private LadderInfo ladder { get; set; } = null!;

        [Resolved]
        private TournamentSceneManager sceneManager { get; set; } = null!;

        private OsuSpriteText hintText = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            var labelFlow = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 14))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };

            labelFlow.AddText(LabelText);

            if (ShowHelpIcon)
            {
                // Pad the icon like FormFieldCaption does, so it has no awkward left margin on wrap.
                labelFlow.AddText(" ", t => t.Width = 5);
                labelFlow.AddArbitraryDrawable(new RuleHelpIcon { Action = () => sceneManager.SetScreen(typeof(AboutScreen)) });
            }

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(2),
                Children = new Drawable[]
                {
                    labelFlow,
                    new SettingsTextBox
                    {
                        RelativeSizeAxes = Axes.X,
                        Current = Current,
                    },
                    hintText = new OsuSpriteText
                    {
                        Font = OsuFont.GetFont(size: 12),
                        Colour = OsuColour.Gray(0.8f),
                    },
                }
            };

            Current.BindValueChanged(_ => validate(), true);
            ladder.Ruleset.BindValueChanged(_ => validate());
        }

        private void validate() => Schedule(() =>
        {
            if (!IsLoaded)
                return;

            string source = Current.Value ?? string.Empty;

            if (string.IsNullOrWhiteSpace(source))
            {
                showHint(EmptyHint, OsuColour.Gray(0.8f));
                return;
            }

            if (!ModMultRule.TryParse(source, out var rule, out string? syntaxError) || rule == null)
            {
                showHint(syntaxError ?? "Invalid rule.", Color4.Red);
                return;
            }

            var ruleset = ladder.Ruleset.Value?.CreateInstance();

            // Names are validated only when the ruleset is known; syntax is always checked above.
            if (ruleset != null)
            {
                string? nameError = rule.Validate(ruleset.AllMods.OfType<Mod>().ToArray());
                if (nameError != null)
                {
                    showHint(nameError, Color4.Red);
                    return;
                }
            }

            showHint("Valid rule.", OsuColour.Gray(0.8f));
        });

        private void showHint(string text, Color4 colour)
        {
            hintText.Text = text;
            hintText.Colour = colour;
        }

        /// <summary>
        /// Small (?) icon next to the label, jumping to the syntax guide on the About screen.
        /// Mirrors the hint icons of lazer settings (see FormFieldCaption) and the behaviour of ExternalLinkButton.
        /// </summary>
        private partial class RuleHelpIcon : CompositeDrawable, IHasTooltip
        {
            public Action? Action { get; set; }

            private readonly SpriteIcon icon;
            private Color4 hoverColour;

            public RuleHelpIcon()
            {
                Size = new Vector2(12);
                Anchor = Anchor.BottomLeft;
                Origin = Anchor.BottomLeft;
                Y = 1f;

                InternalChildren = new Drawable[]
                {
                    icon = new SpriteIcon
                    {
                        Icon = FontAwesome.Solid.QuestionCircle,
                        RelativeSizeAxes = Axes.Both,
                    },
                    new HoverClickSounds(),
                };
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                hoverColour = colours.Yellow;
            }

            protected override bool OnHover(HoverEvent e)
            {
                icon.FadeColour(hoverColour, 200);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                icon.FadeColour(Color4.White, 200);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                Action?.Invoke();
                return true;
            }

            public LocalisableString TooltipText => "See the About screen for the syntax guide.";
        }
    }
}
