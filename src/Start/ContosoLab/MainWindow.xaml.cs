using Microsoft.Graphics.Imaging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AI.Imaging;
using Microsoft.Windows.AI.Search.Experimental.AppContentIndex;
using Microsoft.Windows.AI.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ContosoLab
{
    public sealed partial class MainWindow : Window
    {
        #region Helper methods
        private void EnableUI()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                SearchButton.IsEnabled = true;
                AskButton.IsEnabled = true;
                SearchTextBox.IsEnabled = true;
                StatusText.Text = "Ready";
            });
        }

        private void ShowStatusMessage(string message)
        {
            DispatcherQueue.TryEnqueue(() => StatusText.Text = message);
        }

        private static ScrollViewer? GetScrollViewer(DependencyObject root)
        {
            if (root is ScrollViewer sv) return sv;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private void CenterWindowOnCurrentMonitor(int windowWidth, int windowHeight)
        {
            // Get window handle and AppWindow
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            string fullPath = Path.Combine(AppContext.BaseDirectory, "Assets/appicon.ico");
            if (File.Exists(fullPath))
            {
                appWindow.SetIcon(fullPath);
            }

            // Get the DisplayArea for the current window (handles multi-monitor)
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);

            if (displayArea != null)
            {
                // Get the work area (excludes taskbar) of the display
                var workArea = displayArea.WorkArea;

                // Calculate centered position
                int x = Math.Max(workArea.X + (workArea.Width - windowWidth) / 2, 0);
                int y = Math.Max(workArea.Y + (workArea.Height - windowHeight) / 2, 0);

                // Move and resize the window
                appWindow.MoveAndResize(new RectInt32
                {
                    X = x,
                    Y = y,
                    Width = windowWidth,
                    Height = windowHeight
                });
            }
        }
        #endregion

        public MainWindow()
        {
            InitializeComponent();

            CenterWindowOnCurrentMonitor(1600, 1200);

            MainGrid.Loaded += async (s, e) =>
            {
                bool flowControl = await InitializeAppContentIndexer();
                if (!flowControl)
                {
                    return;
                }
                flowControl = await InitializeOCR();
                if (!flowControl)
                {
                    return;
                }
                flowControl = await LoadAndIndexPdf();
                if (!flowControl)
                {
                    return;
                }
                flowControl = await InitializeLanguageModel();
                if (!flowControl)
                {
                    return;
                }
                EnableUI();
            };
            MainGrid.Unloaded += (s, e) =>
            {
                indexer?.Dispose();
                recognizer?.Dispose();
                languageModel?.Dispose();
            };
        }

        #region Initialize AppContentIndexer and OCR
        AppContentIndexer? indexer;
        TextRecognizer? recognizer;
        LanguageModel? languageModel;
        private async Task<bool> InitializeAppContentIndexer()
        {
            return true;
        }

        private async Task<bool> InitializeOCR()
        {
            return true;
        }
        #endregion

        #region Load And Index Pdf
        private async Task<bool> LoadAndIndexPdf()
        {
            return true;
        }
        #endregion

        #region Search and Show PDF Pages
        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
        }
        #endregion

        #region Initialize Language Model
        private async Task<bool> InitializeLanguageModel()
        {
            return true;
        }
        #endregion

        #region Ask Button and Generate Response
        private async void AskButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
        }
        #endregion
    }
}

