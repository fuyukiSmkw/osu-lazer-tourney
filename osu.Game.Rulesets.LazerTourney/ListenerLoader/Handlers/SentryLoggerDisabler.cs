using System;
using Sentry;

namespace osu.Game.Rulesets.LazerTourney.ListenerLoader.Handlers;

public partial class SentryLoggerDisabler : AbstractHandler
{
    public SentryLoggerDisabler()
    {
        Scheduler.AddDelayed(() =>
        {
            try
            {
                disableSentry();
            }
            catch (Exception e)
            {
                Logging.LogError(e, "Error disabling Sentry");
            }
        }, 100);
    }

    /// <summary>
    /// Stops the host game from reporting to Sentry. Closing the SDK is the public
    /// equivalent of disposing lazer's internal <c>SentryLogger</c> and is a no-op
    /// when Sentry was never initialised (tests, undeployed builds, reporting disabled).
    /// </summary>
    private void disableSentry()
    {
        SentrySdk.Close();
    }
}
