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
            // WaveFileWriter を using で囲むと Stream も閉じてしまう可能性があるため、
            // Stream を閉じたくない場合は外部で管理する必要があります。
            // ここでは API サーバーでの利用を考慮し、内部で Flush だけを行い、Stream は閉じないようにします。
            var writer = new WaveFileWriter(stream, format);
            writer.WriteSamples(samples, 0, samples.Length);
            writer.Flush();
            // Dispose しないことで Stream を開いたままにする（呼び出し側で ms.ToArray() できるようにする）
        }
    }
}
