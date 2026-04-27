using System;
using System.Reflection;
using Tokenizers.DotNet;

class Program {
    static void Main() {
        var t = typeof(Tokenizer);
        Console.WriteLine("Constructors:");
        foreach(var c in t.GetConstructors()) Console.WriteLine(c);
        Console.WriteLine("Methods:");
        foreach(var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)) {
            if(m.DeclaringType == t) Console.WriteLine(m);
        }
    }
}
