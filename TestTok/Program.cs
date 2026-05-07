using System;
using System.IO;
using Tokenizers.DotNet;

class Program {
    static void Main() {
        var path = @"e:\app\VibeVoiceStudio\publish_v1.1.1_final_v4\models\tokenizer.json";
        if(File.Exists(path)) {
            try {
                var tokenizer = new Tokenizer(path);
                var tokens = tokenizer.Encode("Hello VibeVoice!");
                Console.WriteLine($"Tokens: {string.Join(", ", tokens)}");
            } catch(Exception e) {
                Console.WriteLine(e.Message);
            }
        } else {
            Console.WriteLine("Not found");
        }
    }
}
