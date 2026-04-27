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

namespace VibeVoiceNative.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        public ObservableCollection<VoiceModel> VoiceGallery { get; } = new();

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
        public partial bool IsEngineReady { get; set; }

        private readonly IVibeVoiceEngine _engine;
        private readonly AudioPlayer _player;
        private float[]? _lastGeneratedAudio;

        public MainViewModel()
        {
            _engine = new VibeVoiceOnnxEngine();
            _player = new AudioPlayer();
            
            // サンプルデータの投入
            VoiceGallery.Add(new VoiceModel { Name = "Default Female", Language = "Japanese", Gender = "Female", FilePath = "models/ref_female.wav" });
            VoiceGallery.Add(new VoiceModel { Name = "Default Male", Language = "English", Gender = "Male", FilePath = "models/ref_male.wav" });
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
                Progress = 0;
                string modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
                var progressReporter = new Progress<double>(v => Progress = v);
                await _engine.InitializeAsync(modelDir, true, progressReporter);
                IsEngineReady = true;
            }
            catch (Exception)
            {
                // TODO: エラーダイアログの表示
                IsEngineReady = false;
            }
        }

        [RelayCommand]
        private async Task GenerateAudio(string text)
        {
            if (!IsEngineReady || string.IsNullOrWhiteSpace(text)) return;

            Progress = 0;
            _player.Stop();
            _player.Volume = (float)Volume;
            _player.Play();

            var sentences = TextSplitter.SplitIntoSentences(text);
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
        }

        public void Dispose()
        {
            _player?.Dispose();
            (_engine as IDisposable)?.Dispose();
        }
    }
}
