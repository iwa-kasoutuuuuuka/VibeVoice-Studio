using System;
using System.IO;
using Microsoft.ML.OnnxRuntime;

class Program
{
    static void Main()
    {
        string modelDir = @"e:\app\VibeVoiceStudio\publish_v1.1.0\models";
        var options = new SessionOptions();
        options.AppendExecutionProvider_CPU(0);
        try {
            Console.WriteLine("Loading text_encoder...");
            var s1 = new InferenceSession(Path.Combine(modelDir, "text_encoder.onnx"), options);
            Console.WriteLine("Loading diffusion_decoder...");
            var s2 = new InferenceSession(Path.Combine(modelDir, "diffusion_decoder.onnx"), options);
            Console.WriteLine("Loading vocoder...");
            var s3 = new InferenceSession(Path.Combine(modelDir, "vocoder.onnx"), options);
            Console.WriteLine("Success!");
        } catch (Exception ex) {
            Console.WriteLine(ex.ToString());
        }
    }
}
