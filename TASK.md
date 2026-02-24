# Azimuth Build Task

You are building 'Azimuth' — an open-source 3D spatial audio placement tool for Windows. Consumer-friendly desktop app. Looks beautiful, intuitive, ships as a single .exe.

## Tech Stack
- Language: C# (.NET 8)
- UI: WPF + Wpf.Ui (NuGet: WPF-UI) for modern Fluent Windows 11 design
- Audio playback: NAudio (NuGet: NAudio)
- Spatial math: Manual implementation (see below)
- File I/O: System.Text.Json for project save/load
- Export: NAudio for WAV render, NAudio.Lame for MP3

## What the app does
The user has a 2D circular stage/canvas. The listener (user) is always at the center. They can:
1. Drag audio files (.wav, .mp3) onto the canvas
2. Each file becomes a draggable 'source node' — a circle with the filename
3. As they drag a source, audio plays in real-time with spatial positioning
4. Position determines: stereo panning (left/right) + volume (distance from center)
5. Multiple sources can play simultaneously
6. Save/load the scene as a JSON project file (.azimuth extension)
7. Export/render the mix to stereo WAV or MP3

## Project Structure

```
Azimuth/
  Azimuth.sln
  Azimuth/
    Azimuth.csproj          (.NET 8, WPF)
    App.xaml / App.xaml.cs
    MainWindow.xaml / .cs
    Models/
      AudioSource.cs        (file path, position X/Y, volume, name, color)
      AzimuthScene.cs       (list of sources, canvas size, metadata)
    Services/
      SpatialAudioEngine.cs (real-time playback, manages all active sources)
      SpatialMath.cs        (position to pan/volume calculations)
      SceneSerializer.cs    (JSON save/load)
      AudioExporter.cs      (render to WAV/MP3)
    ViewModels/
      MainViewModel.cs      (MVVM, INotifyPropertyChanged, no frameworks)
      AudioSourceViewModel.cs
    Controls/
      SpatialCanvas.cs      (custom WPF Canvas, handles drag-drop of audio files)
      SourceNode.xaml/.cs   (the draggable audio source node visual)
    Converters/
      (value converters for bindings)
    Assets/
      (icons, etc.)
```

## Spatial Math (SpatialMath.cs)

Canvas center = listener position (0,0). Source at position (x, y) relative to center:

```
float distance = Math.Min(Math.Sqrt(x*x + y*y) / maxRadius, 1.0f);
float volume = Math.Max(1.0f / (1.0f + distance * distance * 8f), 0.05f);
float pan = Math.Clamp((float)(x / maxRadius), -1f, 1f);
float leftGain = volume * (pan <= 0 ? 1f : 1f - pan);
float rightGain = volume * (pan >= 0 ? 1f : 1f + pan);
```

## SpatialAudioEngine.cs

- Dictionary<Guid, ActiveSource> for playing sources
- Each ActiveSource: AudioFileReader + volume/pan control
- MixingSampleProvider to mix all sources
- WasapiOut for output
- Methods: AddSource, RemoveSource, UpdateSourcePosition, StopAll, RenderToFile

## UI Design — IMPORTANT, make it look great

Dark theme throughout:
- Background: #0D0D0F
- Surface: #161622
- Canvas background: #0A0A12
- Accent: #7C5CFC (violet)
- Text primary: #E8E8F0
- Text muted: #6B6B80
- Source node colors: rotate through violet, coral, teal, amber, green

The Canvas (center, ~70% of window):
- Large circle
- Subtle ring guides at 25/50/75/100% distance
- Listener icon at center (headphone or person icon, white)
- Front/Back/Left/Right labels at edges
- Source nodes: colored circles with filename, subtle glow
- Subtle line from center to each source

Left sidebar:
- App title 'Azimuth' with logo
- Source list (color dot, name, distance%, pan value)
- Mute/solo buttons per source
- Volume slider per source
- Remove button

Top toolbar (Wpf.Ui style):
- New Scene | Open | Save | Export (WAV / MP3 dropdown)
- Play All / Stop All
- Settings

## AudioSource Model

```csharp
public class AudioSource {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FilePath { get; set; }
    public string Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public float BaseVolume { get; set; } = 1.0f;
    public bool IsMuted { get; set; }
    public string Color { get; set; }
}
```

## Scene File Format (.azimuth — JSON)

```json
{
  "version": 1,
  "name": "My Scene",
  "canvasRadius": 400,
  "sources": [
    {
      "id": "guid-here",
      "name": "rain",
      "filePath": "C:/audio/rain.wav",
      "x": 120,
      "y": -80,
      "baseVolume": 0.8,
      "isMuted": false,
      "color": "#7C5CFC"
    }
  ]
}
```

## Code Quality Standards

- MVVM properly — no business logic in code-behind
- Async/await for file operations
- Proper disposal of NAudio resources (IDisposable)
- Exception handling with user-friendly error dialogs (Wpf.Ui ContentDialog)
- XML doc comments on public APIs
- No magic numbers — constants in a static Config class
- README.md: what it is, build instructions, screenshot placeholder, MIT license

## Git Steps (PowerShell — run as SEPARATE commands, no &&)

After all code is written:
```
git init
git remote add origin https://github.com/mwdlyt/Azimuth.git
git add -A
git commit -m "feat: initial Azimuth build - WPF spatial audio placement tool"
git branch -M main
git push -u origin main
```

Then run: openclaw system event --text "Done: Azimuth spatial audio app built and pushed to GitHub" --mode now
