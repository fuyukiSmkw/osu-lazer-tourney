using System;
using osu.Framework.Allocation;
using osu.Game.Utils;
using osu.Game.Rulesets.LazerTourney.ListenerLoader.Utils;

namespace osu.Game.Rulesets.LazerTourney.ListenerLoader.Handlers;

public partial class SentryLoggerDisabler : AbstractHandler
{
    public SentryLoggerDisabler()
    {
        Scheduler.AddDelayed(() =>
        {
            try
            {
                disableSentryLogger();
            }
            catch (Exception e)
            {
                Logging.LogError(e, "Error disabling SentryLogger");
            }
        }, 100);
    }

    private void disableSentryLogger()
    {
        SentryLogger sl = (SentryLogger)Game.FindInstance(typeof(SentryLogger)) ?? throw new NullDependencyException("SentryLogger not found");
        sl.Dispose();
    }
}
