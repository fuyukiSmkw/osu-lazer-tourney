> [!CAUTION]
> # USE AT YOUR OWN RISK!
> Using this ruleset may result in an **account ban**, as [warned by peppy](https://github.com/Cai1Hsu/osu-plugins/issues/93).
>
> Although no ban has been reported for such case, the possibility still exists. \
> Do **NOT** use this ruleset in an online/release lazer build, and do **NOT** try to submit any scores to the official server with the ruleset running. \
> Refrain from use until peppy provides further clarification on the permitted scope for custom rulesets.

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

## Build

Requires .NET 10.0 or higher.

Ensure you clone [osu!lazer](https://github.com/ppy/osu) in the parent directory, and then

```bash
dotnet build -c Release
```

Remove `-c Release` for debug build.

## Acknowledgements

* [ppy/osu](https://github.com/ppy/osu): The official osu!lazer project. Lots of reference for multiplayer room & spectator implementation.
* [MATRIX-feather/LLin](https://github.com/MATRIX-feather/LLin): A custom ruleset that provides beatmap downloads from a mirror site and includes a music player. Inspired for ruleset DI

## AI Usage Declaration

[OpenCode](https://opencode.ai/) + [Muse Spark 1.3](https://dev.meta.ai/models/muse-spark).
