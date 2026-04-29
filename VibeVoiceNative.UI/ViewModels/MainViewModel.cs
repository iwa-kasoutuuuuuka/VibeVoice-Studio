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
        [ObservableProperty] public partial int InferenceSteps { get; set; } = 16; // Default: Balanced

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
            _logger = new LoggerConfiguration().WriteTo.File("logs/ui_.log").CreateLogger();
            
            SetupAudioDevices();
            InferenceSteps = 16;
        }

        private void SetupAudioDevices()
        {
            AudioDevices.Clear();
            foreach (var d in AudioPlayer.GetDevices()) AudioDevices.Add(new AudioDevice { Id = d.id, Name = d.name });
            if (AudioDevices.Count > 0) SelectedAudioDevice = AudioDevices[0];
        }

        [RelayCommand]
        public async Task InitializeEngine()
        {
            StatusMessage = "Loading Engine...";
            await _engine.InitializeAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models"), SelectedDevice);
            IsEngineReady = true;
            StatusMessage = "Engine Ready";
        }

        [RelayCommand]
        private async Task GenerateAudio()
        {
            if (string.IsNullOrWhiteSpace(InputText)) return;
            if (!IsEngineReady) await InitializeEngine();

            try {
                IsGenerating = true;
                var allAudio = new List<float>();
                _player.Play(SelectedAudioDevice?.Id);

                var sentences = TextSplitter.SplitIntoSentences(InputText);
                var context = new VibeVoiceContext();

                for (int i = 0; i < sentences.Count; i++) {
                    await foreach (var chunk in _engine.GenerateAudioStreamingAsync(sentences[i], RefAudioPath, Speed, Pitch, InferenceSteps, new Progress<double>(), context)) {
                        _player.AddSamples(chunk);
                        allAudio.AddRange(chunk);
                        CurrentAudioBuffer = chunk;
                    }
                }
                _lastGeneratedAudio = allAudio.ToArray();
                StatusMessage = "Complete";
            } finally { IsGenerating = false; }
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

                // 並列実行 (CPU/GPU のリソースに合わせて並列数を制限)
                var options = new ParallelOptions { MaxDegreeOfParallelism = (SelectedDevice == "CPU") ? 2 : 4 };
                
                await Parallel.ForEachAsync(lines.Select((text, index) => (text, index)), options, async (item, ct) => {
                    var allAudio = new List<float>();
                    await foreach (var chunk in _engine.GenerateAudioStreamingAsync(item.text, RefAudioPath, Speed, Pitch, InferenceSteps, new Progress<double>())) {
                        allAudio.AddRange(chunk);
                    }
                    string path = Path.Combine(folder, $"{item.index + 1:D3}.wav");
                    VibeVoiceNative.Inference.Audio.AudioExporter.SaveAsWav(path, allAudio.ToArray(), 24000);
                    
                    Interlocked.Increment(ref completed);
                    StatusMessage = $"Batch: {completed}/{lines.Length}";
                    Progress = (double)completed / lines.Length * 100;
                });

                StatusMessage = "Batch Complete";
            } finally { IsGenerating = false; }
        }

        [RelayCommand] public void Stop() { _engine.Stop(); _player.Stop(); IsGenerating = false; }
        [RelayCommand] public async Task SaveAudio() { /* Implementation same as before */ }
        [RelayCommand] public void AddUserDict(string original) { /* Implementation same as before */ }
        [RelayCommand] public void RemoveUserDict(UserDictionaryEntry entry) { /* Implementation same as before */ }

        public void Dispose() { _player?.Dispose(); (_engine as IDisposable)?.Dispose(); }
    }
}
