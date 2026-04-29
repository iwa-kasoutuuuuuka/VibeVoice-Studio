using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using VibeVoiceNative.Inference;
using VibeVoiceNative.Inference.Text;
using VibeVoiceNative.UI.Models;
using VibeVoiceNative.UI.Audio;
using Serilog;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace VibeVoiceNative.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty] public partial string StatusMessage { get; set; } = "Ready";
        [ObservableProperty] public partial double Progress { get; set; }
        [ObservableProperty] public partial bool IsDownloading { get; set; }
        [ObservableProperty] public partial bool IsEngineReady { get; set; }
        [ObservableProperty] public partial string RefAudioPath { get; set; } = string.Empty;
        [ObservableProperty] public partial double Speed { get; set; } = 1.0;
        [ObservableProperty] public partial double Pitch { get; set; } = 0.0;
        [ObservableProperty] public partial double Volume { get; set; } = 0.8;
        [ObservableProperty] public partial string InputText { get; set; } = string.Empty;
        [ObservableProperty] public partial bool IsEnglish { get; set; }
        [ObservableProperty] public partial bool IsGenerating { get; set; }
        [ObservableProperty] public partial string SelectedDevice { get; set; } = "DirectML";

        public ObservableCollection<VoiceModel> VoiceGallery { get; } = new();
        public ObservableCollection<VoiceModel> FilteredVoiceGallery { get; } = new();
        public ObservableCollection<VibeVoiceModelInfo> AvailableModels { get; } = new();
        public ObservableCollection<AudioDevice> AudioDevices { get; } = new();
        public ObservableCollection<VoicePreset> Presets { get; } = new();
        public ObservableCollection<UserDictionaryEntry> UserDictionary { get; } = new();
        public ObservableCollection<string> InferenceDevices { get; } = new() { "CPU", "DirectML", "CUDA" };

        [ObservableProperty] public partial VoiceModel? SelectedVoice { get; set; }
        [ObservableProperty] public partial VibeVoiceModelInfo? SelectedModel { get; set; }
        [ObservableProperty] public partial AudioDevice? SelectedAudioDevice { get; set; }
        [ObservableProperty] public partial VoicePreset? SelectedPreset { get; set; }

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
            
            _logger.Information("MainViewModel initialized.");

            SetupModels();
            SetupAudioDevices();
            LoadGallery();
            LoadPresets();
            LoadUserDictionary();

            if (VoiceGallery.Count == 0) InitializeSampleGallery();
            UpdateFilteredGallery();

            if (AvailableModels.Count > 0) SelectedModel = AvailableModels[0];
            Volume = 0.8;
        }

        partial void OnIsEnglishChanged(bool value) => UpdateFilteredGallery();
        partial void OnVolumeChanged(double value) => _player.Volume = (float)value;
        partial void OnSelectedDeviceChanged(string value) => _logger.Information("Inference device changed to: {Device}", value);

        partial void OnSelectedVoiceChanged(VoiceModel? value)
        {
            if (value != null)
            {
                RefAudioPath = value.FilePath;
                foreach (var v in VoiceGallery) v.IsSelected = (v == value);
            }
        }

        private void SetupAudioDevices()
        {
            AudioDevices.Clear();
            var devices = AudioPlayer.GetDevices();
            foreach (var d in devices) AudioDevices.Add(new AudioDevice { Id = d.id, Name = d.name });
            if (AudioDevices.Count > 0) SelectedAudioDevice = AudioDevices[0];
        }

        private void SetupModels()
        {
            AvailableModels.Add(new VibeVoiceModelInfo { Name = "VibeVoice-0.5B-v4 (Latest)", Description = "Latest production model." });
        }

        [RelayCommand]
        public async Task InitializeEngine()
        {
            try {
                IsEngineReady = false;
                StatusMessage = "Loading Engine (" + SelectedDevice + ")...";
                string modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
                var progressReporter = new Progress<double>(v => Progress = v);
                await _engine.InitializeAsync(modelDir, SelectedDevice, progressReporter);
                IsEngineReady = true;
                StatusMessage = "Engine Ready";
            } catch (Exception ex) {
                _logger.Error(ex, "Engine initialization failed.");
                StatusMessage = $"Init Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task GenerateAudio()
        {
            if (string.IsNullOrWhiteSpace(InputText)) return;
            if (!IsEngineReady) { await InitializeEngine(); if (!IsEngineReady) return; }

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
                    var progressReporter = new Progress<double>(v => Progress = ((double)i / sentences.Count * 100) + (v / sentences.Count));
                    await foreach (var chunk in _engine.GenerateAudioStreamingAsync(sentences[i], RefAudioPath, Speed, Pitch, progressReporter, context)) {
                        if (!IsGenerating) break;
                        _player.AddSamples(chunk);
                        allAudio.AddRange(chunk);
                        CurrentAudioBuffer = chunk; 
                    }
                }
                if (IsGenerating) {
                    _lastGeneratedAudio = allAudio.ToArray();
                    StatusMessage = $"Complete ({(double)_lastGeneratedAudio.Length/24000:F1}s)";
                }
            } catch (Exception ex) {
                _logger.Error(ex, "Generation failed.");
                StatusMessage = $"Error: {ex.Message}";
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
                for (int i = 0; i < lines.Length; i++) {
                    StatusMessage = $"Batch Exporting {i + 1}/{lines.Length}...";
                    Progress = (double)i / lines.Length * 100;
                    
                    var allAudio = new List<float>();
                    await foreach (var chunk in _engine.GenerateAudioStreamingAsync(lines[i], RefAudioPath, Speed, Pitch, new Progress<double>())) {
                        allAudio.AddRange(chunk);
                    }
                    
                    string path = Path.Combine(folder, $"{i + 1:D3}.wav");
                    VibeVoiceNative.Inference.Audio.AudioExporter.SaveAsWav(path, allAudio.ToArray(), 24000);
                }
                StatusMessage = "Batch Export Complete";
            } catch (Exception ex) {
                _logger.Error(ex, "Batch export failed.");
                StatusMessage = "Batch Error: " + ex.Message;
            } finally { IsGenerating = false; Progress = 100; }
        }

        [RelayCommand]
        public void AddUserDict(string original)
        {
            if (string.IsNullOrWhiteSpace(original)) return;
            UserDictionary.Add(new UserDictionaryEntry { OriginalText = original, Reading = "" });
            SaveUserDictionary();
        }

        [RelayCommand]
        public void RemoveUserDict(UserDictionaryEntry entry) { UserDictionary.Remove(entry); SaveUserDictionary(); }

        private void SaveUserDictionary() { /* 実装略 */ }
        private void LoadUserDictionary() { /* 実装略 */ }

        // --- 以下、既存の保存・読み込み系ロジック ---
        [RelayCommand] public void Stop() { _engine.Stop(); _player.Stop(); IsGenerating = false; }
        [RelayCommand] public async Task PickRefAudio() { if (PickRefAudioFileAsync != null) { var path = await PickRefAudioFileAsync(); if (!string.IsNullOrEmpty(path)) RefAudioPath = path; } }
        [RelayCommand] public async Task SaveAudio() { if (_lastGeneratedAudio != null && PickSaveFileAsync != null) { var path = await PickSaveFileAsync(); if (!string.IsNullOrEmpty(path)) { VibeVoiceNative.Inference.Audio.AudioExporter.SaveAsWav(path, _lastGeneratedAudio, 24000); StatusMessage = "Saved."; } } }

        private void LoadGallery() { /* 実装略 */ }
        private void SaveGallery() { /* 実装略 */ }
        private void LoadPresets() { /* 実装略 */ }
        private void SavePresets() { /* 実装略 */ }
        private void InitializeSampleGallery() { /* 実装略 */ }
        private void UpdateFilteredGallery() { /* 実装略 */ }
        public void Dispose() { _player?.Dispose(); (_engine as IDisposable)?.Dispose(); }
    }
}
