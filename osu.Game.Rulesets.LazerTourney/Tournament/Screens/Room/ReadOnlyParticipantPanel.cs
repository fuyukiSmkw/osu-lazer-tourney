// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.OnlinePlay.Multiplayer.Participants;
using osu.Game.Screens.Play.HUD;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Room
{
    /// <summary>
    /// Read-only single participant row for the tournament room view.
    /// Visually mirrors <see cref="ParticipantPanel"/> (avatar, flag, team flag, rank, mods, state)
    /// but exposes no room operations: no kick button, no context menu, no slot movement, no team switching.
    /// Safe to show on stream.
    /// </summary>
    public partial class ReadOnlyParticipantPanel : CompositeDrawable
    {
        public const float HEIGHT = 40;

        private readonly MultiplayerRoomUser user;
        private readonly bool isHost;
        private readonly MultiplayerPlaylistItem? currentItem;
        private readonly string? teamName;

        [Resolved]
        private IRulesetStore rulesets { get; set; } = null!;

        private OsuSpriteText username = null!;
        private OsuSpriteText userRankText = null!;
        private UpdateableFlag userFlag = null!;
        private ModDisplay userModsDisplay = null!;
        private StateDisplay userStateDisplay = null!;

        public ReadOnlyParticipantPanel(MultiplayerRoomUser user, bool isHost, MultiplayerPlaylistItem? currentItem, string? teamName = null)
        {
            this.user = user;
            this.isHost = isHost;
            this.currentItem = currentItem;
            this.teamName = teamName;

            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var apiUser = user.User;

            string teamSuffix = teamName != null ? $" • {teamName}" : string.Empty;

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, 18),
                    new Dimension(),
                    new Dimension(GridSizeMode.AutoSize),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = FontAwesome.Solid.Crown,
                            Size = new Vector2(14),
                            Colour = Color4Extensions.FromHex("#F7E65D"),
                            Alpha = isHost ? 1 : 0,
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = 5,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4Extensions.FromHex("#33413C")
                                },
                                new UserCoverBackground
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    RelativeSizeAxes = Axes.Both,
                                    Width = 0.75f,
                                    Colour = ColourInfo.GradientHorizontal(Color4.White.Opacity(0), Color4.White.Opacity(0.25f)),
                                    User = apiUser,
                                },
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Spacing = new Vector2(10),
                                    Direction = FillDirection.Horizontal,
                                    Children = new Drawable[]
                                    {
                                        new UpdateableAvatar(apiUser, isInteractive: false)
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            RelativeSizeAxes = Axes.Both,
                                            FillMode = FillMode.Fit,
                                        },
                                        userFlag = new UpdateableFlag
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Size = new Vector2(28, 20),
                                        },
                                        new Container
                                        {
                                            AutoSizeAxes = Axes.Both,
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Child = new UpdateableTeamFlag(apiUser?.Team, isInteractive: false, showTooltipOnHover: false)
                                            {
                                                Size = new Vector2(40, 20),
                                            },
                                        },
                                        username = new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 18),
                                            Text = $"{apiUser?.Username ?? $"User {user.UserID}"}{teamSuffix}",
                                        },
                                        userRankText = new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Font = OsuFont.GetFont(size: 14),
                                        }
                                    }
                                },
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    AutoSizeAxes = Axes.Both,
                                    Margin = new MarginPadding { Right = 70 },
                                    Spacing = new Vector2(2),
                                    Children = new Drawable[]
                                    {
                                        userModsDisplay = new ModDisplay
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Scale = new Vector2(0.5f),
                                            ExpansionMode = ExpansionMode.AlwaysContracted,
                                        },
                                    }
                                },
                            }
                        },
                        userStateDisplay = new StateDisplay
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        },
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            userFlag.CountryCode = user.User?.CountryCode ?? default;
            userStateDisplay.UpdateStatus(Slot.FromUser(user));

            if (currentItem == null)
                return;

            int userBeatmapId = user.BeatmapId ?? currentItem.BeatmapID;
            int userRulesetId = user.RulesetId ?? currentItem.RulesetID;
            Ruleset? userRuleset = rulesets.GetRuleset(userRulesetId)?.CreateInstance();

            int? currentModeRank = userRuleset == null ? null : user.User?.RulesetsStatistics?.GetValueOrDefault(userRuleset.ShortName)?.GlobalRank;
            userRankText.Text = currentModeRank != null ? $"#{currentModeRank.Value:N0}" : string.Empty;

            // Mods of spectators are typically empty, so this stays hidden for them without extra logic.
            Schedule(() => userModsDisplay.Current.Value = userRuleset == null
                ? Array.Empty<Mod>()
                : (IReadOnlyList<Mod>)user.Mods.Select(m => m.ToMod(userRuleset)).ToList());
        }
    }
}
