using Microsoft.UI.Xaml;
using WinRT.Interop;
using Windows.Storage.Pickers;
using VibeVoiceNative.UI.ViewModels;
using System;
using System.Threading.Tasks;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System.Linq;
using Microsoft.UI.Dispatching;
using System.Collections.Generic;
#nullable enable

namespace VibeVoiceNative.UI
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; } = new MainViewModel();
        private DispatcherQueueTimer? _renderTimer;
        private List<float[]> _spectrogramData = new();
        private const int MaxSpectrogramColumns = 100;

        public MainWindow()
        {
            this.InitializeComponent();
            this.Activated += MainWindow_Activated;
            
            SetupPickers();

            _renderTimer = this.DispatcherQueue.CreateTimer();
            if (_renderTimer != null)
            {
                _renderTimer.Interval = TimeSpan.FromMilliseconds(33); // ~30fps
                _renderTimer.Tick += (s, e) => WaveformCanvas.Invalidate();
                _renderTimer.Start();
            }
        }

        private void SetupPickers()
        {
            ViewModel.PickSaveFileAsync = async () =>
            {
                var savePicker = new FileSavePicker();
                InitializeWithWindow.Initialize(savePicker, WindowNative.GetWindowHandle(this));
                savePicker.FileTypeChoices.Add("WAV File", new[] { ".wav" });
                var file = await savePicker.PickSaveFileAsync();
                return file?.Path;
            };

            ViewModel.PickRefAudioFileAsync = async () =>
            {
                var openPicker = new FileOpenPicker();
                InitializeWithWindow.Initialize(openPicker, WindowNative.GetWindowHandle(this));
                openPicker.FileTypeFilter.Add(".wav");
                var file = await openPicker.PickSingleFileAsync();
                return file?.Path;
            };

            ViewModel.PickFolderAsync = async () =>
            {
                var folderPicker = new FolderPicker();
                InitializeWithWindow.Initialize(folderPicker, WindowNative.GetWindowHandle(this));
                folderPicker.FileTypeFilter.Add("*");
                var folder = await folderPicker.PickSingleFolderAsync();
                return folder?.Path;
            };
        }

        private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            this.Activated -= MainWindow_Activated;
            await ViewModel.InitializeEngineCommand.ExecuteAsync(null);
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Black);

            var samples = ViewModel.CurrentAudioBuffer;
            if (samples == null || samples.Length == 0) return;

            // 新しいデータをスペクトログラムバッファに追加
            if (_spectrogramData.Count >= MaxSpectrogramColumns) _spectrogramData.RemoveAt(0);
            _spectrogramData.Add(samples);

            var info = e.Info;
            float colWidth = (float)info.Width / MaxSpectrogramColumns;
            float rowHeight = (float)info.Height / 32; // 32段階の擬似周波数

            for (int x = 0; x < _spectrogramData.Count; x++)
            {
                var frame = _spectrogramData[x];
                // 簡易的な周波数分布を振幅からシミュレート
                for (int y = 0; y < 32; y++)
                {
                    // フレーム内の部分的な平均振幅を「エネルギー」として描画
                    int segmentSize = frame.Length / 32;
                    float energy = frame.Skip(y * segmentSize).Take(segmentSize).Select(Math.Abs).Average();
                    
                    // 色の計算 (紫 -> 赤 -> 黄)
                    byte intensity = (byte)Math.Min(255, energy * 2000);
                    var color = new SKColor(intensity, (byte)(intensity / 2), (byte)(255 - intensity));

                    using var paint = new SKPaint { Color = color };
                    canvas.DrawRect(x * colWidth, info.Height - (y * rowHeight), colWidth, rowHeight, paint);
                }
            }
        }
    }
}
