using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using VibeVoiceNative.Inference;

namespace VibeVoiceNative.UI.Services
{
    public class VibeVoiceApiServer
    {
        private HttpListener? _listener;
        private bool _isRunning;
        private readonly Func<string, string, Task<float[]>> _generateCallback;

        public VibeVoiceApiServer(Func<string, string, Task<float[]>> generateCallback)
        {
            _generateCallback = generateCallback;
        }

        public void Start(int port = 5050)
        {
            if (_isRunning) return;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();
            _isRunning = true;
            
            Task.Run(ListenLoop);
        }

        private async Task ListenLoop()
        {
            while (_isRunning && _listener != null)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = ProcessRequest(context);
                }
                catch { }
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            var req = context.Request;
            var res = context.Response;

            if (req.Url?.AbsolutePath == "/tts")
            {
                string text = req.QueryString["text"] ?? "";
                string voice = req.QueryString["voice"] ?? "default";

                if (!string.IsNullOrEmpty(text))
                {
                    var audio = await _generateCallback(text, voice);
                    byte[] wav = GetWavBytes(audio);
                    
                    res.ContentType = "audio/wav";
                    res.ContentLength64 = wav.Length;
                    await res.OutputStream.WriteAsync(wav, 0, wav.Length);
                }
            }
            
            res.Close();
        }

        private byte[] GetWavBytes(float[] samples)
        {
            using var ms = new MemoryStream();
            VibeVoiceNative.Inference.Audio.AudioExporter.SaveAsWav(ms, samples, 24000);
            return ms.ToArray();
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
        }
    }
}
