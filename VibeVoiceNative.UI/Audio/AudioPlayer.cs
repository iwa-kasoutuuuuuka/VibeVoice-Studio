using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;

namespace VibeVoiceNative.UI.Audio
{
    public class AudioPlayer : IDisposable
    {
        private IWavePlayer? _outputDevice;
        private BufferedWaveProvider? _waveProvider;
        private readonly WaveFormat _format = new(24000, 16, 1); // 24kHz Mono

        public float Volume
        {
            get => _outputDevice?.Volume ?? 1.0f;
            set { if (_outputDevice != null) _outputDevice.Volume = value; }
        }

        public AudioPlayer()
        {
            _waveProvider = new BufferedWaveProvider(_format) { BufferDuration = TimeSpan.FromSeconds(20), DiscardOnBufferOverflow = true };
        }

        public static List<(string id, string name)> GetDevices()
        {
            var devices = new List<(string id, string name)>();
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var caps = WaveOut.GetCapabilities(i);
                devices.Add((i.ToString(), caps.ProductName));
            }
            return devices;
        }

        public void Play(string? deviceId = null)
        {
            Stop();
            int devIndex = int.TryParse(deviceId, out int idx) ? idx : -1;
            _outputDevice = new WaveOutEvent { DeviceNumber = devIndex };
            _outputDevice.Init(_waveProvider);
            _outputDevice.Play();
        }

        public void AddSamples(float[] samples)
        {
            if (_waveProvider == null) return;
            
            // float -> int16
            byte[] buffer = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short s = (short)(Math.Clamp(samples[i], -1.0f, 1.0f) * 32767);
                buffer[i * 2] = (byte)(s & 0xff);
                buffer[i * 2 + 1] = (byte)((s >> 8) & 0xff);
            }
            _waveProvider.AddSamples(buffer, 0, buffer.Length);
        }

        public void Stop()
        {
            _outputDevice?.Stop();
            _waveProvider?.ClearBuffer();
        }

        public void Dispose()
        {
            Stop();
            _outputDevice?.Dispose();
        }
    }
}
