#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VibeVoiceNative.Inference;
using VibeVoiceNative.Inference.Text;
using VibeVoiceNative.Inference.Audio;
using VibeVoiceNative.UI.Models;
using VibeVoiceNative.UI.Audio;
using VibeVoiceNative.UI.Services;
using Serilog;

namespace VibeVoiceNative.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty] public partial string StatusMessage { get; set; } = "Ready / 準備完了";
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
        [ObservableProperty] public partial bool IsApiServerRunning { get; set; }

        public ObservableCollection<VoiceModel> VoiceGallery { get; } = new();
        public ObservableCollection<AudioDevice> AudioDevices { get; } = new();
        public ObservableCollection<UserDictionaryEntry> UserDictionary { get; } = new();
        public ObservableCollection<string> InferenceDevices { get; } = new() { "CPU", "DirectML", "CUDA" };

        [ObservableProperty] public partial VoiceModel? SelectedVoice { get; set; }
        [ObservableProperty] public partial AudioDevice? SelectedAudioDevice { get; set; }

        public Func<Task<string?>>? PickSaveFileAsync { get; set; }
        public Func<Task<string?>>? PickRefAudioFileAsync { get; set; }
        public Func<Task<string?>>? PickFolderAsync { get; set; }

        private readonly IVibeVoiceEngine _engine;
        private readonly VibeVoiceNative.UI.Audio.AudioPlayer _player;
        private readonly VibeVoiceApiServer _apiServer;
        private readonly ModelDownloadManager _downloadManager;
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
            _player = new VibeVoiceNative.UI.Audio.AudioPlayer();
            _apiServer = new VibeVoiceApiServer(ApiGenerateCallback);
            _downloadManager = new ModelDownloadManager();
            
            string? baseDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            string logDir = Path.Combine(baseDir, "logs");
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            _logger = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(logDir, "ui_.log"), rollingInterval: RollingInterval.Day)
                .CreateLogger();
            
            SetupAudioDevices();
            InferenceSteps = 16;
        }

        private void SetupAudioDevices()
        {
            AudioDevices.Clear();
            foreach (var d in VibeVoiceNative.UI.Audio.AudioPlayer.GetDevices()) AudioDevices.Add(new AudioDevice { Id = d.id, Name = d.name });
            if (AudioDevices.Count > 0) SelectedAudioDevice = AudioDevices[0];
        }

        [RelayCommand]
        public void SetLanguage(string langCode)
        {
            IsEnglish = (langCode == "EN");
            StatusMessage = IsEnglish ? "Language switched to English" : "言語を日本語に切り替えました";
            _logger.Information("Language mode changed: {Lang}", langCode);
        }

        private async Task<float[]> ApiGenerateCallback(string text, string voiceName)
        {
            var voice = VoiceGallery.FirstOrDefault(v => v.Name.Equals(voiceName, StringComparison.OrdinalIgnoreCase)) ?? SelectedVoice;
            string path = voice?.FilePath ?? RefAudioPath;
            
            var allAudio = new List<float>();
            await foreach (var chunk in _engine.GenerateAudioStreamingAsync(text, path, Speed, Pitch, InferenceSteps, new Progress<double>()))
            {
                allAudio.AddRange(chunk);
            }
            return allAudio.ToArray();
        }

        [RelayCommand]
        public void ToggleApiServer()
        {
            if (IsApiServerRunning) { _apiServer.Stop(); IsApiServerRunning = false; StatusMessage = "API Server Stopped / APIサーバー停止"; }
            else { _apiServer.Start(); IsApiServerRunning = true; StatusMessage = "API Server Running at :5050 / サーバー起動中"; }
        }

        [RelayCommand]
        public async Task InitializeEngine()
        {
            try {
                IsEngineReady = false;
                StatusMessage = "Loading Engine... / エンジン起動中...";
                string? exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
                string modelDir = Path.Combine(exeDir, "models");
                
                // bin フォルダに入っている場合、一つ上の階層も確認
                if (!Directory.Exists(modelDir))
                {
                    string? parentDir = Path.GetDirectoryName(exeDir);
                    if (parentDir != null)
                    {
                        string altModelDir = Path.Combine(parentDir, "models");
                        if (Directory.Exists(altModelDir)) modelDir = altModelDir;
                    }
                }

                await _engine.InitializeAsync(modelDir, SelectedDevice);
                IsEngineReady = true;
                StatusMessage = "Engine Ready / 準備完了";
            } catch (Exception ex) {
                _logger.Error(ex, "Init error");
                StatusMessage = "Init Error / 初期化失敗";
            }
        }

        [RelayCommand]
        private async Task GenerateAudio()
        {
            if (string.IsNullOrWhiteSpace(InputText)) return;
            if (!IsEngineReady) await InitializeEngine();

            try {
                IsGenerating = true;
                StatusMessage = "Generating... / 音声生成中...";
                var allAudio = new List<float>();
                _player.Stop();
                _player.Play(SelectedAudioDevice?.Id);

                var sentences = ParseScript(InputText);
                var context = new VibeVoiceContext();

                foreach (var (text, voicePath) in sentences) {
                    if (!IsGenerating) break;
                    string targetVoice = voicePath ?? RefAudioPath;
                    await foreach (var chunk in _engine.GenerateAudioStreamingAsync(text, targetVoice, Speed, Pitch, InferenceSteps, new Progress<double>(), context)) {
                        if (!IsGenerating) break;
                        _player.AddSamples(chunk);
                        allAudio.AddRange(chunk);
                        CurrentAudioBuffer = chunk;
                    }
                }
                _lastGeneratedAudio = AudioPreProcessor.CleanAndNormalize(allAudio.ToArray());
                StatusMessage = "Complete / 生成完了";
            } catch (Exception ex) {
                _logger.Error(ex, "Gen error");
                StatusMessage = "Gen Error / 生成失敗";
            } finally { IsGenerating = false; Progress = 100; }
        }

        private List<(string text, string? voicePath)> ParseScript(string script)
        {
            var result = new List<(string, string?)>();
            var lines = script.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"^\[(.+?)\]\s*(.*)$");
                if (match.Success)
                {
                    string speaker = match.Groups[1].Value;
                    string text = match.Groups[2].Value;
                    var voice = VoiceGallery.FirstOrDefault(v => v.Name.Equals(speaker, StringComparison.OrdinalIgnoreCase));
                    result.Add((text, voice?.FilePath));
                }
                else
                {
                    result.Add((line, null));
                }
            }
            return result;
        }

        [RelayCommand]
        public async Task BatchExport()
        {
            if (string.IsNullOrWhiteSpace(InputText) || PickFolderAsync == null) return;
            var folder = await PickFolderAsync();
            if (string.IsNullOrEmpty(folder)) return;

            try {
                IsGenerating = true;
                var script = ParseScript(InputText);
                int completed = 0;

                var options = new ParallelOptions { MaxDegreeOfParallelism = (SelectedDevice == "CPU") ? 2 : 4 };
                
                await Parallel.ForEachAsync(script.Select((item, index) => (item.text, item.voicePath, index)), options, async (item, ct) => {
                    if (!IsGenerating) return;
                    var allAudio = new List<float>();
                    string voice = item.voicePath ?? RefAudioPath;
                    await foreach (var chunk in _engine.GenerateAudioStreamingAsync(item.text, voice, Speed, Pitch, InferenceSteps, new Progress<double>())) {
                        if (!IsGenerating) break;
                        allAudio.AddRange(chunk);
                    }
                    string path = Path.Combine(folder, $"{item.index + 1:D3}_{item.text.Substring(0, Math.Min(5, item.text.Length))}.wav");
                    VibeVoiceNative.Inference.Audio.AudioExporter.SaveAsWav(path, AudioPreProcessor.CleanAndNormalize(allAudio.ToArray()), 24000);
                    
                    Interlocked.Increment(ref completed);
                    StatusMessage = $"Batch: {completed}/{script.Count} / 書き出し中...";
                    Progress = (double)completed / script.Count * 100;
                });

                StatusMessage = "Batch Complete / 一括生成完了";
            } catch (Exception ex) {
                _logger.Error(ex, "Batch error");
                StatusMessage = "Batch Error / 一括生成失敗";
            } finally { IsGenerating = false; Progress = 100; }
        }

        [RelayCommand] public void Stop() { _engine.Stop(); _player.Stop(); IsGenerating = false; }
        [RelayCommand] public async Task SaveAudio() { if (_lastGeneratedAudio != null && PickSaveFileAsync != null) { var path = await PickSaveFileAsync(); if (!string.IsNullOrEmpty(path)) VibeVoiceNative.Inference.Audio.AudioExporter.SaveAsWav(path, _lastGeneratedAudio, 24000); } }
        [RelayCommand] public void AddUserDict(string original) { if (!string.IsNullOrWhiteSpace(original)) UserDictionary.Add(new UserDictionaryEntry { OriginalText = original }); }
        [RelayCommand] public void RemoveUserDict(UserDictionaryEntry entry) { UserDictionary.Remove(entry); }

        public void Dispose() { _apiServer.Stop(); _player?.Dispose(); (_engine as IDisposable)?.Dispose(); }
    }
}
