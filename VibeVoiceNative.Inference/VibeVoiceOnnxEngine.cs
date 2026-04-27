using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.IO;

namespace VibeVoiceNative.Inference
{
    public interface IVibeVoiceEngine
    {
        Task InitializeAsync(string modelDir, bool useGpu, IProgress<double>? progress = null);
        Task<float[]> GenerateAudioAsync(string text, string refAudioPath, IProgress<double> progress);
        void Stop();
    }

    public class VibeVoiceOnnxEngine : IVibeVoiceEngine, IDisposable
    {
        private InferenceSession _textEncoder = null!;
        private InferenceSession _diffusionDecoder = null!;
        private InferenceSession _vocoder = null!;
        private bool _isInitialized = false;

        public async Task InitializeAsync(string modelDir, bool useGpu, IProgress<double>? progress = null)
        {
            var downloader = new Utils.ModelDownloader(modelDir);
            progress?.Report(0);
            await downloader.DownloadMissingModelsAsync(progress);
            
            string[] requiredFiles = { "text_encoder.onnx", "diffusion_decoder.onnx", "vocoder.onnx" };
            foreach (var file in requiredFiles)
            {
                if (!File.Exists(Path.Combine(modelDir, file)))
                    throw new FileNotFoundException($"Required model file missing after download attempt: {file}");
            }

            await Task.Run(() =>
            {
                var options = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
                };

                if (useGpu)
                {
                    // DirectML (Windows での最もポータブルな GPU 加速)
                    try 
                    { 
                        options.AppendExecutionProvider_DML(0); 
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"DirectML initialization failed: {ex.Message}. Falling back to CPU.");
                    }
                }

                _textEncoder = new InferenceSession(Path.Combine(modelDir, "text_encoder.onnx"), options);
                _diffusionDecoder = new InferenceSession(Path.Combine(modelDir, "diffusion_decoder.onnx"), options);
                _vocoder = new InferenceSession(Path.Combine(modelDir, "vocoder.onnx"), options);

                _isInitialized = true;
            });
        }

        public async Task<float[]> GenerateAudioAsync(string text, string refAudioPath, IProgress<double> progress)
        {
            if (!_isInitialized) throw new InvalidOperationException("Engine not initialized.");

            // Placeholder logic for the full pipeline
            progress?.Report(10);
            await Task.Delay(500); // Simulate processing
            
            progress?.Report(50);
            await Task.Delay(500);

            progress?.Report(100);
            return new float[16000]; // Dummy audio
        }

        public void Stop() { }

        public void Dispose()
        {
            _textEncoder?.Dispose();
            _diffusionDecoder?.Dispose();
            _vocoder?.Dispose();
        }
    }
}
