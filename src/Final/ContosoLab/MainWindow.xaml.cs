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

            CenterWindowOnCurrentMonitor(1600,1200);
            
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
        private const string INDEX_NAME = "ContosoLabIndex";
        private AppContentIndexer? indexer;
        private TextRecognizer? recognizer;
        private LanguageModel? languageModel;
        
        private async Task<bool> InitializeAppContentIndexer()
        {
            return await Task.Run(async () =>
            {
                ShowStatusMessage("Getting indexer");
                var result = AppContentIndexer.GetOrCreateIndex(INDEX_NAME);
                if (result?.Succeeded != true)
                {
                    ShowStatusMessage("Failed to create indexer.");
                    return false;
                }

                indexer = result.Indexer;
                await indexer.WaitForIndexCapabilitiesAsync();
                var capabilities = indexer.GetIndexCapabilities();
                var supportedCaps = new
                {
                    TextLexical = capabilities.GetCapabilityState(IndexCapability.TextLexical).InitializationStatus,
                    TextSemantic = capabilities.GetCapabilityState(IndexCapability.TextSemantic).InitializationStatus,
                    ImageOcr = capabilities.GetCapabilityState(IndexCapability.ImageOcr).InitializationStatus,
                    ImageSemantic = capabilities.GetCapabilityState(IndexCapability.ImageSemantic).InitializationStatus
                };
                Debug.WriteLine($"Supported Capabilities: {supportedCaps.ToString()}");
                return true;
            });
        }

        private async Task<bool> InitializeOCR()
        {
            ShowStatusMessage("Getting OCR");
            var readyState = TextRecognizer.GetReadyState();

            if (readyState == Microsoft.Windows.AI.AIFeatureReadyState.NotReady)
            {
                await TextRecognizer.EnsureReadyAsync();
            }

            recognizer = await TextRecognizer.CreateAsync();
            if (recognizer == null)
            {
                ShowStatusMessage("Failed to get OCR.");
                return false;
            }

            return true;
        }
        #endregion

        #region Load And Index Pdf
        private const string pdfName = "IBM PC-DOS 1.0 (Aug. 1981).pdf";
        private const string INDEX_MARKER = "__INDEX_MARKER__";
        private const string marker = $"{INDEX_MARKER};{INDEX_NAME}";

        Windows.Data.Pdf.PdfDocument? pdfDocument;
        private async Task<bool> LoadAndIndexPdf()
        {
            ShowStatusMessage("Loading PDF document");
            string pdfPath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty, pdfName);
            pdfDocument = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(
                await Windows.Storage.StorageFile.GetFileFromPathAsync(pdfPath)).AsTask().ConfigureAwait(false);
            if (pdfDocument == null)
            {
                ShowStatusMessage("Failed to load PDF.");
                return false;
            }
            if (!IsPdfIndexAvailable())
            {
                await IndexPdf(pdfDocument);
            }

            return true;
        }

        private async Task IndexPdf(Windows.Data.Pdf.PdfDocument document)
        {
            if (indexer == null || document == null)
            {
                return;
            }
            await Task.Run(async () =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    for (var i = 1u; i <= document.PageCount; i++)
                    {
                        ShowStatusMessage($"Processing Page {i}...");
                        var imageBuffer = await GetImageFromPdf(i);
                        var page = imageBuffer.CopyToSoftwareBitmap();
                        //var text = await GetTextFromPage(i);    
                        string contentId = $"Page{i}";
                        var sw1 = Stopwatch.StartNew();
                        var imageContent = AppManagedIndexableAppContent.CreateFromBitmap(contentId, page);
                        indexer.AddOrUpdate(imageContent);
                        sw1.Stop();
                        Debug.WriteLine($"Indexed Page {i} in {sw1.ElapsedMilliseconds}ms");
                    }
                    indexer.AddOrUpdate(AppManagedIndexableAppContent.CreateFromString(marker, marker));
                    sw.Stop();
                    Debug.WriteLine($"Indexing complete. Time Elapsed: {sw.ElapsedMilliseconds}ms");
                    ShowStatusMessage($"Indexing complete. Time Elapsed: {sw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    ShowStatusMessage($"Error during indexing: {ex.Message}");
                }
            });
        }

        private async Task<string> GetTextFromPage(uint i)
        {
            var page = await GetImageFromPdf(i);
            if (page == null)
            {
                return string.Empty;
            }

            var result = await recognizer?.RecognizeTextFromImageAsync(page);
            if (result?.Lines == null || result.Lines.Length == 0)
            {
                return string.Empty;
            }

            var lines = result.Lines;

            // Remove the last line, which will contain the page number
            var text = string.Join("\n", lines.Take(lines.Length - 1).Select(l => l.Text) ?? []);

            return text;
        }

        private bool IsPdfIndexAvailable()
        {
            if (indexer == null)
            {
                return false;
            }
            var markerQuery = indexer.CreateTextQuery(marker);
            var markerMatches = markerQuery.GetNextMatches(1);
            return markerMatches?.Any(match => match.ContentId == marker) ?? false;
        }

        private async Task<ImageBuffer?> GetImageFromPdf(uint pageNum)
        {
            if (pdfDocument == null || pageNum < 1 || pageNum > pdfDocument.PageCount)
            {
                return null;
            }

            try
            {
                using var page = pdfDocument.GetPage(pageNum - 1);
                using var ras = new InMemoryRandomAccessStream();
                await page.RenderToStreamAsync(ras).AsTask().ConfigureAwait(false);

                ras.Seek(0);
                var decoder = await BitmapDecoder.CreateAsync(ras).AsTask().ConfigureAwait(false);

                var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied).AsTask().ConfigureAwait(false);

                var imageBuffer = ImageBuffer.CreateForSoftwareBitmap(softwareBitmap);
                return imageBuffer;
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"Error rendering PDF page: {ex.Message}");
                return null;
            }
        }
        #endregion

        #region Search and Show PDF Pages
        private async Task<ImageSource?> ConvertImageBufferToImageSourceAsync(ImageBuffer? buffer)
        {
            if (buffer == null) return null;

            var softwareBitmap = buffer.CopyToSoftwareBitmap();

            // Ensure BGRA8 Premultiplied (required by SoftwareBitmapSource)
            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            {
                var converted = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                softwareBitmap.Dispose();
                softwareBitmap = converted;
            }

            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(softwareBitmap);
            softwareBitmap.Dispose(); 
            return source;
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (indexer == null || string.IsNullOrEmpty(SearchTextBox?.Text))
            {
                return;
            }

            string searchTerm = SearchTextBox.Text;
            var query = indexer.CreateImageQuery(searchTerm);
            IReadOnlyList<ImageQueryMatch> imageMatches = query.GetNextMatches(5);
            if (imageMatches == null || imageMatches.Count == 0)
            {
                ShowStatusMessage("No matches found.");
                PagesBox.ItemsSource = null;
                return;
            }
            var groupedMatches = imageMatches.GroupBy(m => m.ContentId.Replace("Page", string.Empty)).ToDictionary(kv => kv.Key, kv => kv.ToList());

            var pageList = await Task.WhenAll(groupedMatches
                .Select((p, m) => new
                {
                    Rank = m,
                    PageNum = uint.TryParse(p.Key, out uint pageNum) ? pageNum : 0,
                    Rects = p.Value.OfType<AppManagedImageQueryMatch>().Select(v => v.Subregion)
                })
                .Where(item => item.PageNum > 0)
                .Select(async p => new
                {
                    PageRank = p.Rank,
                    Page = await ConvertImageBufferToImageSourceAsync(await GetImageFromPdf(p.PageNum))
                }));
            PagesBox.ItemsSource = pageList.OrderBy(x => x.PageRank).Select(p => p.Page).ToList();
            var scrollViewer = GetScrollViewer(PagesBox);
            scrollViewer?.ScrollToHorizontalOffset(0);
            scrollViewer?.ScrollToVerticalOffset(0);
        }
        #endregion

        #region Initialize Language Model
        private async Task<bool> InitializeLanguageModel()
        {
            ShowStatusMessage("Getting Language Model");
            var readyState = LanguageModel.GetReadyState();

            if (readyState == Microsoft.Windows.AI.AIFeatureReadyState.NotReady)
            {
                await LanguageModel.EnsureReadyAsync();
            }

            languageModel = await LanguageModel.CreateAsync();
            if (languageModel == null)
            {
                ShowStatusMessage("Failed to get language model.");
                return false;
            }

            return true;
        }
        #endregion

        #region Ask Button and Generate Response
        private CancellationTokenSource? cts;
        private async void AskButton_Click(object sender, RoutedEventArgs e)
        {
            if (indexer == null || languageModel == null || string.IsNullOrEmpty(SearchTextBox?.Text))
            {
                return;
            }
            string searchTerm = SearchTextBox.Text;
            var query = indexer.CreateImageQuery(searchTerm);
            IReadOnlyList<ImageQueryMatch> imageMatches = query.GetNextMatches(5);
            if (imageMatches == null || imageMatches.Count == 0)
            {
                ShowStatusMessage("No matches found.");
                return;
            }
            var pages = imageMatches.Select(m => m.ContentId.Replace("Page", string.Empty)).Distinct().ToList();
            var sb = new System.Text.StringBuilder();
            foreach (var p in pages)
            {
                if (!uint.TryParse(p, out uint pageNum) || pageNum == 0)
                {
                    continue;
                }
                sb.AppendLine(await GetTextFromPage(pageNum));
            }

            string systemPrompt = $@"""
You are a knowledgeable assistant specialized in answering questions based solely on information from the context below, between ###. 
When responding, focus on delivering clear, accurate answers drawn only from the context, avoiding outside information or assumptions.

###
{sb.ToString()}
###
""";
            await GenerateResponse(systemPrompt);
        }

        private async Task GenerateResponse(string systemPrompt)
        {
            if (languageModel == null)
            {
                return;
            }
            ResultsText.Text = string.Empty;
            StatusText.Text = "Generating response...";
            cts = new CancellationTokenSource();
            StopButton.Visibility = Visibility.Visible;
            SearchButton.Visibility = Visibility.Collapsed;
            AskButton.Visibility = Visibility.Collapsed;
            try
            {
                var context = languageModel.CreateContext(systemPrompt);
                var oper = languageModel.GenerateResponseAsync(context, SearchTextBox.Text, new LanguageModelOptions());
                oper.Progress = (p, delta) =>
                {
                    DispatcherQueue.TryEnqueue(() => ResultsText.Text += delta);
                    if (cts?.IsCancellationRequested ?? false)
                    {
                        oper.Cancel();
                        ShowStatusMessage("Cancelled.");
                    }
                };
                var result = await oper;
                StatusText.Text = "Done.";
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"Error: {ex.Message}");
            }
            StopButton.Visibility = Visibility.Collapsed;
            SearchButton.Visibility = Visibility.Visible;
            AskButton.Visibility = Visibility.Visible;
            return;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            cts?.Cancel();
        }

        #endregion
    }
}

