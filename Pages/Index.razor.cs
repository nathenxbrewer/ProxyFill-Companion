using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;
using Newtonsoft.Json;
using ProxyFill.Model;

namespace ProxyFill_Companion.Pages;

public partial class Index
{
    public List<string> FailedDownloads { get; set; }
    static string CacheDir = FileSystem.Current.CacheDirectory;
    private static string ImagesPath = Path.Combine(CacheDir,"ProxyFill", "Images");
    private static string OutputPath = Path.Combine(CacheDir,"ProxyFill", "Output");
    
    async Task OnClicked()
    {
        var deckPath = "/Users/nathenbrewer/Downloads/testdeck.json";
        var listString = File.ReadAllText(deckPath);
        var cardList = JsonConvert.DeserializeObject<List<ProxyCardDTO>>(listString);

        await DownloadImages(cardList);
    }
    
    async Task DownloadImages(IEnumerable<ProxyCardDTO> cards)
    {
        Console.WriteLine("Downloading front images...");
        if (!Directory.Exists(ImagesPath))
        {
            Directory.CreateDirectory(ImagesPath);
        }
    
        //group by imageURL for duplicates, they may have multiple cards with different proxy art, so we cant group by card. 
        var groupedList = cards.GroupBy(x => x.FrontImage).ToList();
        var imageCount = groupedList.Count;
        var x = 0;

        foreach (var group in groupedList)
        {
            var card = group.Select(x => x).First();
            var filename = $"{card.Name} {card.SetCode} {card.Number} ({GetGoogleDriveId(card.FrontImage)}).png";
            var path = Path.Combine(ImagesPath, filename);

            if (File.Exists(path))
            {
                Console.WriteLine($"{filename} already exists, continuing...");
                continue;
            }

            var downloadLink = group.Key;
            if (!await DownloadDriveFile(downloadLink, path))
            {
                FailedDownloads.Add(filename);
            }

            //ConsoleUtility.ProgressBar(x,imageCount);
        }
        Console.WriteLine("Front images downloaded!");
    }
    
    static string GetGoogleDriveId(string driveURL)
    {
        return driveURL.Replace(@"https://drive.google.com/uc?export=download&id=", "");
    }

    async Task<bool> DownloadDriveFile(string downloadLink, string path)
    {
        if (downloadLink.Contains("https://drive.google.com/uc?"))
        {
            var fileId = downloadLink.Substring(47);
            downloadLink =
                $"https://content.googleapis.com/drive/v2/files/{fileId}?key=AIzaSyBOGAtxTDZMJas_EkIRb0pVBpyQYpTaHXU&alt=media&source=downloadUrl";
        }

        using var client = new HttpClient();
        using var result = await client.GetAsync(downloadLink);
        if (!result.IsSuccessStatusCode) return false;
        var imageData = await result.Content.ReadAsByteArrayAsync();
        File.WriteAllBytes(path, imageData);

        //validate image is there
        if (!File.Exists(path)) return false;
        Console.WriteLine($"Downloaded {Path.GetFileName(path)} successfully!");

        var stream = new MemoryStream();
        await result.Content.LoadIntoBufferAsync();
        await result.Content.CopyToAsync(stream);
        await SaveFile(stream, path);
        return true;
    }
    
    async Task SaveFile(MemoryStream stream, string path)
    {
        //var fileSaverResult = await FileSaver.Default.SaveAsync("test.txt", stream, cancellationToken);
        var fileSaverResult = await FileSaver.Default.SaveAsync(path, stream, new CancellationToken());
        if (fileSaverResult.IsSuccessful)
        {
            await Toast.Make($"The file was saved successfully to location: {fileSaverResult.FilePath}").Show();
        }
        else
        {
            await Toast.Make($"The file was not saved successfully with error: {fileSaverResult.Exception.Message}").Show();
        }
    }
}