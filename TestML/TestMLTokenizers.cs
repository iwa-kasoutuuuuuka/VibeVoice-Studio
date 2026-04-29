using System;
using System.IO;
using Microsoft.ML.Tokenizers;

class Program {
    static void Main() {
        try {
            string jsonPath = @"e:\app\VibeVoiceStudio\publish_v1.1.1_final_v2\models\tokenizer.json";
            Console.WriteLine($"Loading tokenizer from {jsonPath}...");
            
            var t = typeof(Tokenizer);
            Console.WriteLine($"Type: {t.FullName}");
            foreach(var m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)) {
                Console.WriteLine($"Static Method: {m.Name}");
            }
        } catch (Exception e) {
            Console.WriteLine("!!! ERROR !!!");
            Console.WriteLine(e.Message);
            if (e.InnerException != null) Console.WriteLine($"Inner: {e.InnerException.Message}");
            Console.WriteLine(e.StackTrace);
        }
    }
}
