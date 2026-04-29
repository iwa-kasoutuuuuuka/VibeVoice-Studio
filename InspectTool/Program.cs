using System;
using System.IO;
using Microsoft.ML.OnnxRuntime;
using System.Linq;

class Program {
    static void Main(string[] args) {
        string dir = @"e:\app\VibeVoiceStudio\publish_v1.1.1_final_v3\models";
        string m = args.Length > 0 ? args[0] : "text_encoder.onnx";
        if (Path.IsPathRooted(m)) {
            dir = Path.GetDirectoryName(m);
            m = Path.GetFileName(m);
        }
        try {
            using var s = new InferenceSession(Path.Combine(dir, m));
            Console.WriteLine($"--- {m} ---");
            foreach (var o in s.InputMetadata) {
                string dims = string.Join(", ", o.Value.Dimensions.Select(d => d.ToString()));
                Console.WriteLine($"  In: {o.Key} [{dims}] ({o.Value.ElementType})");
            }
            foreach (var o in s.OutputMetadata) {
                string dims = string.Join(", ", o.Value.Dimensions.Select(d => d.ToString()));
                Console.WriteLine($"  Out: {o.Key} [{dims}] ({o.Value.ElementType})");
            }
        } catch (Exception e) {
            Console.WriteLine(e.Message);
        }
    }
}
