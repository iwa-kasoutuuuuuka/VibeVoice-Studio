using System;
using System.Speech.Synthesis;
using System.IO;

class Program {
    static void Main() {
        var synth = new SpeechSynthesizer();
        synth.SetOutputToWaveFile("test.wav");
        synth.Speak("ƒeƒXƒg‚Å‚·");
        Console.WriteLine("Done");
    }
}
