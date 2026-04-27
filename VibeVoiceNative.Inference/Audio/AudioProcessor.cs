using System;
using System.IO;
using NAudio.Wave;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;

namespace VibeVoiceNative.Inference.Audio
{
    public class AudioProcessor
    {
        private const int SampleRate = 16000;
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
            // Dummy Mel Filterbank application for structural completeness.
            // In a full implementation, this computes the dot product of STFT power spec and Mel basis matrix.
            float[,] melSpec = new float[NMels, numFrames];
            for (int m = 0; m < NMels; m++)
            {
                for (int t = 0; t < numFrames; t++)
                {
                    melSpec[m, t] = (float)Math.Log(stft[t, m % (N_Fft / 2)] + 1e-5);
                }
            }
            return melSpec;
        }
    }
}
