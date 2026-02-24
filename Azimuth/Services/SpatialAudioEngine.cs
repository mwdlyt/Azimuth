using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Azimuth.Models;

namespace Azimuth.Services;

/// <summary>
/// Tracks playback state for a single audio source.
/// </summary>
internal sealed class ActiveSource : IDisposable
{
    public Guid Id { get; }
    public WaveStream Reader { get; }
    public PanningSampleProvider Panner { get; }
    public VolumeSampleProvider Volume { get; }
    public bool IsPlaying { get; set; } = true;
    public float LastComputedVolume { get; set; }

    public ActiveSource(Guid id, WaveStream reader, PanningSampleProvider panner, VolumeSampleProvider volume)
    {
        Id = id;
        Reader = reader;
        Panner = panner;
        Volume = volume;
    }

    public void Dispose()
    {
        Reader.Dispose();
    }
}

/// <summary>
/// Real-time spatial audio engine. Manages playback of multiple positioned audio sources.
/// </summary>
public sealed class SpatialAudioEngine : IDisposable
{
    private readonly Dictionary<Guid, ActiveSource> _sources = new();
    private readonly MixingSampleProvider _mixer;
    private WasapiOut? _output;
    private bool _isPlaying;
    private readonly object _lock = new();

    public bool IsPlaying => _isPlaying;

    public SpatialAudioEngine()
    {
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(AppConfig.SampleRate, AppConfig.Channels))
        {
            ReadFully = true
        };
    }

    /// <summary>
    /// Adds an audio source and starts playing it at the given spatial position.
    /// </summary>
    public void AddSource(AudioSource source, double canvasRadius)
    {
        lock (_lock)
        {
            if (_sources.ContainsKey(source.Id)) return;

            var reader = AudioReaderFactory.CreateReader(source.FilePath);

            // Convert WaveStream -> ISampleProvider (handles all bit depths via NAudio extension)
            ISampleProvider raw = reader.ToSampleProvider();

            // PanningSampleProvider requires mono input -- mix down stereo sources before panning
            ISampleProvider sampleProvider = raw.WaveFormat.Channels switch
            {
                1 => raw,                                          // already mono
                _ => new StereoToMonoSampleProvider(raw)           // mix L+R -> mono
            };

            // Resample if needed
            if (sampleProvider.WaveFormat.SampleRate != AppConfig.SampleRate)
            {
                var resampler = new WdlResamplingSampleProvider(sampleProvider, AppConfig.SampleRate);
                sampleProvider = resampler;
            }

            var panner = new PanningSampleProvider(sampleProvider);
            var volume = new VolumeSampleProvider(panner);

            // Enable looping
            var looper = new LoopingSampleProvider(reader, panner, volume);

            var (leftGain, rightGain) = SpatialMath.CalculateGains(source.X, source.Y, canvasRadius);
            float combinedVolume = (leftGain + rightGain) / 2f * source.BaseVolume;
            float pan = SpatialMath.PanValue(source.X, canvasRadius);

            panner.Pan = pan;
            volume.Volume = source.IsMuted ? 0f : combinedVolume;

            var active = new ActiveSource(source.Id, reader, panner, volume);
            active.LastComputedVolume = combinedVolume;
            _sources[source.Id] = active;
            _mixer.AddMixerInput(looper);
        }
    }

    /// <summary>
    /// Removes an audio source from playback.
    /// </summary>
    public void RemoveSource(Guid sourceId)
    {
        lock (_lock)
        {
            if (_sources.Remove(sourceId, out var active))
            {
                active.Dispose();
            }
        }
    }

    /// <summary>
    /// Updates the spatial position of an existing source in real-time.
    /// </summary>
    public void UpdateSourcePosition(Guid sourceId, double x, double y, double canvasRadius, float baseVolume, bool isMuted)
    {
        lock (_lock)
        {
            if (!_sources.TryGetValue(sourceId, out var active)) return;

            var (leftGain, rightGain) = SpatialMath.CalculateGains(x, y, canvasRadius);
            float combinedVolume = (leftGain + rightGain) / 2f * baseVolume;
            float pan = SpatialMath.PanValue(x, canvasRadius);

            active.Panner.Pan = pan;
            active.LastComputedVolume = combinedVolume;
            active.Volume.Volume = (isMuted || !active.IsPlaying) ? 0f : combinedVolume;
        }
    }

    /// <summary>
    /// Starts playback for a single source. Starts the engine if not already playing.
    /// </summary>
    public void PlaySource(Guid sourceId)
    {
        lock (_lock)
        {
            if (!_sources.TryGetValue(sourceId, out var active)) return;
            active.IsPlaying = true;
            active.Volume.Volume = active.LastComputedVolume;

            EnsurePlaying();
        }
    }

    /// <summary>
    /// Pauses a single source (preserves position, sets volume to 0).
    /// </summary>
    public void PauseSource(Guid sourceId)
    {
        lock (_lock)
        {
            if (!_sources.TryGetValue(sourceId, out var active)) return;
            active.IsPlaying = false;
            active.Volume.Volume = 0f;
        }
    }

    /// <summary>
    /// Returns whether a specific source is currently playing.
    /// </summary>
    public bool IsSourcePlaying(Guid sourceId)
    {
        lock (_lock)
        {
            return _sources.TryGetValue(sourceId, out var active) && active.IsPlaying;
        }
    }

    /// <summary>
    /// Gets the current playback position of a source.
    /// </summary>
    public TimeSpan GetSourcePosition(Guid sourceId)
    {
        lock (_lock)
        {
            if (!_sources.TryGetValue(sourceId, out var active)) return TimeSpan.Zero;
            return active.Reader.CurrentTime;
        }
    }

    /// <summary>
    /// Gets the total duration of a source's audio file.
    /// </summary>
    public TimeSpan GetSourceDuration(Guid sourceId)
    {
        lock (_lock)
        {
            if (!_sources.TryGetValue(sourceId, out var active)) return TimeSpan.Zero;
            return active.Reader.TotalTime;
        }
    }

    /// <summary>
    /// Seeks a source to the specified position.
    /// </summary>
    public void SeekSource(Guid sourceId, TimeSpan position)
    {
        lock (_lock)
        {
            if (!_sources.TryGetValue(sourceId, out var active)) return;
            var clamped = position < TimeSpan.Zero ? TimeSpan.Zero
                : position > active.Reader.TotalTime ? active.Reader.TotalTime
                : position;
            active.Reader.CurrentTime = clamped;
        }
    }

    /// <summary>
    /// Ensures the WasapiOut output device is running.
    /// </summary>
    private void EnsurePlaying()
    {
        if (_isPlaying) return;

        _output?.Dispose();
        _output = new WasapiOut();
        _output.Init(_mixer);
        _output.Play();
        _isPlaying = true;
    }

    /// <summary>
    /// Starts audio output playback.
    /// </summary>
    public void Play()
    {
        lock (_lock)
        {
            // Mark all sources as playing
            foreach (var active in _sources.Values)
            {
                active.IsPlaying = true;
                active.Volume.Volume = active.LastComputedVolume;
            }

            EnsurePlaying();
        }
    }

    /// <summary>
    /// Stops audio output playback.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (!_isPlaying) return;

            _output?.Stop();
            _output?.Dispose();
            _output = null;
            _isPlaying = false;

            // Reset all reader positions and mark as not playing
            foreach (var active in _sources.Values)
            {
                active.Reader.Position = 0;
                active.IsPlaying = false;
            }
        }
    }

    /// <summary>
    /// Stops playback and removes all sources.
    /// </summary>
    public void StopAll()
    {
        lock (_lock)
        {
            Stop();
            foreach (var active in _sources.Values)
            {
                active.Dispose();
            }
            _sources.Clear();
            _mixer.RemoveAllMixerInputs();
        }
    }

    /// <summary>
    /// Gets the mixer provider for rendering purposes.
    /// </summary>
    internal MixingSampleProvider Mixer => _mixer;

    public void Dispose()
    {
        StopAll();
        _output?.Dispose();
    }
}

