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
    public AudioFileReader Reader { get; }
    public PanningSampleProvider Panner { get; }
    public VolumeSampleProvider Volume { get; }

    public ActiveSource(Guid id, AudioFileReader reader, PanningSampleProvider panner, VolumeSampleProvider volume)
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

            var reader = new AudioFileReader(source.FilePath);

            // Ensure stereo
            ISampleProvider sampleProvider = reader.WaveFormat.Channels == 1
                ? new MonoToStereoSampleProvider(reader) { LeftVolume = 1f, RightVolume = 1f }
                : (ISampleProvider)reader;

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
            active.Volume.Volume = isMuted ? 0f : combinedVolume;
        }
    }

    /// <summary>
    /// Starts audio output playback.
    /// </summary>
    public void Play()
    {
        lock (_lock)
        {
            if (_isPlaying) return;

            _output?.Dispose();
            _output = new WasapiOut();
            _output.Init(_mixer);
            _output.Play();
            _isPlaying = true;
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

            // Reset all reader positions
            foreach (var active in _sources.Values)
            {
                active.Reader.Position = 0;
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
    private readonly AudioFileReader _reader;
    private readonly PanningSampleProvider _panner;
    private readonly VolumeSampleProvider _volume;

    public LoopingSampleProvider(AudioFileReader reader, PanningSampleProvider panner, VolumeSampleProvider volume)
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
