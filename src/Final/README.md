# 🤖 ContosoLab - AI-Powered Document Search Assistant

## Step-by-Step Lab: Building a Windows AI-Powered PDF Search Application and Query

A comprehensive guide to creating a modern Windows application that leverages Microsoft's AI APIs for intelligent document search and natural language querying.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows%2011-blue.svg)
![AI](https://img.shields.io/badge/AI-Microsoft%20Experimental-orange.svg)

---

## 🎯 What You'll Build

By the end of this lab, you'll have created a sophisticated application featuring:

- 🖥️ **AI-Enhanced Windows Application** - Turbocharge a Windows application with AI features and modern UI
- 🔍 **AI-Powered Search** - Semantic search using Microsoft's experimental AI indexing
- 🤖 **RAG-Powered Queries** - Local Retrieval Augmented Generation queries using Semantic Search and Phi-Silica
- 💬 **Natural Language Queries** - Ask questions about documents in plain English
- 📄 **Advanced PDF Processing** - Text extraction with the OCR API and page rendering
- 🖼️ **Visual Results** - Preview relevant document pages alongside query results
- ⚡ **Real-time Responses** - Streaming AI responses with cancellation support

---

## 📋 Prerequisites

### System Requirements

- **Copilot+ PC** - Machine with an NPU (40+ TOPS) and 16GB RAM
- **Windows 11** (Build 26100 or later) - Required for AI features
- **.NET 9 SDK** - Latest version
- **Visual Studio 2022** (17.8 or later)

### Development Environment

1. **Visual Studio 2022** with these workloads:
   - .NET desktop development
   - WinUI Application Development

2. Developer Mode enabled in Windows Settings

---

## 🚀 Lab Steps

### **Step 1: Open the Current Project**

1. In Visual Studio, open `ContosoLab.sln` in the `Start` folder.
2. Run the application. It should display something like this:

![ContosoLab Initial Screen](Images/ContosoLab1.png)

This is a basic Windows application with the new Fluent UI and custom chrome. It contains only the UI (no event handlers), the logic to resize and position the window, and placeholders for the code to be inserted, enclosed in code regions.

![Start Code Structure](Images/StartCode.png)

We will add AI functionality to query a document, show the pages relevant to the query, and answer the query using the context provided by the relevant pages.

We have added all the basic infrastructure needed to add AI to the app. If you want to check the steps needed to implement the basic infrastructure for the app, see the [AI Package Infrastructure documentation](AIPackageInfrastructure.md).

The document used for this app is the IBM PC-DOS 1.0 Manual, which can be obtained from the [Internet Archive](https://archive.org/details/ibm-pc-dos-1.0-aug.-1981), but you can replace it with one that fits your own needs. It was processed to remove the index pages and leave only the content ones.

---

### **Step 2: Implement Search with Semantic Search**

#### 2.1 Initialize Semantic Search and OCR models

To use the search and OCR models, we need to initialize them. In `MainWindow.xaml.cs`, replace the code in the corresponding region with:

```csharp
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
```

The initialization of Semantic Search has the following steps:

1. The indexer is initialized with `AppContentIndexer.GetOrCreateIndex`, passing the index name we want to create.
2. We check the return from this function, checking the `Succeeded` property. If it's true, we can get the model instance in the property `Indexer`.
3. We wait for the model to be fully initialized with `await indexer.WaitForIndexCapabilitiesAsync()`

The code is run in a background task, because `AppContentIndexer.GetOrCreateIndex` is synchronous and may take a long time to finish, blocking the UI thread. To avoid blocking the app, you should run the code in the background task.

We must provide a unique name for the index we're creating, which we've defined as a constant in the class.

OCR initialization follows the patterns of the other Windows AI APIs:

1. Call the static method `GetReadyState()` to query the availability of the model: Ready, Not Ready (available but not downloaded), Not Available, or Disabled
2. If the status is not ready, call `EnsureReadyAsync()` to download the model and make it available
3. Create the model instance

The model instances are stored in class fields and can be used later.

These methods are called in the `Loaded` event for the Window, which was already configured initially.

The code will initialize the indexer and recognizer, then enable the buttons at the end. When you run the app, you should see "Getting indexer", "Getting OCR", then "Ready", with the buttons enabled.

> **Note**: The first time you run the application, the AI models may need to be downloaded, which can take several minutes depending on your internet connection.

#### 2.2 Loading and Indexing the PDF

To load and index the PDF, we use this procedure:

- Read the PDF and convert the file to bitmap images using the **Windows.Data.Pdf.PdfDocument** class
- Use the OCR model to convert the images to text and extract each page's text
- Index the retrieved text for use in Semantic Search
- Display the pages in the _Relevant Pages_ section when a query is made and relevant pages are retrieved

Replace the code in the `Load And Index Pdf` region with:

```csharp
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
            for (var i = 1u; i <= document.PageCount; i++)
            {
                ShowStatusMessage($"Processing Page {i}...");
                var imageBuffer = await GetImageFromPdf(i);
                if (imageBuffer == null)
                {
                    continue;
                }
                
                var page = imageBuffer.CopyToSoftwareBitmap();
                string contentId = $"Page{i}";
                var imageContent = AppManagedIndexableAppContent.CreateFromBitmap(contentId, page);
                indexer.AddOrUpdate(imageContent);
            }
            indexer.AddOrUpdate(AppManagedIndexableAppContent.CreateFromString(marker, marker));
            ShowStatusMessage($"Indexing complete.");
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
```

> **Note**: You should see red squiggles under `Path` and `File`. Click on them and press `Ctrl+.` to add `System.IO` to the usings in the file.

Once the PDF is loaded, it's indexed — we are indexing the pages directly - AppContentIndexer can retrieve pages by doing semantic search in the text contained in the images. The pages are then added to the indexer database. This is done in the `IndexPdf()` method.

The code reads the page, converts it into a BitmapImage and creates a new `AppManagedIndexableAppContent` with a `ContentId` set to `PageXX`, where `XX` is the page number, adding the page image as content. When querying relevant pages from the database, Semantic Search won't return the text or the image — it will only return the `ContentId` and the region where it found the text, so we need a way to "rehydrate" the `ContentId` into text. 

At the end of processing, the code calls `AddOrUpdate()` to update the index with the new text. This code should be run in a background thread to avoid to block the UI thread.

You may have noticed that after all pages are processed, we add an extra content with `indexer.AddOrUpdate(AppManagedIndexableAppContent.CreateFromString(marker, marker));`. This is a marker to indicate that the content is already indexed.

The marker is used in the `IsPdfIndexAvailable()` method that checks if the PDF was already indexed. The code creates a query asking for the marker. If it's retrieved, then the file is indexed and we don't need to reindex it. We use this code to check if the index exists and index the PDF if it's not already indexed, in the `LoadAndIndexPdf()` method.

Running the project, you will see "Getting indexer", then "Loading PDF", "Processing Page xxx", and "Adding content to index". The processing and adding content to index will only be called the first time.

#### 2.3 Adding Search capability to the app

To add search capability to the app, we need to process the Search button event handler. Replace the `Search and Show PDF Pages` section with this code:

```csharp
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
```

We create a query with the search term, get the 5 most relevant matches, and retrieve the page numbers from the ContentIds. Then, we get the page images using the `GetImageFromPdf()` method, which we defined in the indexing section, that retrieves the page and converts it to a `BitmapImage`. The code loads the page as a stream and creates the `BitmapImage` from this stream.

When you run this code, you can ask a question and get the relevant page images when you click the `Search` button.

![Search Results](Images/ContosoLab2.png)

---

### **Step 3: Implement RAG with Search and Phi-Silica**

#### 3.1 Initialize the Language Model

The pattern to initialize the Language Model is the same as we've seen for the OCR. Replace the code in the `Initialize Language Model` region with:

```csharp
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
```

#### 3.2 Querying the model

With the Language Model initialized, we can generate answers using a system prompt. Replace the code in the `Ask Button and Generate Response` region with this code:

```csharp
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

    string systemPrompt = $"""
    You are a knowledgeable assistant specialized in answering questions based solely on information from the context below, between ###. 
    When responding, focus on delivering clear, accurate answers drawn only from the context, avoiding outside information or assumptions.

    ###
    {sb}
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
```

When the user clicks the `Ask AI` button, the code gets the 5 most relevant page matches and uses the pages' text, retrieved using the OCR model. Then, it creates a system prompt using the pages' text as context. Finally, it calls `GenerateResponse()` to generate the response.

`GenerateResponse()` calls `languageModel.GenerateResponseAsync()`, passing the system prompt and user prompt. The `Progress` handler displays the tokens in the output text block as they are emitted by the model. The user can cancel the operation at any time by pressing the `Stop` button. When the operation is canceled, an `OperationCanceledException` is thrown.

When running the program, you can click the `Ask AI` button and the program will answer the query using the text that comes from the PDF file.

Now the program is complete! You can run it, ask a question, and click the `Search` button—it will perform a semantic search and show the relevant page images. When you click `Ask AI`, the program will perform a semantic search, get the text for the relevant pages, add it to the system prompt as context, and submit the query to the language model.

![Final Application](Images/ContosoLab3.png)

With this, you can add AI to your program. You can use any of the other models available in the Windows AI APIs in the same way you did here. As you can see, we have combined three different models—Semantic Search, OCR, and Phi-Silica—into an easy-to-use application. You can easily change the document used to modify the app to your needs: if you change it to a support document, you can use it as a troubleshooter, for example.

If you have any issues following the lab, you can see the complete project in the `Final` folder.

---

## 🛠️ Troubleshooting

### Common Issues

- **"PDF file not found"**: Ensure the PDF file is in the same directory as the executable
- **Models not downloading**: Check your internet connection and ensure Developer Mode is enabled
- **Slow performance**: The first run will be slower due to model downloads and PDF indexing
- **OCR accuracy issues**: Ensure the PDF has clear, readable text for best results

---

## 🏫 Learning Resources

- **Microsoft AI Documentation**: https://docs.microsoft.com/en-us/windows/ai/
- **Windows App SDK**: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/
- **Windows AI APIs**: https://docs.microsoft.com/en-us/windows/ai/apis/

---

## 🎉 Congratulations!

You've successfully built a sophisticated AI-powered document search assistant! This application demonstrates:

- ✅ **Modern UI Design** with custom styling and animations
- ✅ **Microsoft AI Integration** using the Windows AI APIs
- ✅ **Advanced PDF Processing** with text and image extraction
- ✅ **Natural Language Queries** with contextual AI responses

Your application now provides an intelligent interface for searching and querying documents using cutting-edge AI technology!
