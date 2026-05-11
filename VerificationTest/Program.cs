using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using VibeVoiceNative.Inference;
using VibeVoiceNative.Inference.Text;

namespace VibeVoiceNative.Verification
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                string rootDir = @"E:\app\VibeVoiceStudio\publish_portable_v1.1.2";
                string modelDir = Path.Combine(rootDir, "models");
                
                Console.WriteLine("--- Verifying Path Resolution & Resource Loading ---");
                
                // エンジンの初期化 (これにより内部で TextProcessor も初期化される)
                var engine = new VibeVoiceOnnxEngine();
                await engine.InitializeAsync(modelDir, "CPU");
                Console.WriteLine("1. Engine Initialization: SUCCESS");

                // MeCab / Tokenizer の直接検証
                var processor = new VibeVoiceTextProcessor(modelDir);
                string testText = "こんにちは、これはテストです。";
                var tokens = processor.TextToTokens(testText, "japanese");
                
                Console.WriteLine($"2. Text Processing Test:");
                Console.WriteLine($"   Input: {testText}");
                Console.WriteLine($"   Tokens Count: {tokens.Length}");
                
                if (tokens.Length > 0)
                {
                    Console.WriteLine("   SUCCESS: MeCab and Tokenizer are working correctly.");
                }
                else
                {
                    Console.WriteLine("   FAILED: No tokens generated. Check dictionary path.");
                }

                // 読みの変換テスト
                string reading = processor.ConvertToReading("難読漢字のテスト");
                Console.WriteLine($"   Reading Test: 難読漢字のテスト -> {reading}");
                if (reading.Contains("ナンドク") || reading.Contains("なんどく"))
                {
                    Console.WriteLine("   SUCCESS: MeCab Dictionary (ipadic) is loaded correctly.");
                }

                Console.WriteLine("\nVERIFICATION COMPLETE!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAILED: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
