#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using VibeVoiceNative.UI.Models;
using VibeVoiceNative.Inference;
using VibeVoiceNative.Inference.Audio;
using VibeVoiceNative.Inference.Text;
using VibeVoiceNative.Inference.Utils;

namespace VibeVoiceNative.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        public class VibeVoiceModelInfo
        {
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public string RepoUrl { get; set; } = "";
            public Dictionary<string, string> ModelUrls { get; set; } = new();
        }

        public ObservableCollection<VibeVoiceModelInfo> AvailableModels { get; } = new();

        [ObservableProperty]
        public partial VibeVoiceModelInfo? SelectedModel { get; set; }

        public ObservableCollection<VoiceModel> VoiceGallery { get; } = new();
        public ObservableCollection<VoiceModel> FilteredVoiceGallery { get; } = new();

        [ObservableProperty]
        public partial string StatusMessage { get; set; } = "Ready";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(GenerateAudioCommand))]
        public partial bool IsDownloading { get; set; }

        [ObservableProperty]
        public partial VoiceModel? SelectedVoice { get; set; }

        partial void OnSelectedVoiceChanged(VoiceModel? value)
        {
            if (value != null)
            {
                RefAudioPath = value.FilePath;
            }
        }

        [ObservableProperty]
        public partial string RefAudioPath { get; set; } = string.Empty;

        [ObservableProperty]
        public partial double Progress { get; set; }

        [ObservableProperty]
        public partial double Speed { get; set; } = 1.0;

        [ObservableProperty]
        public partial double Pitch { get; set; } = 0.0;

        [ObservableProperty]
        public partial double Volume { get; set; } = 0.8;

        partial void OnVolumeChanged(double value)
        {
            if (_player != null) _player.Volume = (float)value;
        }

        [ObservableProperty]
        public partial string InputText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsEnglish { get; set; }

        partial void OnIsEnglishChanged(bool value)
        {
            // クラッシュ回避のため OS 言語設定の強制変更を一時停止
            // var lang = value ? "en-US" : "ja-JP";
            // Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = lang;
            
            UpdateFilteredGallery();
        }

        private void UpdateFilteredGallery()
        {
            try
            {
                FilteredVoiceGallery.Clear();
                var langToFilter = IsEnglish ? "English" : "Japanese";
                var filtered = VoiceGallery.Where(v => v.Language == langToFilter).ToList();
                foreach (var v in filtered)
                {
                    FilteredVoiceGallery.Add(v);
                }
                
                if (FilteredVoiceGallery.Count > 0)
                {
                    SelectedVoice = FilteredVoiceGallery[0];
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Gallery Error: {ex.Message}";
            }
        }

        [ObservableProperty]
        public partial bool IsEngineReady { get; set; }

        private readonly IVibeVoiceEngine _engine;
        private readonly AudioPlayer _player;
        private float[]? _lastGeneratedAudio;

        public MainViewModel()
        {
            _engine = new VibeVoiceOnnxEngine();
            _player = new AudioPlayer();
            
            SetupModels();

            // サンプルデータの投入 (4種類)
            VoiceGallery.Add(new VoiceModel { Name = "日本女性 (JA Female)", Language = "Japanese", Gender = "Female", FilePath = "models/ref_ja_female.wav" });
            VoiceGallery.Add(new VoiceModel { Name = "日本男性 (JA Male)", Language = "Japanese", Gender = "Male", FilePath = "models/ref_ja_male.wav" });
            VoiceGallery.Add(new VoiceModel { Name = "English Female", Language = "English", Gender = "Female", FilePath = "models/ref_en_female.wav" });
            VoiceGallery.Add(new VoiceModel { Name = "English Male", Language = "English", Gender = "Male", FilePath = "models/ref_en_male.wav" });

            UpdateFilteredGallery();

            if (AvailableModels.Count > 0) SelectedModel = AvailableModels[0];
        }

        private void SetupModels()
        {
            AvailableModels.Add(new VibeVoiceModelInfo 
            { 
                Name = "Realtime-0.5B (ONNX Community)", 
                Description = "Fastest, community optimized for ONNX.",
                RepoUrl = "https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX",
                ModelUrls = new Dictionary<string, string> {
                    { "text_encoder.onnx", "https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX/resolve/main/text_encoder.onnx?download=true" },
                    { "text_encoder.onnx.data", "https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX/resolve/main/text_encoder.onnx.data?download=true" },
                    { "acoustic_connector.onnx", "https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX/resolve/main/acoustic_connector.onnx?download=true" },
                    { "acoustic_connector.onnx.data", "https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX/resolve/main/acoustic_connector.onnx.data?download=true" },
                    { "acoustic_decoder.onnx", "https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX/resolve/main/acoustic_decoder.onnx?download=true" },
                    { "acoustic_decoder.onnx.data", "https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX/resolve/main/acoustic_decoder.onnx.data?download=true" },
                    { "tts_lm_prefill.onnx", "https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX/resolve/main/tts_lm_prefill.onnx?download=true" },
                    { "tts_lm_prefill.onnx.data", "https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX/resolve/main/tts_lm_prefill.onnx.data?download=true" },
                    { "tts_lm_step.onnx", "https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX/resolve/main/tts_lm_step.onnx?download=true" },
                    { "tts_lm_step.onnx.data", "https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX/resolve/main/tts_lm_step.onnx.data?download=true" },
                    { "text_to_condition.onnx", "https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX/resolve/main/text_to_condition.onnx?download=true" },
                    { "text_to_condition.onnx.data", "https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX/resolve/main/text_to_condition.onnx.data?download=true" },
                    { "prediction_head.onnx", "https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX/resolve/main/prediction_head.onnx?download=true" },
                    { "prediction_head.onnx.data", "https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX/resolve/main/prediction_head.onnx.data?download=true" },
                    { "tokenizer.json", "https://huggingface.co/elbruno/VibeVoice-Realtime-0.5B-ONNX/resolve/main/tokenizer.json?download=true" }
                }
            });

            AvailableModels.Add(new VibeVoiceModelInfo { Name = "VibeVoice-Realtime-0.5B (Official)", RepoUrl = "https://huggingface.co/microsoft/VibeVoice-Realtime-0.5B" });
            AvailableModels.Add(new VibeVoiceModelInfo { Name = "VibeVoice-1.5B", RepoUrl = "https://huggingface.co/microsoft/VibeVoice-1.5B" });
            AvailableModels.Add(new VibeVoiceModelInfo { Name = "VibeVoice-Large", RepoUrl = "https://huggingface.co/aoi-ot/VibeVoice-Large" });
            AvailableModels.Add(new VibeVoiceModelInfo { Name = "VibeVoice-ASR", RepoUrl = "https://huggingface.co/microsoft/VibeVoice-ASR" });
        }

        [RelayCommand]
        private void AddToGallery()
        {
            if (string.IsNullOrEmpty(RefAudioPath)) return;
            
            VoiceGallery.Add(new VoiceModel 
            { 
                Name = Path.GetFileNameWithoutExtension(RefAudioPath),
                FilePath = RefAudioPath,
                Language = "Detected", // 実際は UI から取得
                Gender = "Unknown"
            });
        }

        [RelayCommand]
        private async Task PickRefAudio()
        {
            if (PickRefAudioFileAsync != null)
            {
                string? filePath = await PickRefAudioFileAsync();
                if (!string.IsNullOrEmpty(filePath))
                {
                    RefAudioPath = filePath;
                }
            }
        }

        public Func<Task<string?>>? PickRefAudioFileAsync { get; set; }
        public Func<Task<string?>>? PickSaveFileAsync { get; set; }

        [RelayCommand]
        private async Task SaveAudio()
        {
            if (_lastGeneratedAudio == null || _lastGeneratedAudio.Length == 0) return;

            if (PickSaveFileAsync != null)
            {
                string? filePath = await PickSaveFileAsync();
                if (!string.IsNullOrEmpty(filePath))
                {
                    AudioExporter.SaveAsWav(filePath, _lastGeneratedAudio);
                }
            }
        }

        [RelayCommand]
        public async Task InitializeEngine()
        {
            try
            {
                string modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
                
                // 必要なファイルが揃っているか確認
                string[] requiredFiles = { 
                    "text_encoder.onnx", "text_encoder.onnx.data",
                    "tts_lm_prefill.onnx", "tts_lm_prefill.onnx.data",
                    "tts_lm_step.onnx", "tts_lm_step.onnx.data",
                    "text_to_condition.onnx", "text_to_condition.onnx.data",
                    "prediction_head.onnx", "prediction_head.onnx.data",
                    "acoustic_connector.onnx", "acoustic_connector.onnx.data",
                    "acoustic_decoder.onnx", "acoustic_decoder.onnx.data",
                    "tokenizer.json"
                };
                bool allExist = true;
                foreach (var file in requiredFiles)
                {
                    if (!File.Exists(Path.Combine(modelDir, file)))
                    {
                        allExist = false;
                        break;
                    }
                }

                if (!allExist)
                {
                    if (SelectedModel == null || SelectedModel.ModelUrls.Count == 0)
                    {
                        StatusMessage = "This model requires manual ONNX export. Please place files in 'models' folder.";
                        IsEngineReady = false;
                        return;
                    }

                    IsDownloading = true;
                    StatusMessage = $"Starting download for {SelectedModel.Name}...";
                    
                    var downloader = new ModelDownloader(modelDir, SelectedModel.ModelUrls);
                    var downloadProgress = new Progress<double>(v => {
                        Progress = v;
                        StatusMessage = $"Downloading... {v:F1}%";
                    });

                    await downloader.DownloadMissingModelsAsync(downloadProgress);
                    IsDownloading = false;
                }

                StatusMessage = "Loading Engine...";
                Progress = 0;
                var progressReporter = new Progress<double>(v => Progress = v);
                await _engine.InitializeAsync(modelDir, true, progressReporter);
                IsEngineReady = true;
                StatusMessage = "Engine Ready";
                Progress = 100;
            }
            catch (Exception ex)
            {
                IsDownloading = false;
                StatusMessage = $"Error: {ex.Message}";
                IsEngineReady = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanGenerate))]
        private async Task GenerateAudio()
        {
            if (string.IsNullOrWhiteSpace(InputText))
            {
                StatusMessage = "Please enter text.";
                return;
            }

            // エンジンが未初期化なら自動的に初期化を試みる
            if (!IsEngineReady)
            {
                StatusMessage = "Engine not ready. Starting initialization...";
                await InitializeEngine();
                if (!IsEngineReady) return; // 初期化失敗時は中断
            }

            StatusMessage = "Generating speech...";
            Progress = 0;
            _player.Stop();
            _player.Volume = (float)Volume;
            _player.Play();

            var sentences = TextSplitter.SplitIntoSentences(InputText);
            var allAudio = new List<float>();
            
            for (int i = 0; i < sentences.Count; i++)
            {
                var progressReporter = new Progress<double>(v => 
                {
                    // 文ごとの進捗を全体の進捗にマッピング
                    Progress = ((double)i / sentences.Count * 100) + (v / sentences.Count);
                });

                float[] audioData = await _engine.GenerateAudioAsync(sentences[i], RefAudioPath, progressReporter);
                
                // TODO: ここで Speed/Pitch を適用するロジックをエンジン側に持たせるか、後処理する
                
                _player.AddSamples(audioData);
                allAudio.AddRange(audioData);
            }

            _lastGeneratedAudio = allAudio.ToArray();
            Progress = 100;
            StatusMessage = "Generation complete.";
        }

        private bool CanGenerate() => !IsDownloading;

        public void Dispose()
        {
            _player?.Dispose();
            (_engine as IDisposable)?.Dispose();
        }
    }
}
