using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.IO;
using Serilog;

namespace VibeVoiceNative.Inference
{
    public interface IVibeVoiceEngine
    {
        Task InitializeAsync(string modelDir, bool useGpu, IProgress<double>? progress = null);
        Task InitializeAsync(string modelDir, string device, IProgress<double>? progress = null);
        IAsyncEnumerable<float[]> GenerateAudioStreamingAsync(string text, string refAudioPath, double speed, double pitch, IProgress<double> progress, VibeVoiceContext? context = null);
        void Stop();
    }

    public class VibeVoiceOnnxEngine : IVibeVoiceEngine, IDisposable
    {
        private InferenceSession _textEncoder = null!;
        private InferenceSession _lmPrefill = null!;
        private InferenceSession _lmStep = null!;
        private InferenceSession _textToCond = null!;
        private InferenceSession _predictionHead = null!;
        private InferenceSession _vocoder = null!; // acoustic_decoder
        private InferenceSession _acousticConnector = null!;
        private Text.VibeVoiceTextProcessor? _textProcessor;
        private bool _isInitialized = false;
        private bool _stopRequested = false;
        private string? _modelDir;
        private readonly Serilog.ILogger _logger;

        public VibeVoiceOnnxEngine()
        {
            if (!Directory.Exists("logs")) Directory.CreateDirectory("logs");

            _logger = new LoggerConfiguration()
                .WriteTo.File("logs/engine_.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
            
            _logger.Information("VibeVoiceOnnxEngine instance created.");
        }

        public async Task InitializeAsync(string modelDir, bool useGpu, IProgress<double>? progress = null)
        {
            await InitializeAsync(modelDir, useGpu ? "DirectML" : "CPU", progress);
        }

        public async Task InitializeAsync(string modelDir, string device, IProgress<double>? progress = null)
        {
            _logger.Information("Initializing engine with device: {Device}", device);
            progress?.Report(0);

            if (!Directory.Exists(modelDir)) throw new DirectoryNotFoundException($"Model directory not found: {modelDir}");
            _modelDir = modelDir;

            await Task.Run(() =>
            {
                var options = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
                if (device == "DirectML") options.AppendExecutionProvider_DML(0);
                else if (device == "CUDA") options.AppendExecutionProvider_CUDA(0);

                _textProcessor = new Text.VibeVoiceTextProcessor(modelDir);

                try
                {
                    _textEncoder = LoadSession(Path.Combine(modelDir, "text_encoder.onnx"), options);
                    _lmPrefill = LoadSession(Path.Combine(modelDir, "lm_prefill.onnx"), options);
                    _lmStep = LoadSession(Path.Combine(modelDir, "lm_step.onnx"), options);
                    _textToCond = LoadSession(Path.Combine(modelDir, "text_to_cond.onnx"), options);
                    _predictionHead = LoadSession(Path.Combine(modelDir, "prediction_head.onnx"), options);
                    _vocoder = LoadSession(Path.Combine(modelDir, "acoustic_decoder.onnx"), options);
                    _acousticConnector = LoadSession(Path.Combine(modelDir, "acoustic_connector.onnx"), options);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to load session(s).");
                    throw;
                }
            });

            _isInitialized = true;
            progress?.Report(100);
            _logger.Information("Engine initialized successfully with {Device}.", device);
        }

        private InferenceSession LoadSession(string path, SessionOptions options)
        {
            string dir = Path.GetDirectoryName(path) ?? "";
            string fileName = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);

            string int8Path = Path.Combine(dir, $"{fileName}.int8{ext}");
            string finalPath = File.Exists(int8Path) ? int8Path : path;

            if (!File.Exists(finalPath)) throw new FileNotFoundException($"Model file not found: {Path.GetFileName(finalPath)}");
            _logger.Information("Loading model: {FileName} (Quantized: {IsQuantized})", Path.GetFileName(finalPath), finalPath == int8Path);
            return new InferenceSession(finalPath, options);
        }

        public async IAsyncEnumerable<float[]> GenerateAudioStreamingAsync(string text, string refAudioPath, double speed, double pitch, IProgress<double> progress, VibeVoiceContext? context = null)
        {
            if (!_isInitialized) throw new InvalidOperationException("Engine not initialized.");
            _stopRequested = false;

            var pipeline = new VibeVoicePipeline(_textEncoder, _lmPrefill, _lmStep, _textToCond, _predictionHead, _vocoder, _acousticConnector, _textProcessor!);
            
            await foreach (var chunk in pipeline.RunInferenceStreaming(text, refAudioPath, speed, pitch, progress, context))
            {
                if (_stopRequested) yield break;
                yield return chunk;
            }
        }

        public void Stop() { _stopRequested = true; }

        public void Dispose()
        {
            _textEncoder?.Dispose();
            _lmPrefill?.Dispose();
            _lmStep?.Dispose();
            _textToCond?.Dispose();
            _predictionHead?.Dispose();
            _vocoder?.Dispose();
            _acousticConnector?.Dispose();
        }
    }
}
