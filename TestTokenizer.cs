using System;
using Tokenizers.DotNet;
using System.IO;

class Program {
    static void Main() {
        try {
            Console.WriteLine("Loading Tokenizer...");
            // hf_tokenizers.dll がロードされるタイミングを確認
            var tokenizer = new Tokenizer("tokenizer.json"); 
            Console.WriteLine("Successfully loaded Tokenizer.");
        } catch (Exception e) {
            Console.WriteLine("!!! ERROR !!!");
            Console.WriteLine(e.Message);
            if (e.InnerException != null) Console.WriteLine($"Inner: {e.InnerException.Message}");
            Console.WriteLine(e.StackTrace);
        }
    }
}
