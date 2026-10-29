> [!CAUTION]
> # USE AT YOUR OWN RISK!
> Using this ruleset **may result in an account ban**. osu!'s position on custom rulesets that behave like plugins is not clearly defined, and peppy has [explicitly warned](https://github.com/Cai1Hsu/osu-plugins/issues/93) that users installing rulesets like this "will get them banned". No ban has been reported for such rulesets, but the possibility exists.
>
> ### Where this project stands
> This ruleset does *not* use reflection, and does *nothing* at game startup, and **only after you click** `Open tournament client` in the settings section pushes the tournament client screen. Structurally this follows the exact same pattern as [`lazer-sandbox`](https://github.com/EVAST9919/lazer-sandbox).
> > `lazer-sandbox` is a custom ruleset that hosts its own fullscreen screens (visualizer etc.) and is opened manually from its settings, and has been [listed in peppy's custom ruleset directory since Jun 2021](https://github.com/ppy/osu/discussions/13096#discussioncomment-866167), while in the same discussion where maintainers later replied to other rulesets hosting fullscreen UI, saying that the ruleset API [*"was NOT intended" for this*](https://github.com/ppy/osu/discussions/13096#discussioncomment-1878876). Devs were clearly aware of `lazer-sandbox`, never endorsed it, and never took action against it specifically. The [May 2026 `osu-plugins` related discussions](https://github.com/Cai1Hsu/osu-plugins/issues/93) and [official release blocking certain rulesets](https://github.com/p-720/mosu/blob/2df91468c545dbc1dfe01951e6f9f8d8b991f45d/README.md) did not involve `lazer-sandbox` either, so devs' current stance is unclear.
>
> ### Practical advice
> Treat this as unsupported and expect it to break anytime. Avoid using it on an official online build, don't submit scores with the ruleset running, and use a separate osu! install if possible.

# lazer!tourney

An osu!lazer *plugin* (ruleset) that brings osu! tournament client + osu!tourney to osu!lazer.

Basically a fork of `osu.Game.Tournament` with Multiplayer spectator replacing the chroma area.

## Install

### Script

Download and run the installer script: [Windows](https://github.com/fuyukiSmkw/osu-lazer-tourney/releases/latest/download/osu.Game.Rulesets.LazerTourney.installer.bat) or [Linux/Mac](https://github.com/fuyukiSmkw/osu-lazer-tourney/releases/latest/download/osu.Game.Rulesets.LazerTourney.installer.sh).

Similarly you can use the uninstaller script to remove: [Windows](https://github.com/fuyukiSmkw/osu-lazer-tourney/releases/latest/download/osu.Game.Rulesets.LazerTourney.uninstaller.bat) or [Linux/Mac](https://github.com/fuyukiSmkw/osu-lazer-tourney/releases/latest/download/osu.Game.Rulesets.LazerTourney.uninstaller.sh).

### Manual

Place the built `osu.Game.Rulesets.LazerTourney.dll` into the `rulesets/` subfolder of your osu!lazer directory:

- Windows: `%APPDATA%\osu\rulesets`
- Linux: `~/.local/share/osu/rulesets`

## Usage

Open osu! settings and find the **lazer!tourney** section, then click **`Open tournament client`** (only available from the main menu).

## Build

Requires .NET 10.0 or higher.

Ensure you clone [osu!lazer](https://github.com/ppy/osu) in the parent directory, and then use the `UseLocalOsu` script to add reference to your local clone:

```bash
./UseLocalOsu.sh

# Use this script under Windows
./UseLocalOsu.ps1
```

After that:

```bash
dotnet build -c Release
```

Remove `-c Release` for debug build.

## Acknowledgements

* [ppy/osu](https://github.com/ppy/osu): The official osu!lazer project. Lots of reference for multiplayer room & spectator implementation.
* [MATRIX-feather/LLin](https://github.com/MATRIX-feather/LLin): A custom ruleset that provides beatmap downloads from a mirror site and includes a music player. Inspired for ruleset DI.
* [EVAST9919/lazer-sandbox](https://github.com/EVAST9919/lazer-sandbox): Reference for ruleset DI structure.

## AI Usage Declaration

[OpenCode](https://opencode.ai/) + [Muse Spark 1.3](https://dev.meta.ai/models/muse-spark), [DeepSeek v4.1 Flash](https://www.deepseek.com/en/news/deepseek-v4-1-flash/).
