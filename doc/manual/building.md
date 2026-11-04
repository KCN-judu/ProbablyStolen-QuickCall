# Building


## Requirements

- *Probably Stolen*, game version 0.46D. The build compiles against the game's own assemblies in `Probably Stolen_Data/Managed`.
- Git Bash or another POSIX shell on Windows.
- No other tools: the C# 5 compiler that ships with Windows (.NET Framework 4) builds the mod.

## Build

```bash
./build.sh            # out/quick_call
./build.sh --install  # also copies it to the game's Mods folder
```

Two environment variables override the defaults:

| Variable | Default |
| --- | --- |
| `GAME` | `/d/SteamLibrary/steamapps/common/Probably Stolen` |
| `CSC` | `/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe` |

## Install by hand

Copy the folder `out/quick_call` into the game's `Mods` folder, start the game, and enable the mod in the Mods menu. Mods load when the game starts, so restart after enabling or updating one.

The mod declares no capabilities, so the game enables it without a permission prompt.
