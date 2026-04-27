using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeCab;

namespace VibeVoiceNative.Inference.Text
{
    public interface ITextProcessor
    {
        long[] TextToTokens(string text, string language);
    }

    public class VibeVoiceTextProcessor : ITextProcessor
    {
        public long[] TextToTokens(string text, string language)
        {
            if (language.ToLower() == "japanese" || language == "ja")
            {
                return ProcessJapanese(text);
            }
            else
            {
                return ProcessEnglish(text);
            }
        }

        private long[] ProcessJapanese(string text)
        {
            var tokens = new List<long>();
            try
            {
                // MeCab の初期化 (辞書パスは環境に合わせて調整)
                string dicPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dic", "ipadic");
                var param = new MeCabParam { DicDir = dicPath };
                using var tagger = MeCabTagger.Create(param);
                
                var nodes = tagger.ParseToNodes(text);
                foreach (var node in nodes)
                {
                    if (node.CharType == 0) continue; // BOS/EOS skip

                    // 特徴量から読みを取得 (IPA辞書の場合、インデックス8が読み)
                    var features = node.Feature.Split(',');
                    string reading = features.Length > 8 ? features[8] : node.Surface;
                    
                    // カナを音素に変換 (簡略化した実装例)
                    var phonemes = ConvertKatakanaToPhonemes(reading);
                    foreach (var p in phonemes)
                    {
                        if (_phonemeToId.TryGetValue(p, out long id))
                            tokens.Add(id);
                    }
                }
            }
            catch (Exception)
            {
                // フォールバック: 文字コードをそのまま返す等の処理
            }
            return tokens.ToArray();
        }

        private List<string> ConvertKatakanaToPhonemes(string kana)
        {
            // TODO: カナ→音素変換テーブルの実装
            // 例: "ア" -> "a", "カ" -> "k", "a"
            return new List<string> { "a", "i", "u" }; // Dummy
        }

        private readonly Dictionary<string, long> _phonemeToId = new()
        {
            { "a", 1 }, { "i", 2 }, { "u", 3 }, { "e", 4 }, { "o", 5 }
        };

        private long[] ProcessEnglish(string text)
        {
            // TODO: G2P.Net or simple rule-based
            return new long[] { 201, 202, 203 };
        }
    }
}