/// <summary>
/// Wraps a sample provider to loop the underlying AudioFileReader when it reaches the end.
/// </summary>
internal sealed class LoopingSampleProvider : ISampleProvider
{
    private readonly WaveStream _reader;
    private readonly PanningSampleProvider _panner;
    private readonly VolumeSampleProvider _volume;

    public LoopingSampleProvider(WaveStream reader, PanningSampleProvider panner, VolumeSampleProvider volume)
    {
        _reader = reader;
        _panner = panner;
        _volume = volume;
    }

    public WaveFormat WaveFormat => _volume.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = _volume.Read(buffer, offset + totalRead, count - totalRead);
            if (read == 0)
            {
                // Loop: reset to beginning
                _reader.Position = 0;
                read = _volume.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0) break; // Prevent infinite loop on empty file
            }
            totalRead += read;
        }
        return totalRead;
    }
}

/// <summary>
/// Mixes a stereo sample provider down to mono by averaging left and right channels.
/// Required because PanningSampleProvider only accepts mono input.
/// </summary>
internal sealed class StereoToMonoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private float[] _sourceBuffer = Array.Empty<float>();

    public WaveFormat WaveFormat { get; }

    public StereoToMonoSampleProvider(ISampleProvider source)
    {
        if (source.WaveFormat.Channels != 2)
            throw new ArgumentException("Source must be stereo (2 channels).", nameof(source));

        _source = source;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int stereoCount = count * 2;
        if (_sourceBuffer.Length < stereoCount)
            _sourceBuffer = new float[stereoCount];

        int read = _source.Read(_sourceBuffer, 0, stereoCount);
        int monoSamples = read / 2;

        for (int i = 0; i < monoSamples; i++)
            buffer[offset + i] = (_sourceBuffer[i * 2] + _sourceBuffer[i * 2 + 1]) * 0.5f;

        return monoSamples;
    }
}
