using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using VibeVoiceNative.UI.Models;
using VibeVoiceNative.Inference;
using VibeVoiceNative.Inference.Audio;

namespace VibeVoiceNative.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        public ObservableCollection<VoiceModel> VoiceGallery { get; } = new();

        [ObservableProperty]
        private VoiceModel _selectedVoice;

        partial void OnSelectedVoiceChanged(VoiceModel value)
        {
            if (value != null)
            {
                RefAudioPath = value.FilePath;
            }
        }
        [ObservableProperty]
        private string _refAudioPath = string.Empty;

        [ObservableProperty]
        private double _progress;

        [ObservableProperty]
        private bool _isEngineReady;

        private readonly IVibeVoiceEngine _engine;
        private readonly AudioPlayer _player;
        private float[] _lastGeneratedAudio;

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

        public Func<Task<string>> PickSaveFileAsync { get; set; }

        [RelayCommand]
        private async Task SaveAudio()
        {
            if (_lastGeneratedAudio == null || _lastGeneratedAudio.Length == 0) return;

            if (PickSaveFileAsync != null)
            {
                string filePath = await PickSaveFileAsync();
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
        private async Task GenerateAudio()
        {
            if (!IsEngineReady) return;

            Progress = 0;
            var progressReporter = new Progress<double>(v => Progress = v);
            
            _player.Stop();
            _player.Play(); // ストリーミング再生を開始

            // TODO: テキスト解析と推論の実行
            float[] audioData = await _engine.GenerateAudioAsync("テスト", RefAudioPath, progressReporter);
            _lastGeneratedAudio = audioData;
            
            _player.AddSamples(audioData);
        }

        public void Dispose()
        {
            _player?.Dispose();
            (_engine as IDisposable)?.Dispose();
        }
    }
}
