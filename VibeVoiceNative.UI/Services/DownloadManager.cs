using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;

namespace VibeVoiceNative.UI.Services
{
    public class ModelDownloadManager
    {
        private readonly HttpClient _client = new();

        public async Task DownloadModelAsync(string modelUrl, string destPath, IProgress<double> progress)
        {
            using var response = await _client.GetAsync(modelUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            var totalRead = 0L;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);
                totalRead += read;
                if (totalBytes != -1) progress.Report((double)totalRead / totalBytes * 100);
            }
        }

        public List<(string name, string url)> GetModelList()
        {
            // 実際には設定ファイルや API から取得
            return new List<(string name, string url)> {
                ("VibeVoice-0.5B-v4 (Base)", "https://huggingface.co/iwa-kasoutuuuuuka/VibeVoice/resolve/main/v4/text_encoder.onnx")
            };
        }
    }
}
