using System;
using Microsoft.ML.OnnxRuntime;

class Program {
    static void Main() {
        try {
            string path = @"e:\app\VibeVoiceStudio\publish_v1.1.1_final_v2\models\tts_lm_prefill.onnx";
            using var session = new InferenceSession(path);
            Console.WriteLine($"Model: {Path.GetFileName(path)}");
            foreach (var input in session.InputMetadata) {
                Console.WriteLine($"Input: {input.Key}");
            }
        } catch (Exception e) {
            Console.WriteLine(e.Message);
        }
    }
}
