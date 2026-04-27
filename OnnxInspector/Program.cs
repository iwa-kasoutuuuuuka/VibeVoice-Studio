using System;
using Microsoft.ML.OnnxRuntime;
using System.Linq;

class Program {
    static void Main() {
        string[] models = { ""e:\\app\\VibeVoiceStudio\\publish_v1.1.0\\models\\text_encoder.onnx"", ""e:\\app\\VibeVoiceStudio\\publish_v1.1.0\\models\\acoustic_connector.onnx"", ""e:\\app\\VibeVoiceStudio\\publish_v1.1.0\\models\\acoustic_decoder.onnx"" };
        foreach(var m in models) {
            try {
                var session = new InferenceSession(m);
                Console.WriteLine($""\n--- {m} ---"");
                Console.WriteLine(""Inputs:"");
                foreach(var input in session.InputMetadata) {
                    Console.WriteLine($""  {input.Key}: [{string.Join("","", input.Value.Dimensions)}] ({input.Value.ElementType})"");
                }
                Console.WriteLine(""Outputs:"");
                foreach(var output in session.OutputMetadata) {
                    Console.WriteLine($""  {output.Key}: [{string.Join("","", output.Value.Dimensions)}] ({output.Value.ElementType})"");
                }
            } catch (Exception ex) {
                Console.WriteLine($""Error loading {m}: {ex.Message}"");
            }
        }
    }
}
