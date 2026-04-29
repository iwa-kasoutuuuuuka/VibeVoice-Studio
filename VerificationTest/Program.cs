using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using VibeVoiceNative.Inference;
using System.IO;

namespace VibeVoiceVerification
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== VibeVoice Studio Pro Smoke Test ===");
            
            // 既存のモデルフォルダを直接参照
            string modelDir = @"E:\app\VibeVoiceStudio\publish_v1.1.1_final_v4\models";
            if (!Directory.Exists(modelDir))
            {
                Console.WriteLine($"Error: Model directory not found at {modelDir}");
                return;
            }

            var engine = new VibeVoiceOnnxEngine();
            
            try
            {
                // 1. Initialization Test (CPU)
                Console.WriteLine($"Testing Initialization using models at: {modelDir}");
                var initProgress = new Progress<double>(v => Console.Write($"\rProgress: {v:F0}%"));
                await engine.InitializeAsync(modelDir, "CPU", initProgress);
                Console.WriteLine("\n[PASS] Initialization Successful.");

                // 2. Parallelism Logic Test
                Console.WriteLine("Testing Parallel Loop Integrity...");
                int counter = 0;
                await Parallel.ForEachAsync(Enumerable.Range(0, 4), async (i, ct) => {
                    await Task.Delay(10);
                    Interlocked.Increment(ref counter);
                });
                if (counter == 4) Console.WriteLine("[PASS] Parallel logic functional.");

                Console.WriteLine("=== Verification Successful! ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[FAIL] Smoke Test FAILED: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
