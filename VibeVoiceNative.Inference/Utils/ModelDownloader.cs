using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace VibeVoiceNative.Inference.Utils
{
    public class ModelDownloader
    {
        private readonly string _modelDir;
        private readonly Dictionary<string, string> _modelUrls = new()
        {
            // TODO: 実際の VibeVoice ONNX モデルの配布先 URL を設定
            { "text_encoder.onnx", "https://huggingface.co/example/VibeVoice-ONNX/resolve/main/text_encoder.onnx" },
            { "diffusion_decoder.onnx", "https://huggingface.co/example/VibeVoice-ONNX/resolve/main/diffusion_decoder.onnx" },
            { "vocoder.onnx", "https://huggingface.co/example/VibeVoice-ONNX/resolve/main/vocoder.onnx" }
        };

        public ModelDownloader(string modelDir)
        {
            _modelDir = modelDir;
        }

        public async Task DownloadMissingModelsAsync(IProgress<double>? progress)
        {
            if (!Directory.Exists(_modelDir))
            {
                Directory.CreateDirectory(_modelDir);
            }

            using var client = new HttpClient();
            int completed = 0;

            foreach (var kvp in _modelUrls)
            {
                string filePath = Path.Combine(_modelDir, kvp.Key);
                if (!File.Exists(filePath))
                {
                    await DownloadFileAsync(client, kvp.Value, filePath, progress, completed, _modelUrls.Count);
                }
                completed++;
            }
        }

        private async Task DownloadFileAsync(HttpClient client, string url, string destination, IProgress<double>? progress, int completedFiles, int totalFiles)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var source = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0L;
            int read;

            while ((read = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);
                totalRead += read;

                if (totalBytes != -1)
                {
                    double fileProgress = (double)totalRead / totalBytes;
                    double totalProgress = ((double)completedFiles + fileProgress) / totalFiles * 100;
                    progress?.Report(totalProgress);
                }
            }
        }
    }
}
