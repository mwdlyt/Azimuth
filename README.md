<h1 align="center">
  Azimuth
</h1>

<p align="center">
  A spatial audio placement tool for Windows.<br/>
  Drop audio sources onto a 2D stage, position them around a listener, and hear the mix change in real time.
</p>

<p align="center">
  <a href="https://github.com/mwdlyt/Azimuth/releases"><img alt="Release" src="https://img.shields.io/github/v/release/mwdlyt/Azimuth?style=flat&colorA=18181B&colorB=7C5CFC&label=release"></a>
  <a href="https://dotnet.microsoft.com/"><img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&colorA=18181B"></a>
  <a href="LICENSE"><img alt="MIT License" src="https://img.shields.io/badge/license-MIT-white?style=flat&colorA=18181B"></a>
</p>

<br/>

<p align="center">
  <!-- Replace with a real screenshot: open Azimuth with sources + orbit paths, save as docs/screenshot.png -->
  <img alt="Azimuth" width="900" src="docs/screenshot.png">
</p>

## About

Azimuth gives you a circular stage with a listener at its center. Audio files dragged onto the stage become sources that you can position freely. Each source's stereo panning and volume are computed in real time from its distance and angle relative to the listener. Sources can also follow orbital paths for continuous spatial movement.

The result is an intuitive way to prototype spatial audio mixes, design soundscapes, or experiment with positional audio without touching a DAW.

## Features

**Spatial engine** --- Real-time stereo panning and inverse-square distance attenuation. Sources beyond the visible canvas edge continue to produce audio at reduced volume, giving depth beyond the stage boundary.

**Orbital motion** --- Sources can follow circular or elliptical paths around a configurable center point. Speed, direction, and radii are adjustable per source. Animation runs at 60 fps with live audio updates.

**Multi-format input** --- WAV, MP3, FLAC, OGG, AAC, WMA, M4A, AIFF, and OPUS.

**Per-source controls** --- Independent play/pause, mute, solo, and volume. Timeline scrubbers with waveform previews.

**Project persistence** --- Save and load scenes as `.azimuth` JSON files. Export the spatial mix to stereo WAV or MP3.

**Undo / redo** --- Full command-pattern history for moves, adds, removes, and volume changes (100 levels).

**Keyboard-driven workflow** --- Ctrl+Z/Y undo/redo, Space play/stop, Delete to remove, G for grid snap, O to toggle orbit, and the usual Ctrl+S/O/N.

**Settings and recent files** --- Remembers window size, last opened scenes, grid preferences, and audio parameters across sessions.

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+S` | Save |
| `Ctrl+Shift+S` | Save As |
| `Ctrl+O` | Open |
| `Ctrl+N` | New Scene |
| `Ctrl+Z` | Undo |
| `Ctrl+Y` | Redo |
| `Space` | Play / Stop |
| `Delete` | Remove selected source |
| `Escape` | Deselect |
| `G` | Toggle snap-to-grid |
| `O` | Toggle orbit on selected source |

## Getting Started

Requirements: Windows 10 or later (x64), [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
git clone https://github.com/mwdlyt/Azimuth.git
cd Azimuth
dotnet run --project Azimuth
```

To publish a self-contained single executable:

```
dotnet publish Azimuth -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The output lands in `Azimuth/bin/Release/net8.0-windows/win-x64/publish/`.

## Scene Format

Scenes are plain JSON with an `.azimuth` extension:

```json
{
  "version": 1,
  "name": "My Scene",
  "canvasRadius": 400,
  "sources": [
    {
      "name": "rain",
      "filePath": "C:/audio/rain.wav",
      "x": 120,
      "y": -80,
      "baseVolume": 0.8,
      "orbitEnabled": true,
      "orbitRadiusX": 150,
      "orbitSpeed": 45,
      "orbitClockwise": true
    }
  ]
}
```

## Architecture

```
Azimuth/
  Models/          Data models -- AudioSource, AzimuthScene, AppConfig
  ViewModels/      MVVM -- MainViewModel, AudioSourceViewModel, RelayCommand
  Services/        Audio engine, spatial math, serialization, user settings
  Commands/        Undo/redo implementations -- move, add, remove, volume
  Controls/        Custom WPF controls -- SpatialCanvas, SourceNode, OrbitPanel, TimelinePanel
  Views/           Settings window
  Converters/      WPF value converters
```

| Dependency | Purpose |
|---|---|
| [WPF-UI](https://github.com/lepoco/wpfui) | Fluent Design controls |
| [NAudio](https://github.com/naudio/NAudio) | Audio playback and processing |
| [NAudio.Lame](https://github.com/Corey-M/NAudio.Lame) | MP3 export |
| [NAudio.Vorbis](https://github.com/naudio/Vorbis) | OGG/Vorbis decoding |

## Roadmap

- Automation keyframes --- animate positions along a timeline
- 3D elevation with HRTF
- MIDI controller mapping
- Plugin system for per-source effects

## License

[MIT](LICENSE)

