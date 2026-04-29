using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VibeVoiceNative.Inference;
using VibeVoiceNative.Inference.Text;
using VibeVoiceNative.UI.Models;
using VibeVoiceNative.UI.Audio;
using Serilog;

namespace VibeVoiceNative.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty] public partial string StatusMessage { get; set; } = "Ready";
        [ObservableProperty] public partial double Progress { get; set; }
        [ObservableProperty] public partial bool IsEngineReady { get; set; }
        [ObservableProperty] public partial string RefAudioPath { get; set; } = string.Empty;
        [ObservableProperty] public partial double Speed { get; set; } = 1.0;
        [ObservableProperty] public partial double Pitch { get; set; } = 0.0;
        [ObservableProperty] public partial double Volume { get; set; } = 0.8;
        [ObservableProperty] public partial string InputText { get; set; } = string.Empty;
        [ObservableProperty] public partial bool IsEnglish { get; set; }
        [ObservableProperty] public partial bool IsGenerating { get; set; }
        [ObservableProperty] public partial string SelectedDevice { get; set; } = "DirectML";
        [ObservableProperty] public partial int InferenceSteps { get; set; } = 16;

        public ObservableCollection<VoiceModel> FilteredVoiceGallery { get; } = new();
        public ObservableCollection<AudioDevice> AudioDevices { get; } = new();
        public ObservableCollection<UserDictionaryEntry> UserDictionary { get; } = new();
        public ObservableCollection<string> InferenceDevices { get; } = new() { "CPU", "DirectML", "CUDA" };

        [ObservableProperty] public partial VoiceModel? SelectedVoice { get; set; }
        [ObservableProperty] public partial AudioDevice? SelectedAudioDevice { get; set; }

        public Func<Task<string?>>? PickSaveFileAsync { get; set; }
        public Func<Task<string?>>? PickRefAudioFileAsync { get; set; }
        public Func<Task<string?>>? PickFolderAsync { get; set; }

        private readonly IVibeVoiceEngine _engine;
        private readonly AudioPlayer _player;
        private readonly Serilog.ILogger _logger;
        private float[]? _lastGeneratedAudio;
        private float[]? _currentAudioBuffer;

        public float[]? CurrentAudioBuffer
        {
            get => _currentAudioBuffer;
            set => SetProperty(ref _currentAudioBuffer, value);
        }

        public MainViewModel() : this(new VibeVoiceOnnxEngine()) { }

        public MainViewModel(IVibeVoiceEngine engine)
        {
            _engine = engine;
            _player = new AudioPlayer();
            
            if (!Directory.Exists("logs")) Directory.CreateDirectory("logs");
            _logger = new LoggerConfiguration()
                .WriteTo.File("logs/ui_.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
            
            _logger.Information("MainViewModel initialized with high-speed parallel support.");

            SetupAudioDevices();
            InferenceSteps = 16;
        }

        private void SetupAudioDevices()
        {
            AudioDevices.Clear();
            foreach (var d in AudioPlayer.GetDevices()) AudioDevices.Add(new AudioDevice { Id = d.id, Name = d.name });
            if (AudioDevices.Count > 0) SelectedAudioDevice = AudioDevices[0];
        }

        partial void OnSelectedDeviceChanged(string value)
        {
            _logger.Information("Device changed to {Device}, triggering re-init.", value);
            _ = InitializeEngine(); // デバイス変更時に自動的にエンジンを再初期化
        }

        [RelayCommand]
        public async Task InitializeEngine()
        {
            try {
                IsEngineReady = false;
                StatusMessage = $"Loading ({SelectedDevice})...";
                string modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
                await _engine.InitializeAsync(modelDir, SelectedDevice);
                IsEngineReady = true;
                StatusMessage = "Engine Ready";
            } catch (Exception ex) {
                _logger.Error(ex, "Engine init failed.");
                StatusMessage = "Init Error";
            }
        }

        [RelayCommand]
        private async Task GenerateAudio()
        {
            if (string.IsNullOrWhiteSpace(InputText)) return;
            if (!IsEngineReady) await InitializeEngine();

            try {
                IsGenerating = true;
                StatusMessage = "Generating...";
                var allAudio = new List<float>();
                _player.Stop();
                _player.Play(SelectedAudioDevice?.Id);

                var sentences = TextSplitter.SplitIntoSentences(InputText);
                var context = new VibeVoiceContext();

                for (int i = 0; i < sentences.Count; i++) {
                    if (!IsGenerating) break;
                    await foreach (var chunk in _engine.GenerateAudioStreamingAsync(sentences[i], RefAudioPath, Speed, Pitch, InferenceSteps, new Progress<double>(), context)) {
                        if (!IsGenerating) break;
                        _player.AddSamples(chunk);
                        allAudio.AddRange(chunk);
                        CurrentAudioBuffer = chunk;
                    }
                }
                _lastGeneratedAudio = allAudio.ToArray();
                StatusMessage = "Complete";
            } catch (Exception ex) {
                _logger.Error(ex, "Gen failed.");
                StatusMessage = "Gen Error";
            } finally { IsGenerating = false; Progress = 100; }
        }

        [RelayCommand]
        public async Task BatchExport()
        {
            if (string.IsNullOrWhiteSpace(InputText) || PickFolderAsync == null) return;
            var folder = await PickFolderAsync();
            if (string.IsNullOrEmpty(folder)) return;

            try {
                IsGenerating = true;
                var lines = InputText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                int completed = 0;

                // GPU 推論時は並列数を上げ、CPU の場合は抑える
                int maxConcurrency = (SelectedDevice == "CPU") ? 2 : 4;
                var options = new ParallelOptions { MaxDegreeOfParallelism = maxConcurrency };
                
                await Parallel.ForEachAsync(lines.Select((text, index) => (text, index)), options, async (item, ct) => {
                    if (!IsGenerating) return;
                    
                    var allAudio = new List<float>();
                    try {
                        await foreach (var chunk in _engine.GenerateAudioStreamingAsync(item.text, RefAudioPath, Speed, Pitch, InferenceSteps, new Progress<double>())) {
                            if (!IsGenerating) break;
                            allAudio.AddRange(chunk);
                        }
                        
                        string path = Path.Combine(folder, $"{item.index + 1:D3}.wav");
                        VibeVoiceNative.Inference.Audio.AudioExporter.SaveAsWav(path, allAudio.ToArray(), 24000);
                        
                        Interlocked.Increment(ref completed);
                        StatusMessage = $"Batch: {completed}/{lines.Length}";
                        Progress = (double)completed / lines.Length * 100;
                    } catch (Exception ex) {
                        _logger.Error(ex, "Batch item failed: {Text}", item.text);
                    }
                });

                StatusMessage = "Batch Complete";
            } catch (Exception ex) {
                _logger.Error(ex, "Batch export failed.");
                StatusMessage = "Batch Error";
            } finally { IsGenerating = false; Progress = 100; }
        }

        [RelayCommand] public void Stop() { _engine.Stop(); _player.Stop(); IsGenerating = false; }
        [RelayCommand] public async Task SaveAudio() { if (_lastGeneratedAudio != null && PickSaveFileAsync != null) { var path = await PickSaveFileAsync(); if (!string.IsNullOrEmpty(path)) VibeVoiceNative.Inference.Audio.AudioExporter.SaveAsWav(path, _lastGeneratedAudio, 24000); } }
        [RelayCommand] public void AddUserDict(string original) { if (!string.IsNullOrWhiteSpace(original)) UserDictionary.Add(new UserDictionaryEntry { OriginalText = original }); }
        [RelayCommand] public void RemoveUserDict(UserDictionaryEntry entry) { UserDictionary.Remove(entry); }

        public void Dispose() { _player?.Dispose(); (_engine as IDisposable)?.Dispose(); }
    }
}
