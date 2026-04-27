using System;
using System.IO;
using Tokenizers.DotNet;
using System.Linq;

namespace VibeVoiceNative.Inference.Text
{
    public interface ITextProcessor
    {
        long[] TextToTokens(string text, string language);
    }

    public class VibeVoiceTextProcessor : ITextProcessor
    {
        private readonly Tokenizer _tokenizer;

        public VibeVoiceTextProcessor()
        {
            string tokenizerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "tokenizer.json");
            
            if (!File.Exists(tokenizerPath))
            {
                throw new FileNotFoundException($"Tokenizer dictionary not found: {tokenizerPath}. Please download it via the app UI first.");
            }

            _tokenizer = new Tokenizer(tokenizerPath);
        }

        public long[] TextToTokens(string text, string language)
        {
            var tokens = _tokenizer.Encode(text);
            return tokens.Select(id => (long)id).ToArray();
        }
    }
}

