// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay.Components
{
    /// <summary>
    /// Tournament copy of the internal <c>SkinBackground</c>: shows the current skin's
    /// menu background, falling back to the default rotation texture when absent.
    /// </summary>
    public partial class TournamentSkinBackground : Background
    {
        private readonly Skin skin;

        public TournamentSkinBackground(Skin skin, string fallbackTextureName)
            : base(fallbackTextureName)
        {
            this.skin = skin;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Sprite.Texture = skin.GetTexture("menu-background") ?? Sprite.Texture;
        }

        public override bool Equals(Background? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return other.GetType() == GetType()
                   && ((TournamentSkinBackground)other).skin.SkinInfo.Equals(skin.SkinInfo);
        }
    }
}
