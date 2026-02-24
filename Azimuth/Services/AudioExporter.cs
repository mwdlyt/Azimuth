using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Lame;
using Azimuth.Models;

namespace Azimuth.Services;

/// <summary>
/// Renders an Azimuth scene to a stereo WAV or MP3 file.
/// </summary>
public static class AudioExporter
{
    /// <summary>
    /// Renders all sources in the scene to a WAV file. Each source plays once (no looping).
    /// </summary>
    public static async Task ExportWavAsync(AzimuthScene scene, string outputPath, double canvasRadius, IProgress<double>? progress = null)
    {
        await Task.Run(() =>
        {
            var mixer = CreateMixerForScene(scene, canvasRadius, out var readers, out long maxLength);
            if (readers.Count == 0) return;

            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(AppConfig.SampleRate, AppConfig.Channels);
            using var writer = new WaveFileWriter(outputPath, waveFormat);

            RenderMixer(mixer, writer, maxLength, progress);

            foreach (var r in readers) r.Dispose();
        });
    }

    /// <summary>
    /// Renders all sources in the scene to an MP3 file.
    /// </summary>
    public static async Task ExportMp3Async(AzimuthScene scene, string outputPath, double canvasRadius, IProgress<double>? progress = null)
    {
        await Task.Run(() =>
        {
            var mixer = CreateMixerForScene(scene, canvasRadius, out var readers, out long maxLength);
            if (readers.Count == 0) return;

            // Render to a temp WAV first, then convert
            var tempWav = Path.GetTempFileName() + ".wav";
            try
            {
                var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(AppConfig.SampleRate, AppConfig.Channels);
                using (var writer = new WaveFileWriter(tempWav, waveFormat))
                {
                    RenderMixer(mixer, writer, maxLength, progress);
                }

                foreach (var r in readers) r.Dispose();

                // Convert to MP3
                using var wavReader = new AudioFileReader(tempWav);
                using var mp3Writer = new LameMP3FileWriter(outputPath, wavReader.WaveFormat, LAMEPreset.STANDARD);
                wavReader.CopyTo(mp3Writer);
            }
            finally
            {
                if (File.Exists(tempWav)) File.Delete(tempWav);
            }
        });
    }

    private static MixingSampleProvider CreateMixerForScene(
        AzimuthScene scene, double canvasRadius,
        out List<AudioFileReader> readers, out long maxLength)
    {
        readers = new List<AudioFileReader>();
        maxLength = 0;

        var mixer = new MixingSampleProvider(
            WaveFormat.CreateIeeeFloatWaveFormat(AppConfig.SampleRate, AppConfig.Channels))
        {
            ReadFully = false
        };

        foreach (var source in scene.Sources)
        {
            if (source.IsMuted || !File.Exists(source.FilePath)) continue;

            var reader = new AudioFileReader(source.FilePath);
            readers.Add(reader);

            if (reader.Length > maxLength) maxLength = reader.Length;

            ISampleProvider sp = reader.WaveFormat.Channels == 1
                ? new MonoToStereoSampleProvider(reader)
                : (ISampleProvider)reader;

            if (sp.WaveFormat.SampleRate != AppConfig.SampleRate)
                sp = new WdlResamplingSampleProvider(sp, AppConfig.SampleRate);

            var panner = new PanningSampleProvider(sp);
            var volume = new VolumeSampleProvider(panner);

            var (leftGain, rightGain) = SpatialMath.CalculateGains(source.X, source.Y, canvasRadius);
            float combinedVolume = (leftGain + rightGain) / 2f * source.BaseVolume;
            float pan = SpatialMath.PanValue(source.X, canvasRadius);

            panner.Pan = pan;
            volume.Volume = combinedVolume;

            mixer.AddMixerInput(volume);
        }

        return mixer;
    }

    private static void RenderMixer(MixingSampleProvider mixer, WaveFileWriter writer, long maxLength, IProgress<double>? progress)
    {
        var buffer = new float[AppConfig.SampleRate * AppConfig.Channels]; // 1 second buffer
        long totalSamplesWritten = 0;
        long estimatedTotalSamples = maxLength / 4; // float = 4 bytes

        while (true)
        {
            int read = mixer.Read(buffer, 0, buffer.Length);
            if (read == 0) break;

            writer.WriteSamples(buffer, 0, read);
            totalSamplesWritten += read;

            if (estimatedTotalSamples > 0)
                progress?.Report((double)totalSamplesWritten / estimatedTotalSamples);
        }

        progress?.Report(1.0);
    }
}
