using System;
using System.IO;
using Microsoft.ML.Tokenizers;

class Program {
    static void Main() {
        var path = ""e:\\app\\VibeVoiceStudio\\publish_v1.1.0\\models\\tokenizer.json"";
        if(File.Exists(path)) {
            try {
                using var stream = File.OpenRead(path);
                var tokenizer = Tokenizer.CreateHuggingFace(stream);
                var tokens = tokenizer.EncodeToIds(""Hello VibeVoice!"");
                Console.WriteLine($""Tokens: {string.Join("","", tokens)}"");
            } catch(Exception e) {
                Console.WriteLine(e.Message);
            }
        } else {
            Console.WriteLine(""Not found"");
        }
    }
}
