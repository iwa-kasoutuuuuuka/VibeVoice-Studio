using System;
using System.Diagnostics;
using System.Linq;
using MeCab;

class Program {
    static void Main() {
        try {
            // MeCab を強制的にロードさせる
            // 辞書なしでダミーパラメータで作成を試みる（DLLロードのチェックが目的）
            var tagger = MeCabTagger.Create(new MeCabParam());
            Console.WriteLine("MeCab loaded.");
            
            var modules = Process.GetCurrentProcess().Modules;
            foreach (ProcessModule m in modules) {
                if (m.ModuleName.ToLower().Contains("mecab")) {
                    Console.WriteLine($"Found: {m.FileName}");
                }
            }
        } catch (Exception e) {
            Console.WriteLine($"Error: {e.Message}");
            if (e.InnerException != null) Console.WriteLine($"Inner: {e.InnerException.Message}");
            Console.WriteLine(e.StackTrace);
        }
    }
}
