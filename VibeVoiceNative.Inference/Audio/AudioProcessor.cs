using System;
using System.IO;
using NAudio.Wave;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;

namespace VibeVoiceNative.Inference.Audio
{
    public class AudioProcessor
    {
        private const int SampleRate = 24000;
        private const int N_Fft = 1024;
        private const int HopLength = 256;
        private const int NMels = 80;

        public float[,] ProcessReferenceAudio(string wavPath)
        {
            float[] waveform = ReadWav(wavPath);
            return ComputeMelSpectrogram(waveform);
        }

        private float[] ReadWav(string path)
        {
            using var reader = new AudioFileReader(path);
            
            // 16kHz モノラルに変換
            var outFormat = new WaveFormat(SampleRate, 1);
            using var resampler = new MediaFoundationResampler(reader, outFormat);
            resampler.ResamplerQuality = 60;

            var provider = resampler.ToSampleProvider();
            
            float[] buffer = new float[16000 * 30]; // max 30 sec for ref
            int read = provider.Read(buffer, 0, buffer.Length);
            
            float[] result = new float[read];
            Array.Copy(buffer, result, read);
            
            return result;
        }

        private float[,] ComputeMelSpectrogram(float[] waveform)
        {
            int numFrames = 1 + (waveform.Length - N_Fft) / HopLength;
            if (numFrames <= 0) return new float[NMels, 1]; // Fallback

            float[,] stft = new float[numFrames, N_Fft / 2 + 1];
            float[] window = GenerateHannWindow(N_Fft);

            for (int i = 0; i < numFrames; i++)
            {
                int start = i * HopLength;
                Complex[] frame = new Complex[N_Fft];
                for (int j = 0; j < N_Fft; j++)
                {
                    if (start + j < waveform.Length)
                        frame[j] = new Complex(waveform[start + j] * window[j], 0);
                }

                Fourier.Forward(frame, FourierOptions.Matlab);

                for (int j = 0; j < N_Fft / 2 + 1; j++)
                {
                    stft[i, j] = (float)(frame[j].Magnitude * frame[j].Magnitude);
                }
            }

            return ApplyMelFilterbank(stft, numFrames);
        }

        private float[] GenerateHannWindow(int size)
        {
            float[] window = new float[size];
            for (int i = 0; i < size; i++)
            {
                window[i] = (float)(0.5 * (1 - Math.Cos(2 * Math.PI * i / (size - 1))));
            }
            return window;
        }

        private float[,] ApplyMelFilterbank(float[,] stft, int numFrames)
        {
            float[,] melSpec = new float[NMels, numFrames];
            float maxFreq = SampleRate / 2.0f;
            
            // メル尺度への変換ヘルパー
            float ToMel(float freq) => 2595.0f * (float)Math.Log10(1.0f + freq / 700.0f);
            float ToFreq(float mel) => 700.0f * ((float)Math.Pow(10, mel / 2595.0f) - 1.0f);

            float minMel = ToMel(0);
            float maxMel = ToMel(maxFreq);

            // 各フィルタの中心周波数を計算
            float[] melPoints = new float[NMels + 2];
            for (int i = 0; i < NMels + 2; i++)
            {
                melPoints[i] = ToFreq(minMel + (maxMel - minMel) * i / (NMels + 1));
            }

            int[] binPoints = melPoints.Select(f => (int)Math.Floor(f / maxFreq * (N_Fft / 2))).ToArray();

            for (int m = 0; m < NMels; m++)
            {
                int startBin = binPoints[m];
                int centerBin = binPoints[m + 1];
                int endBin = binPoints[m + 2];

                for (int t = 0; t < numFrames; t++)
                {
                    float sum = 0;
                    // 左側の斜面
                    for (int k = startBin; k < centerBin; k++)
                    {
                        float weight = (float)(k - startBin) / (centerBin - startBin);
                        sum += weight * stft[t, k];
                    }
                    // 右側の斜面
                    for (int k = centerBin; k < endBin; k++)
                    {
                        float weight = (float)(endBin - k) / (endBin - centerBin);
                        sum += weight * stft[t, k];
                    }
                    melSpec[m, t] = (float)Math.Log(sum + 1e-5);
                }
            }
            return melSpec;
        }
        /// <summary>
        /// スピードとピッチの調整を適用します。
        /// 簡易的な実装としてリサンプリング（線形補間）を使用します。
        /// </summary>
        public static float[] ApplyEffects(float[] data, double speed, double pitch)
        {
            if (speed == 1.0 && pitch == 0.0) return data;

            // ピッチ（セント）を周波数倍率に変換: 2^(cents/1200)
            double pitchFactor = Math.Pow(2, pitch / 1200.0);
            
            // 総合的な再生速度の倍率 (speed * pitchFactor)
            // ピッチを上げると速くなる（テープの回転を速くした状態）
            double totalRate = speed * pitchFactor;
            
            if (totalRate == 1.0) return data;

            int newLength = (int)(data.Length / totalRate);
            if (newLength <= 0) return Array.Empty<float>();

            float[] result = new float[newLength];
            for (int i = 0; i < newLength; i++)
            {
                double sourceIndex = i * totalRate;
                int index1 = (int)Math.Floor(sourceIndex);
                int index2 = Math.Min(index1 + 1, data.Length - 1);
                float fraction = (float)(sourceIndex - index1);

                // 線形補間
                result[i] = data[index1] * (1 - fraction) + data[index2] * fraction;
            }

            return result;
        }
    }
}
