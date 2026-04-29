using System;
using Microsoft.ML.OnnxRuntime;
using System.Linq;
using System.IO;

class Program {
    static void Main(string[] args) {
        string modelDir = args.Length > 0 ? args[0] : ".";
        if (!Directory.Exists(modelDir)) {
            Console.WriteLine($"Directory not found: {modelDir}");
            return;
        }

        var files = Directory.GetFiles(modelDir, "*.onnx");
        foreach(var m in files) {
            try {
                using var session = new InferenceSession(m);
                Console.WriteLine($"\n--- {Path.GetFileName(m)} ---");
                Console.WriteLine("Inputs:");
                foreach(var input in session.InputMetadata) {
                    Console.WriteLine($"  {input.Key}: [{string.Join(",", input.Value.Dimensions)}] ({input.Value.ElementType})");
                }
                Console.WriteLine("Outputs:");
                foreach(var output in session.OutputMetadata) {
                    Console.WriteLine($"  {output.Key}: [{string.Join(",", output.Value.Dimensions)}] ({output.Value.ElementType})");
                }
            } catch (Exception ex) {
                Console.WriteLine($"Error loading {m}: {ex.Message}");
            }
        }
    }
}
