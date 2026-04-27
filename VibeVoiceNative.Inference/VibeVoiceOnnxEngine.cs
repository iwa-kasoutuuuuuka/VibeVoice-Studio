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
        private InferenceSession _lmPrefill = null!;
        private InferenceSession _lmStep = null!;
        private InferenceSession _textToCond = null!;
        private InferenceSession _predictionHead = null!;
        private InferenceSession _diffusionDecoder = null!;
        private InferenceSession _vocoder = null!;
        private bool _isInitialized = false;

        public async Task InitializeAsync(string modelDir, bool useGpu, IProgress<double>? progress = null)
        {
            progress?.Report(0);
            
            string[] requiredFiles = { 
                "text_encoder.onnx", "text_encoder.onnx.data",
                "tts_lm_prefill.onnx", "tts_lm_prefill.onnx.data",
                "tts_lm_step.onnx", "tts_lm_step.onnx.data",
                "text_to_condition.onnx", "text_to_condition.onnx.data",
                "prediction_head.onnx", "prediction_head.onnx.data",
                "acoustic_connector.onnx", "acoustic_connector.onnx.data",
                "acoustic_decoder.onnx", "acoustic_decoder.onnx.data" 
            };
            foreach (var file in requiredFiles)
            {
                if (!File.Exists(Path.Combine(modelDir, file)))
                    throw new FileNotFoundException($"Required model file missing: {file}");
            }

            await Task.Run(() =>
            {
                var options = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
                };

                // 推論加速プロバイダーの優先順位設定
                try
                {
                    // 1. TensorRT (最強)
                    options.AppendExecutionProvider_Tensorrt(0);
                    options.AppendExecutionProvider_CUDA(0);
                }
                catch
                {
                    try
                    {
                        // 2. CUDA (安定)
                        options.AppendExecutionProvider_CUDA(0);
                    }
                    catch
                    {
                        try
                        {
                            // 3. DirectML (Windows ポータブル GPU)
                            options.AppendExecutionProvider_DML(0);
                        }
                        catch
                        {
                            // 4. CPU (フォールバック)
                        }
                    }
                }

                _textEncoder = new InferenceSession(Path.Combine(modelDir, "text_encoder.onnx"), options);
                _lmPrefill = new InferenceSession(Path.Combine(modelDir, "tts_lm_prefill.onnx"), options);
                _lmStep = new InferenceSession(Path.Combine(modelDir, "tts_lm_step.onnx"), options);
                _textToCond = new InferenceSession(Path.Combine(modelDir, "text_to_condition.onnx"), options);
                _predictionHead = new InferenceSession(Path.Combine(modelDir, "prediction_head.onnx"), options);
                _diffusionDecoder = new InferenceSession(Path.Combine(modelDir, "acoustic_connector.onnx"), options);
                _vocoder = new InferenceSession(Path.Combine(modelDir, "acoustic_decoder.onnx"), options);

                _isInitialized = true;
            });
        }

        public async Task<float[]> GenerateAudioAsync(string text, string refAudioPath, IProgress<double> progress)
        {
            if (!_isInitialized) throw new InvalidOperationException("Engine not initialized.");

            return await Task.Run(() =>
            {
                progress?.Report(2);
                
                // 1. Text Processing
                var textProcessor = new Text.VibeVoiceTextProcessor();
                long[] tokens = textProcessor.TextToTokens(text, "japanese");

                progress?.Report(5);

                // 2. Audio Processing
                var audioProcessor = new Audio.AudioProcessor();
                float[,] melSpec = audioProcessor.ProcessReferenceAudio(refAudioPath);

                // 3. Run Pipeline
                return VibeVoicePipeline.RunInference(
                    tokens,
                    melSpec,
                    _textEncoder,
                    _lmPrefill,
                    _lmStep,
                    _textToCond,
                    _predictionHead,
                    _diffusionDecoder,
                    _vocoder,
                    progress
                );
            });
        }

        public void Stop() { }

        public void Dispose()
        {
            _textEncoder?.Dispose();
            _lmPrefill?.Dispose();
            _lmStep?.Dispose();
            _textToCond?.Dispose();
            _predictionHead?.Dispose();
            _diffusionDecoder?.Dispose();
            _vocoder?.Dispose();
        }
    }
}
