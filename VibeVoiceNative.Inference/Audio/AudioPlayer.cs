using System;
using System.Collections.Generic;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.CoreAudioApi;

namespace VibeVoiceNative.Inference.Audio
{
    public class AudioPlayer : IDisposable
    {
        private IWavePlayer _outputDevice = null!;
        private BufferedWaveProvider _bufferedWaveProvider = null!;
        private VolumeSampleProvider _volumeProvider = null!;
        private readonly WaveFormat _waveFormat;

        public string? SelectedDeviceId { get; set; }

        public AudioPlayer(int sampleRate = 24000, int channels = 1)
        {
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            InitializePlayer();
        }

        public static (string id, string name)[] GetDevices()
        {
            var devices = new List<(string, string)>();
            using var enumerator = new MMDeviceEnumerator();
            var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var endpoint in endpoints)
            {
                devices.Add((endpoint.ID, endpoint.FriendlyName));
            }
            return devices.ToArray();
        }

        private void InitializePlayer()
        {
            _outputDevice?.Dispose();
            
            if (string.IsNullOrEmpty(SelectedDeviceId))
            {
                _outputDevice = new WasapiOut(AudioClientShareMode.Shared, 100);
            }
            else
            {
                using var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDevice(SelectedDeviceId);
                _outputDevice = new WasapiOut(device, AudioClientShareMode.Shared, false, 100);
            }

            _bufferedWaveProvider = new BufferedWaveProvider(_waveFormat)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromMinutes(5)
            };

            _volumeProvider = new VolumeSampleProvider(_bufferedWaveProvider.ToSampleProvider());
            _outputDevice.Init(_volumeProvider);
        }

        public void ChangeDevice(string deviceId)
        {
            SelectedDeviceId = deviceId;
            float currentVolume = Volume;
            Stop();
            InitializePlayer();
            Volume = currentVolume;
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
