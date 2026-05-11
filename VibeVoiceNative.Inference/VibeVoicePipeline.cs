using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using VibeVoiceNative.Inference.Text;
using VibeVoiceNative.Inference.Audio;
using Serilog;

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
        private bool _stopRequested = false;

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

        public void RequestStop() => _stopRequested = true;

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
            Log.Information("Text: '{Text}', Tokens: {Count}, Lang: {Lang}", text, tokens.Length, lang);
            if (tokens.Length == 0) yield break;

            // 2. Text Encoding (Text to Condition/Embedding)
            var inputIds = new DenseTensor<long>(new[] { 1, tokens.Length });
            var attentionMask = new DenseTensor<long>(new[] { 1, tokens.Length });
            for (int i = 0; i < tokens.Length; i++) {
                inputIds[0, i] = tokens[i];
                attentionMask[0, i] = 1;
            }

            using var condResult = _textToCond.Run(new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
            });
            var textEmbeds = condResult.First().AsTensor<float>(); // [1, seq, 896]
            int textLen = textEmbeds.Dimensions[1];

            progress?.Report(10);

            // 3. LM Autoregressive Loop (Prefill + Step)
            // 音声のコンディショニング列を生成する
            var conditioningList = new List<float[]>();
            
            // KV Cache 初期化 [20, 1, 2, 0, 64] -> 現実的には [20, 1, 2, 1, 64] など最小サイズから始める必要がある場合がある
            // モデルの定義に合わせて初期空テンソルを作成
            var pastKeys = new DenseTensor<float>(new[] { 20, 1, 2, 0, 64 });
            var pastValues = new DenseTensor<float>(new[] { 20, 1, 2, 0, 64 });
            var posIds = new DenseTensor<long>(new[] { 1, textLen });
            for (int i = 0; i < textLen; i++) posIds[0, i] = i;

            // Prefill
            using var prefillResult = _lmPrefill.Run(new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor("inputs_embeds", textEmbeds),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
                NamedOnnxValue.CreateFromTensor("position_ids", posIds),
                NamedOnnxValue.CreateFromTensor("past_keys", pastKeys),
                NamedOnnxValue.CreateFromTensor("past_values", pastValues)
            });

            var lmHidden = prefillResult.First().AsTensor<float>(); // [1, textLen, 896]
            Log.Information("LM Prefill output shape: {Shape}", string.Join(",", lmHidden.Dimensions.ToArray()));
            
            var currentKeys = prefillResult.ElementAt(1).AsTensor<float>();
            var currentValues = prefillResult.ElementAt(2).AsTensor<float>();

            // Prefill の最後の隠れ状態を最初のコンディショニングとして使用
            float[] lastHidden = new float[896];
            int lastIdx = (int)lmHidden.Dimensions[1] - 1;
            if (lastIdx < 0) {
                Log.Error("LM hidden states empty.");
                yield break;
            }
            for (int i = 0; i < 896; i++) lastHidden[i] = lmHidden.GetValue(lastIdx * 896 + i);
            conditioningList.Add(lastHidden);

            // Step Loop (最大長まで生成)
            int maxGenLen = Math.Min(500, textLen * 10);
            for (int i = 0; i < maxGenLen; i++)
            {
                if (_stopRequested) yield break;

                var stepInput = new DenseTensor<float>(new[] { 1, 1, 896 });
                for (int j = 0; j < 896; j++) stepInput.SetValue(j, lastHidden[j]);
                
                var stepPos = new DenseTensor<long>(new[] { 1, 1 });
                stepPos.SetValue(0, (long)(textLen + i));
                
                var stepMask = new DenseTensor<long>(new[] { 1, textLen + i + 1 });
                for (int j = 0; j < stepMask.Length; j++) stepMask.SetValue(j, 1);

                using var stepResult = _lmStep.Run(new List<NamedOnnxValue> {
                    NamedOnnxValue.CreateFromTensor("inputs_embeds", stepInput),
                    NamedOnnxValue.CreateFromTensor("attention_mask", stepMask),
                    NamedOnnxValue.CreateFromTensor("position_ids", stepPos),
                    NamedOnnxValue.CreateFromTensor("past_keys", currentKeys),
                    NamedOnnxValue.CreateFromTensor("past_values", currentValues)
                });

                var nextHidden = stepResult.First().AsTensor<float>();
                currentKeys = stepResult.ElementAt(1).AsTensor<float>();
                currentValues = stepResult.ElementAt(2).AsTensor<float>();

                lastHidden = new float[896];
                for (int j = 0; j < 896; j++) lastHidden[j] = (float)Math.Tanh(nextHidden.GetValue(j) / 100.0);
                conditioningList.Add(lastHidden);

                if (i > 20 && lastHidden.Max(Math.Abs) < 0.001) break; 
            }

            int generatedSeqLen = conditioningList.Count;
            Log.Information("LM generation complete. Generated sequence length: {Len}", generatedSeqLen);

            progress?.Report(30);

            // 4. Voice Conditioning (Acoustic Connector)
            var speechLatent = new DenseTensor<float>(new[] { 1, 64 });
            var rndLatent = new Random();
            for (int i = 0; i < 64; i++) speechLatent.SetValue(i, (float)(rndLatent.NextDouble() * 2 - 1) * 0.05f);
            
            using var voiceCondResult = _acousticConnector.Run(new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor("speech_latent", speechLatent)
            });
            var voiceCondTensor = voiceCondResult.First().AsTensor<float>();
            float[] voiceCond = voiceCondTensor.ToArray();
            // Voice Conditioning も Tanh で正規化
            for (int j = 0; j < voiceCond.Length; j++) voiceCond[j] = (float)Math.Tanh(voiceCond[j] / 10.0);

            // 5. CFM (Conditional Flow Matching) Loop
            var latents = new DenseTensor<float>(new[] { generatedSeqLen, 64 });
            var rnd = new Random();
            for (int i = 0; i < latents.Length; i++) latents.SetValue(i, NextGaussian(rnd));

            Log.Information("Diffusion started. Initial latent Max: {Max}", latents.Max(Math.Abs));

            float dt = 1.0f / steps;
            for (int s = 0; s < steps; s++)
            {
                if (_stopRequested) yield break;

                float t = s * dt;
                
                var timesteps = new DenseTensor<long>(new[] { generatedSeqLen });
                for (int i = 0; i < generatedSeqLen; i++) timesteps.SetValue(i, (long)(t * 1000));

                var currentCond = new DenseTensor<float>(new[] { generatedSeqLen, 896 });
                for (int i = 0; i < generatedSeqLen; i++) {
                    for (int j = 0; j < 896; j++) {
                        currentCond.SetValue(i * 896 + j, conditioningList[i][j] + voiceCond[j]);
                    }
                }

                using var predResult = _predictionHead.Run(new List<NamedOnnxValue> {
                    NamedOnnxValue.CreateFromTensor("noisy_latent", latents),
                    NamedOnnxValue.CreateFromTensor("timestep", timesteps),
                    NamedOnnxValue.CreateFromTensor("conditioning", currentCond)
                });
                var velocity = predResult.First().AsTensor<float>();

                float stepMaxVel = 0;
                for (int i = 0; i < latents.Length; i++)
                {
                    float v = (velocity.Length > i) ? velocity.GetValue(i) : 0;
                    if (float.IsNaN(v) || float.IsInfinity(v)) v = 0;
                    
                    stepMaxVel = Math.Max(stepMaxVel, Math.Abs(v));
                    latents.SetValue(i, latents.GetValue(i) + v * dt);
                }

                if (s % 8 == 0) Log.Information("Step {Step}/{Total}: t={T:F2}, Max vel={Vel}", s, steps, t, stepMaxVel);
                progress?.Report(30 + (int)(60.0 * (s + 1) / steps));
                await Task.Yield(); 
            }

            for (int i = 0; i < latents.Length; i++) latents.SetValue(i, latents.GetValue(i) * 10.0f);
            Log.Information("Diffusion complete. Boosted latent Max: {Max}", latents.Max(Math.Abs));

            // 6. Vocoder (Acoustic Decoder)
            var vocInput = new DenseTensor<float>(new[] { 1, 64, generatedSeqLen });
            for (int i = 0; i < generatedSeqLen; i++)
                for (int j = 0; j < 64; j++)
                    vocInput.SetValue(j * generatedSeqLen + i, latents.GetValue(i * 64 + j));

            using var vocResult = _vocoder.Run(new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor("latent", vocInput)
            });
            var audioResultTensor = vocResult.First().AsTensor<float>();
            var audio = audioResultTensor.ToArray();

            // ゲイン削除済

            var finalAudio = AudioProcessor.ApplyEffects(audio, speed, pitch);
            
            float maxAmp = finalAudio.Length > 0 ? finalAudio.Max(Math.Abs) : 0;
            Log.Information("Generation complete. Samples: {Samples}, Max amplitude: {Max}", finalAudio.Length, maxAmp);

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
