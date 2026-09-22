// Copyright (c) 2025 MATRIX-feather. Licensed under the MIT Licence.
// Copyright (c) 2025 fuyukiS <fuyukiS@outlook.jp>. Licensed under the MIT License.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Rulesets.LazerTourney.Tournament;

#nullable enable

namespace osu.Game.Rulesets.LazerTourney.ListenerLoader.Handlers;

public partial class LazerTourneyRulesetIconListener : AbstractHandler
{
    [Resolved(canBeNull: true)]
    private IBindable<RulesetInfo>? ruleset { get; set; }

    [Resolved]
    private RulesetStore? rulesets { get; set; }

    [Resolved(canBeNull: true)]
    private OsuGame? game { get; set; }

    [BackgroundDependencyLoader]
    private void load()
    {
        if (ruleset is not Bindable<RulesetInfo> rs) return;

        ruleset.BindValueChanged(v =>
        {
            if (v.NewValue.ShortName != LazerTourneyRuleset.SHORT_NAME
            || v.OldValue.ShortName == LazerTourneyRuleset.SHORT_NAME
            )
                return;
            if (v.OldValue == null)
            {
                var std = rulesets?.AvailableRulesets.FirstOrDefault();
                if (std is not null)
                    rs.Value = std;
                return;
            }

            rs.Value = v.OldValue;

            game?.ScreenStack.Push(new TournamentGame());
        });
    }
}
