using Microsoft.UI.Xaml;
using VibeVoiceNative.UI.ViewModels;
using System;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace VibeVoiceNative.UI
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; } = new MainViewModel();

        public MainWindow()
        {
            this.InitializeComponent();
            this.Activated += MainWindow_Activated;
            
            // ファイル保存ピッカーの設定
            ViewModel.PickSaveFileAsync = async () =>
            {
                var savePicker = new FileSavePicker();
                InitializeWithWindow.Initialize(savePicker, WindowNative.GetWindowHandle(this));
                savePicker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
                savePicker.FileTypeChoices.Add("WAV File", new[] { ".wav" });
                savePicker.SuggestedFileName = "VibeVoice_Generated";

                var file = await savePicker.PickSaveFileAsync();
                return file?.Path;
            };

            ViewModel.PickRefAudioFileAsync = async () =>
            {
                var openPicker = new FileOpenPicker();
                InitializeWithWindow.Initialize(openPicker, WindowNative.GetWindowHandle(this));
                openPicker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
                openPicker.FileTypeFilter.Add(".wav");

                var file = await openPicker.PickSingleFileAsync();
                return file?.Path;
            };
        }

        private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            this.Activated -= MainWindow_Activated; // 1回のみ実行
            await ViewModel.InitializeEngineCommand.ExecuteAsync(null);
        }
    }
}
