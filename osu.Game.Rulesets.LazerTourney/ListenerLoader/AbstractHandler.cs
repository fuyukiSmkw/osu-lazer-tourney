// Copyright (c) 2025 MATRIX-feather. Licensed under the MIT Licence.
// Copyright (c) 2025 fuyukiS <fuyukiS@outlook.jp>. Licensed under the MIT License.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Rulesets.LazerTourney.ListenerLoader;

#nullable enable

public abstract partial class AbstractHandler : CompositeDrawable
{
    [Resolved]
    private OsuGame game { get; set; } = null!;

    protected OsuGame Game => game;
}
