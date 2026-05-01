using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Tokenizers.DotNet;
using MeCab;

namespace VibeVoiceNative.Inference.Text
{
    public interface ITextProcessor
    {
        long[] TextToTokens(string text, string language);
    }

    public class VibeVoiceTextProcessor : ITextProcessor
    {
        private readonly Tokenizer? _tokenizer;
        private readonly MeCabTagger? _mecab;
        public Dictionary<string, string> UserDictionary { get; } = new();

        public VibeVoiceTextProcessor(string? modelDir = null)
        {
            string? baseDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            string tokenizerPath = Path.Combine(modelDir ?? Path.Combine(baseDir, "models"), "tokenizer.json");
            
            if (File.Exists(tokenizerPath)) 
            {
                try { _tokenizer = new Tokenizer(tokenizerPath); } catch { }
            }

            try
            {
                string? exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
                string? parentDir = Path.GetDirectoryName(exeDir);

                var possibleDicPaths = new List<string> {
                    Path.Combine(modelDir ?? "", "dic", "ipadic"),
                    Path.Combine(exeDir, "dic"),
                    Path.Combine(exeDir, "models", "dic", "ipadic")
                };
                
                if (parentDir != null)
                {
                    possibleDicPaths.Add(Path.Combine(parentDir, "dic"));
                    possibleDicPaths.Add(Path.Combine(parentDir, "models", "dic", "ipadic"));
                }

                string? dicPath = possibleDicPaths.FirstOrDefault(Directory.Exists);
                if (dicPath != null)
                {
                    _mecab = MeCabTagger.Create(new MeCabParam { DicDir = dicPath });
                }
            } catch { }
        }

        public long[] TextToTokens(string text, string language)
        {
            // ユーザー辞書の適用
            string processedText = ApplyUserDictionary(text);

            if (language.ToLower() == "japanese" && _mecab != null)
            {
                processedText = ConvertToReading(processedText);
            }
            else if (language.ToLower() == "english")
            {
                processedText = ProcessEnglishText(processedText);
            }

            if (_tokenizer == null) return Array.Empty<long>();
            var tokens = _tokenizer.Encode(processedText);
            return tokens.Select(id => (long)id).ToArray();
        }

        private string ApplyUserDictionary(string text)
        {
            string result = text;
            foreach (var entry in UserDictionary)
            {
                result = result.Replace(entry.Key, entry.Value);
            }
            return result;
        }

        private string ProcessEnglishText(string text)
        {
            var words = text.ToLowerInvariant().Split(new[] { ' ', '.', ',', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            var phs = words.Select(w => ConvertToPhonemes(w));
            return string.Join(" ", phs);
        }

        private string ConvertToPhonemes(string word)
        {
            var map = new Dictionary<string, string> {
                {"hello", "HH AH L OW"}, {"vibe", "V AY B"}, {"voice", "V OY S"},
                {"studio", "S T UW D IY OW"}, {"the", "DH AH"}, {"is", "IH Z"},
                {"a", "AH"}, {"test", "T EH S T"}, {"welcome", "W EH L K AH M"}
            };
            return map.TryGetValue(word, out var p) ? p : word;
        }

        public string ConvertToReading(string text)
        {
            if (_mecab == null) return text;
            text = NormalizeText(text);

            var readings = new List<string>();
            try
            {
                foreach (var node in _mecab.ParseToNodes(text))
                {
                    if (node.CharType > 0)
                    {
                        string[] features = node.Feature.Split(',');
                        string reading = features.Length > 7 ? features[7] : node.Surface;
                        readings.Add(reading);
                    }
                }
            }
            catch { return text; }

            return string.Join("", readings);
        }

        private string NormalizeText(string text)
        {
            text = text.Replace("〜", "から")
                       .Replace("＆", "アンド")
                       .Replace("％", "パーセント")
                       .Replace("＋", "プラス");

            return System.Text.RegularExpressions.Regex.Replace(text, @"\d+", m =>
            {
                if (long.TryParse(m.Value, out long val)) return NumberToKana(val);
                return m.Value;
            });
        }

        private string NumberToKana(long num)
        {
            if (num == 0) return "ゼロ";
            string[] digits = { "", "いち", "に", "さん", "よん", "ご", "ろく", "なな", "はち", "きゅう" };
            string[] units = { "", "じゅう", "ひゃく", "せん" };
            string[] bigUnits = { "", "まん", "おく" };
            
            string result = "";
            long temp = num;
            int bigUnitIdx = 0;

            while (temp > 0)
            {
                int part = (int)(temp % 10000);
                if (part > 0)
                {
                    string partStr = "";
                    int p = part;
                    for (int i = 0; i < 4 && p > 0; i++)
                    {
                        int d = p % 10;
                        if (d > 0)
                        {
                            string s = (d == 1 && i > 0) ? "" : digits[d];
                            partStr = s + units[i] + partStr;
                        }
                        p /= 10;
                    }
                    result = partStr + bigUnits[bigUnitIdx] + result;
                }
                temp /= 10000;
                bigUnitIdx++;
            }
            return result;
        }
    }
}
