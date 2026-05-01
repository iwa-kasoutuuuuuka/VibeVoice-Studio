using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using VibeVoiceNative.Inference.Text;
using VibeVoiceNative.Inference.Audio;

namespace VibeVoiceNative.Inference
{
    public class VibeVoicePipeline
    {
        private readonly InferenceSession _textEncoder;
        private readonly InferenceSession _lmPrefill;
        private readonly InferenceSession _lmStep;
        private readonly InferenceSession _textToCond;
        private readonly InferenceSession _predictionHead;
        private readonly InferenceSession _vocoder;
        private readonly InferenceSession _acousticConnector;
        private readonly ITextProcessor _textProcessor;

        public VibeVoicePipeline(
            InferenceSession textEncoder,
            InferenceSession lmPrefill,
            InferenceSession lmStep,
            InferenceSession textToCond,
            InferenceSession predictionHead,
            InferenceSession vocoder,
            InferenceSession acousticConnector,
            ITextProcessor textProcessor)
        {
            _textEncoder = textEncoder;
            _lmPrefill = lmPrefill;
            _lmStep = lmStep;
            _textToCond = textToCond;
            _predictionHead = predictionHead;
            _vocoder = vocoder;
            _acousticConnector = acousticConnector;
            _textProcessor = textProcessor;
        }

        public async IAsyncEnumerable<float[]> RunInferenceStreaming(
            string text,
            string refAudioPath,
            double speed,
            double pitch,
            int steps,
            IProgress<double> progress,
            VibeVoiceContext? context = null)
        {
            progress?.Report(5);

            // 1. Text processing
            string lang = System.Text.RegularExpressions.Regex.IsMatch(text, @"[ア-ン]|[ぁ-ん]|[一-龠]") ? "japanese" : "english";
            var tokens = _textProcessor.TextToTokens(text, lang);
            if (tokens.Length == 0) yield break;

            // 2. Text Conditioning & Encoding
            var inputIds = new DenseTensor<long>(new[] { 1, tokens.Length });
            var attentionMask = new DenseTensor<long>(new[] { 1, tokens.Length });
            for (int i = 0; i < tokens.Length; i++) {
                inputIds[0, i] = tokens[i];
                attentionMask[0, i] = 1;
            }

            // text_to_condition を使用して推論用のコンディショニングを取得
            using var condResult = _textToCond.Run(new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
            });
            var conditioning = condResult.First().AsTensor<float>(); // [1, seq, 896]
            int seqLen = conditioning.Dimensions[1];
            int hiddenDim = conditioning.Dimensions[2];

            progress?.Report(20);

            // 3. CFM (Conditional Flow Matching) Loop
            // Euler Method: x_{t+dt} = x_t + v(x_t, t) * dt
            // Noise (t=1.0) -> Data (t=0.0)
            
            var latents = new DenseTensor<float>(new[] { seqLen, 64 });
            var rnd = new Random();
            for (int i = 0; i < latents.Length; i++) 
            {
                latents.SetValue(i, NextGaussian(rnd));
            }

            // [1, seq, 896] -> [seq, 896]
            var flatCond = new DenseTensor<float>(new[] { seqLen, hiddenDim });
            for (int i = 0; i < seqLen; i++)
                for (int j = 0; j < hiddenDim; j++)
                    flatCond[i, j] = conditioning[0, i, j];

            float dt = -1.0f / steps;
            for (int s = 0; s < steps; s++)
            {
                float t = 1.0f + (s * dt);
                var timestep = new DenseTensor<long>(new[] { seqLen });
                for (int i = 0; i < seqLen; i++) timestep[i] = (long)(t * 1000);

                using var predResult = _predictionHead.Run(new List<NamedOnnxValue> {
                    NamedOnnxValue.CreateFromTensor("noisy_latent", latents),
                    NamedOnnxValue.CreateFromTensor("timestep", timestep),
                    NamedOnnxValue.CreateFromTensor("conditioning", flatCond)
                });
                var velocity = predResult.First().AsTensor<float>();

                for (int i = 0; i < latents.Length; i++)
                {
                    latents.SetValue(i, latents.GetValue(i) + velocity.GetValue(i) * dt);
                }

                progress?.Report(20 + (int)(70.0 * (s + 1) / steps));
                // 進行状況の報告のために空のチャンクを返す（必要に応じて）
                if (s % 4 == 0) await Task.Yield(); 
            }

            // 4. Vocoder (Acoustic Decoder)
            // [seq, 64] -> [1, 64, seq]
            var vocInput = new DenseTensor<float>(new[] { 1, 64, seqLen });
            for (int i = 0; i < seqLen; i++)
                for (int j = 0; j < 64; j++)
                    vocInput[0, j, i] = latents[i, j];

            using var vocResult = _vocoder.Run(new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor("latent", vocInput)
            });
            var audio = vocResult.First().AsTensor<float>().ToArray();

            // スピードとピッチの適用
            var finalAudio = AudioProcessor.ApplyEffects(audio, speed, pitch);
            
            yield return finalAudio;
            progress?.Report(100);
        }

        private float NextGaussian(Random rnd)
        {
            double u1 = 1.0 - rnd.NextDouble();
            double u2 = 1.0 - rnd.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2));
        }
    }
}
