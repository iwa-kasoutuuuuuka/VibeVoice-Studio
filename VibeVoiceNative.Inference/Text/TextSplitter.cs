using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VibeVoiceNative.Inference.Text
{
    public static class TextSplitter
    {
        public static List<string> SplitIntoSentences(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();

            // 日本語の句読点および英語の終止符で分割
            string pattern = @"(?<=[。！？.!?.])\s*";
            var sentences = Regex.Split(text, pattern);
            
            var result = new List<string>();
            foreach (var s in sentences)
            {
                if (!string.IsNullOrWhiteSpace(s))
                    result.Add(s.Trim());
            }
            return result;
        }
    }
}
