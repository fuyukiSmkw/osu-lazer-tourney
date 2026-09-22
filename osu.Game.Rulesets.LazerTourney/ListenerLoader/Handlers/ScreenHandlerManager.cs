using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Rulesets.LazerTourney.ListenerLoader.Handlers.ScreenHandlers;
using osu.Game.Screens;

#nullable enable

namespace osu.Game.Rulesets.LazerTourney.ListenerLoader.Handlers;

public partial class ScreenHandlerManager : AbstractHandler
{
    private OsuScreenStack? screenStack;

    private readonly List<AbstractScreenHandler> handlers = new();

    public event Action<(IScreen last, IScreen next)>? OnScreenChanged;

    [BackgroundDependencyLoader]
    private void load()
    {
        if (!hookScreenStack())
            Logging.Log("hookScreenStack failed!");

        // TODO: Register your custom screen handlers here, e.g.:
        // addHandler(new YourScreenHandler());
    }

    private void addHandler(AbstractScreenHandler handler)
    {
        AddInternal(handler);
        handlers.Add(handler);

        if (screenStack != null)
            handler.SetScreenStack(screenStack);
    }

    private bool hookScreenStack()
    {
        lock (this)
        {
            screenStack = Game.ScreenStack;

            screenStack.ScreenExited += onScreenSwitch;
            screenStack.ScreenPushed += onScreenSwitch;

            foreach (var abstractScreenHandler in handlers)
                abstractScreenHandler.SetScreenStack(screenStack);

            return true;
        }
    }

    private void onScreenSwitch(IScreen lastscreen, IScreen newscreen)
    {
        if (newscreen is not Drawable drawable)
            return;

        if (!drawable.IsLoaded)
            drawable.OnLoadComplete += _ => processNewScreen(lastscreen, newscreen);
        else
            processNewScreen(lastscreen, newscreen);
    }

    private void processNewScreen(IScreen lastscreen, IScreen newscreen)
    {
        if (!newscreen.IsCurrentScreen())
            return;

        Logging.Log($"Screen Changed! {lastscreen} -> {newscreen}", level: LogLevel.Debug);

        foreach (var screenHandler in handlers)
            screenHandler.Handle(lastscreen, newscreen);

        OnScreenChanged?.Invoke((lastscreen, newscreen));
    }
}
