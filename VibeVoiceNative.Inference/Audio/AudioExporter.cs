using System.IO;
using NAudio.Wave;

namespace VibeVoiceNative.Inference.Audio
{
    public static class AudioExporter
    {
        public static void SaveAsWav(string filePath, float[] samples, int sampleRate = 24000, int channels = 1)
        {
            using var fs = File.Create(filePath);
            SaveAsWav(fs, samples, sampleRate, channels);
        }

        public static void SaveAsWav(Stream stream, float[] samples, int sampleRate = 24000, int channels = 1)
        {
            var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            using (var writer = new WaveFileWriter(stream, format))
            {
                writer.WriteSamples(samples, 0, samples.Length);
                writer.Flush();
            }
            // using ブロックを抜ける際にヘッダーが更新されます。
        }
    }
}
