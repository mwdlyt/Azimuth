# Azimuth

**Spatial audio placement tool for Windows.**

Azimuth is a desktop application that lets you position audio sources on a 2D circular stage and hear real-time spatial stereo panning and distance-based volume. Drag `.wav` or `.mp3` files onto the canvas, move them around, and export the final mix.

![Screenshot placeholder](docs/screenshot.png)

## Features

- Drag-and-drop audio files onto a circular spatial canvas
- Real-time stereo panning and distance-based volume attenuation
- Multiple simultaneous audio sources with looping playback
- Per-source mute, solo, and volume controls
- Save/load scenes as `.azimuth` JSON project files
- Export stereo mix to WAV or MP3
- Dark Fluent UI theme (Windows 11 style)

## Tech Stack

- **Language:** C# / .NET 8
- **UI:** WPF + [WPF-UI](https://github.com/lepoco/wpfui) (Fluent design)
- **Audio:** [NAudio](https://github.com/naudio/NAudio) + NAudio.Lame
- **Serialization:** System.Text.Json

## Build

```bash
dotnet restore
dotnet build
dotnet run --project Azimuth
```

### Publish single-file .exe

```bash
dotnet publish Azimuth/Azimuth.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## How It Works

Audio sources are positioned relative to the canvas center (the listener). The spatial math calculates:

- **Distance attenuation:** `volume = 1 / (1 + distance² × 8)` — inverse-square falloff
- **Stereo panning:** based on horizontal position (left/right)
- **Per-channel gain:** `leftGain = volume × (1 - pan)`, `rightGain = volume × (1 + pan)`

## License

MIT
