using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using VibeVoiceNative.Inference;
using VibeVoiceNative.UI.ViewModels;
using System.IO;
using System.Linq;

namespace VibeVoiceVerification
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== VibeVoice Studio Ultimate Final Debug Test ===");
            
            // 1. Script Parsing Test
            Console.WriteLine("Testing Script Parsing...");
            var vm = new MainViewModel(new VibeVoiceOnnxEngine());
            string testScript = "[Speaker1] Hello\n[Speaker2] World\nPlain text";
            
            // Reflection を使って private な ParseScript をテスト
            var method = typeof(MainViewModel).GetMethod("ParseScript", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (List<(string text, string? voicePath)>)method.Invoke(vm, new object[] { testScript });

            if (result.Count == 3 && result[0].text == "Hello" && result[2].text == "Plain text")
                Console.WriteLine("[PASS] Script parsing logic functional.");
            else
                Console.WriteLine("[FAIL] Script parsing logic error.");

            // 2. Audio Pre-processing Test
            Console.WriteLine("Testing Audio Pre-processing...");
            float[] dummySamples = new float[] { 0, 0, 0.5f, -0.5f, 0, 0 };
            var cleaned = VibeVoiceNative.Inference.Audio.AudioPreProcessor.CleanAndNormalize(dummySamples);
            if (cleaned.Length < dummySamples.Length && Math.Abs(cleaned.Max()) > 0.8f)
                Console.WriteLine("[PASS] Audio pre-processing functional.");
            else
                Console.WriteLine("[FAIL] Audio pre-processing error.");

            // 3. API Server MemoryStream Test (Stability Check)
            Console.WriteLine("Testing API Server WAV Generation...");
            float[] testSamples = new float[24000]; // 1 sec of silence
            using (var ms = new MemoryStream())
            {
                VibeVoiceNative.Inference.Audio.AudioExporter.SaveAsWav(ms, testSamples, 24000);
                byte[] bytes = ms.ToArray();
                if (bytes.Length > 0) Console.WriteLine("[PASS] MemoryStream WAV export functional.");
                else Console.WriteLine("[FAIL] MemoryStream WAV export produced empty data.");
            }

            Console.WriteLine("=== Final Debug Complete ===");
        }
    }
}
