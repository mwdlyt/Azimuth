# Azimuth Feature Task: Per-Source Play + Timeline

Read the existing codebase thoroughly before making changes. Pay close attention to:
- SpatialAudioEngine.cs — audio engine (uses NAudio, WasapiOut, MixingSampleProvider)
- MainViewModel.cs — MVVM, commands, source management
- AudioSourceViewModel.cs — per-source view model
- SpatialCanvas.cs — the main canvas control
- MainWindow.xaml / .cs — the main window layout
- AppConfig.cs — constants

## Feature 1: Per-Source Play/Pause Toggle

### Goal
Each source in the left sidebar should have its own Play/Pause button, letting the user audition individual sources independently.

### Implementation

**SpatialAudioEngine.cs — add:**
- `PlaySource(Guid sourceId)` — start playback for a single source only
- `PauseSource(Guid sourceId)` — pause a single source (preserve position)
- `IsSourcePlaying(Guid sourceId)` — bool
- Track per-source play state in `ActiveSource` with a `bool IsPlaying` property
- When playing a single source, use a separate `WasapiOut` per source OR route through the main mixer but mute all others temporarily — the cleaner approach is per-source WasapiOut instances so they're fully independent
- Actually, simplest approach: add a `IsSoloed` state per ActiveSource. `PlaySource` sets IsSoloed=true on that source and sets volume to 0 on all others. `StopSolo` restores all volumes.

**AudioSourceViewModel.cs — add:**
- `IsPlaying` property (bool, notifies)
- `PlayPauseCommand` — toggles per-source playback
- `PlayPauseIcon` — returns play or pause icon string based on state

**Left sidebar in MainWindow.xaml:**
- Add a play/pause icon button to each source row
- Use WPF-UI SymbolIcon: `Play` / `Pause`
- Place it next to the mute button

## Feature 2: Per-Source Timeline / Scrubber

### Goal
At the bottom of the app (or as an expandable panel), show a horizontal timeline for each source. The user can:
- See the duration of each source audio file
- See a playhead showing current position
- Drag the playhead to seek to a different position
- See the waveform (simplified — a series of amplitude bars, not a full waveform render)

### Implementation

**New file: Controls/TimelinePanel.xaml + .cs**
- ItemsControl bound to `MainViewModel.Sources`
- Each row: source color indicator | source name | scrubber track
- Scrubber track: a custom Canvas showing:
  - Background: dark (#161622)
  - Amplitude bars: sample the audio file at N points (e.g., 200 points across the duration), draw vertical bars proportional to amplitude. Use a background Thread/Task to compute this on load. Store as `double[] WaveformSamples` on AudioSourceViewModel.
  - Playhead: a thin vertical line at the current position (updates every 100ms via a DispatcherTimer)
  - Click/drag on the track: seek to that position

**SpatialAudioEngine.cs — add:**
- `GetSourcePosition(Guid sourceId)` — returns `TimeSpan` current position
- `GetSourceDuration(Guid sourceId)` — returns `TimeSpan` total duration  
- `SeekSource(Guid sourceId, TimeSpan position)` — seek to position (set Reader.Position)

**AudioSourceViewModel.cs — add:**
- `Duration` property (TimeSpan, set on load)
- `CurrentPosition` property (TimeSpan, updated by timer)
- `PositionFraction` property (double 0.0–1.0, for binding scrubber position)
- `WaveformSamples` property (double[], 200 amplitude values 0.0–1.0)
- `LoadWaveformAsync(string filePath)` — samples audio file to generate waveform data
- `SeekToFraction(double fraction)` — calls engine to seek

**MainViewModel.cs — add:**
- `DispatcherTimer` at 100ms to update all source `CurrentPosition` values while playing
- `IsTimelinePanelVisible` bool property for show/hide toggle

**MainWindow.xaml:**
- Add a timeline toggle button in the toolbar ("Timeline" with a waveform icon)
- Add a Grid row at the bottom for the TimelinePanel
- TimelinePanel height: 160px, collapsible via `Visibility` binding to `IsTimelinePanelVisible`
- Splitter between canvas area and timeline panel (GridSplitter)

### Waveform sampling algorithm (in AudioSourceViewModel)
```
// Sample audio file to get N amplitude points
var samples = new double[200];
using var reader = AudioReaderFactory.CreateReader(filePath);
var raw = reader.ToSampleProvider();
if (raw.WaveFormat.Channels == 2) raw = new StereoToMonoSampleProvider(raw);

long totalSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8 / reader.WaveFormat.Channels);
int samplesPerBucket = (int)(totalSamples / 200);
var buffer = new float[samplesPerBucket];

for (int i = 0; i < 200; i++)
{
    int read = raw.Read(buffer, 0, samplesPerBucket);
    if (read == 0) break;
    float max = 0;
    for (int j = 0; j < read; j++) max = Math.Max(max, Math.Abs(buffer[j]));
    samples[i] = max;
}
return samples;
```

## Code Quality
- Zero build errors or warnings
- MVVM — no logic in code-behind
- Async waveform loading so UI doesn't freeze
- Proper disposal of any timers or resources
- Matches the dark design (bg #0D0D0F, surface #161622, accent #7C5CFC)

## When completely finished:
Run as SEPARATE PowerShell commands:
```
git add -A
git commit -m "feat: per-source play toggle and timeline scrubber with waveform"
git push
```

Then: openclaw system event --text "Done: Azimuth per-source play and timeline features complete" --mode now
