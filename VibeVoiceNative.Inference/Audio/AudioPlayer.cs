using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VibeVoiceNative.Inference.Audio
{
    public class AudioPlayer : IDisposable
    {
        private IWavePlayer _outputDevice;
        private BufferedWaveProvider _bufferedWaveProvider;
        private VolumeSampleProvider _volumeProvider;
        private readonly WaveFormat _waveFormat;

        public AudioPlayer(int sampleRate = 24000, int channels = 1)
        {
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            InitializePlayer();
        }

        private void InitializePlayer()
        {
            _outputDevice = new WaveOutEvent(); // または WasapiOut
            _bufferedWaveProvider = new BufferedWaveProvider(_waveFormat)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromMinutes(5)
            };

            _volumeProvider = new VolumeSampleProvider(_bufferedWaveProvider.ToSampleProvider());
            _outputDevice.Init(_volumeProvider);
        }

        public void Play()
        {
            if (_outputDevice.PlaybackState != PlaybackState.Playing)
            {
                _outputDevice.Play();
            }
        }

        public void Stop()
        {
            _outputDevice.Stop();
            _bufferedWaveProvider.ClearBuffer();
        }

        public void AddSamples(float[] samples)
        {
            // float[] を byte[] に変換してバッファに追加
            byte[] byteArray = new byte[samples.Length * 4];
            Buffer.BlockCopy(samples, 0, byteArray, 0, byteArray.Length);
            _bufferedWaveProvider.AddSamples(byteArray, 0, byteArray.Length);
        }

        public float Volume
        {
            get => _volumeProvider.Volume;
            set => _volumeProvider.Volume = value;
        }

        public PlaybackState State => _outputDevice.PlaybackState;

        public void Dispose()
        {
            _outputDevice?.Dispose();
        }
    }
}
