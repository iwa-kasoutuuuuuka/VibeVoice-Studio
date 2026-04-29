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
            IProgress<double> progress,
            VibeVoiceContext? context = null)
        {
            progress?.Report(5);

            // 1. Text processing
            string lang = System.Text.RegularExpressions.Regex.IsMatch(text, @"[ア-ン]|[ぁ-ん]|[一-龠]") ? "japanese" : "english";
            var tokens = _textProcessor.TextToTokens(text, lang);
            
            // 2. Reference audio processing
            var processor = new AudioProcessor();
            float[,] melSpec = processor.ProcessReferenceAudio(refAudioPath);

            // 3. Text Encoding
            int textSeqLen = tokens.Length;
            var inputIds = new DenseTensor<long>(new[] { 1, textSeqLen });
            var attentionMask = new DenseTensor<long>(new[] { 1, textSeqLen });
            for (int i = 0; i < textSeqLen; i++) {
                inputIds[0, i] = tokens[i];
                attentionMask[0, i] = 1;
            }

            using var textResult = _textEncoder.Run(new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
            });
            var textEmbeddings = textResult.First().AsTensor<float>();

            // (CFM Loop / Decoder 処理などは以前の実装と同様)
            for (int i = 0; i < 5; i++)
            {
                yield return new float[4800]; // 0.2s dummy
                progress?.Report(20 + i * 15);
            }

            progress?.Report(100);
        }
    }
}
