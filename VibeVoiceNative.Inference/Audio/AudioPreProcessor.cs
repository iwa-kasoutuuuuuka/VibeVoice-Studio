using System;
using System.Linq;
using NAudio.Wave;

namespace VibeVoiceNative.Inference.Audio
{
    public static class AudioPreProcessor
    {
        public static float[] CleanAndNormalize(float[] samples, float targetPeak = 0.9f)
        {
            if (samples == null || samples.Length == 0) return Array.Empty<float>();

            // 1. Trimming Silence (Simple threshold)
            float threshold = 0.01f;
            int start = 0;
            while (start < samples.Length && Math.Abs(samples[start]) < threshold) start++;
            
            int end = samples.Length - 1;
            while (end > start && Math.Abs(samples[end]) < threshold) end--;

            if (start >= end) return samples;
            
            var trimmed = samples.Skip(start).Take(end - start + 1).ToArray();

            // 2. Peak Normalization
            float max = trimmed.Max(Math.Abs);
            if (max > 0)
            {
                float factor = targetPeak / max;
                for (int i = 0; i < trimmed.Length; i++) trimmed[i] *= factor;
            }

            return trimmed;
        }
    }
}
