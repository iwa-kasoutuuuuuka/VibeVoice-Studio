using System;
using System.Threading.Tasks;
using VibeVoiceNative.Inference;
using System.IO;
using System.Diagnostics;

class Program {
    static async Task Main() {
        var engine = new VibeVoiceOnnxEngine();
        
        // 実行バイナリの場所から相対的に models フォルダを探す
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string modelDir = @"E:\app\VibeVoiceStudio\publish_v1.1.1_final_v4\models";

        Console.WriteLine($"Initializing engine with modelDir: {modelDir}");
        try {
            if (!Directory.Exists(modelDir)) throw new DirectoryNotFoundException($"Models directory not found at {modelDir}");
            
            await engine.InitializeAsync(modelDir, true);

            Console.WriteLine("Generating audio (Streaming)...");
            var progress = new Progress<double>(p => Console.WriteLine($"Progress: {p:F1}%"));
            
            string dummyWav = Path.Combine(baseDir, "dummy_ref.wav");
            CreateDummyWav(dummyWav);
            
            int totalSamples = 0;
            var sw = Stopwatch.StartNew();
            
            await foreach (var chunk in engine.GenerateAudioStreamingAsync("こんにちは、VibeVoice Studioへようこそ。", dummyWav, 1.0f, 1.0f, progress))
            {
                totalSamples += chunk.Length;
                Console.WriteLine($"Received chunk: {chunk.Length} samples");
            }
            sw.Stop();
            
            double duration = (double)totalSamples / 24000;
            Console.WriteLine($"\nSUCCESS: Generated {totalSamples} samples ({duration:F2} seconds).");
            Console.WriteLine($"Inference Time: {sw.ElapsedMilliseconds}ms (RTF: {sw.ElapsedMilliseconds / (duration * 1000):F2})");
            
        } catch(Exception e) {
            Console.WriteLine("\n!!! ERROR CAUGHT !!!");
            Console.WriteLine($"Message: {e.Message}");
            if (e.InnerException != null) Console.WriteLine($"Inner: {e.InnerException.Message}");
            Console.WriteLine($"Stack: {e.StackTrace}");
        }
    }
    
    static void CreateDummyWav(string path) {
        int sampleRate = 24000;
        int seconds = 1;
        short[] data = new short[sampleRate * seconds];
        for (int i = 0; i < data.Length; i++) {
            data[i] = (short)(Math.Sin(2 * Math.PI * 440.0 * i / sampleRate) * 10000);
        }

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        bw.Write("RIFF".ToCharArray());
        bw.Write(36 + data.Length * 2);
        bw.Write("WAVE".ToCharArray());
        bw.Write("fmt ".ToCharArray());
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write("data".ToCharArray());
        bw.Write(data.Length * 2);
        foreach (var sample in data) bw.Write(sample);
    }
}
