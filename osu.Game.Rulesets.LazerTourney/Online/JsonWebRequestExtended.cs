// Copyright (c) 2025 fuyukiS <fuyukiS@outlook.jp>. Licensed under the MIT License.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using osu.Framework.IO.Network;

namespace osu.Game.Rulesets.LazerTourney.Online;

public partial class JsonWebRequestExtended<T>(string url = null, params object[] args) : JsonWebRequest<T>(url, args)
{
    public Task<T> AwaitRequest()
    {
        var tcs = new TaskCompletionSource<T>();

        Finished += () => tcs.TrySetResult(ResponseObject);
        Failed += ex => tcs.TrySetException(ex);

        try
        {
            // PerformAsync also faults on request errors. Observe it here so failures reported
            // through the returned task do not additionally become unobserved task exceptions.
            _ = observeFailure(PerformAsync());
        }
        catch (Exception e)
        {
            tcs.TrySetException(e);
        }

        return tcs.Task;
    }

    private static async Task observeFailure(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // Surfaced to the caller through the Failed event and AwaitRequest's task.
        }
    }
}

