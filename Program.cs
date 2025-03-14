using System;
using HtmlAgilityPack;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ImgHarvestApp
{
    internal class ImgHarvest
    {
        static readonly HttpClient client = new HttpClient();
        static string baseDir;
        static string version = "1.0";
        static HashSet<string> visitedUrls = new HashSet<string>();
        static HashSet<string> downloadedImageUrls = new HashSet<string>(); // Track downloaded image URLs
        static string startDomain;
        static bool crawlExternal = false;
        static bool deduplicationEnabled = true; // Deduplication enabled by default
        static HashSet<string> failedImageDownloads = new HashSet<string>();
        static int totalImagesDownloaded = 0;
        static int totalFailedDownloads = 0;
        static int totalUrlsCrawled = 0;
        static Stopwatch stopwatch = new Stopwatch();

        static async Task Main(string[] args)
        {
            if (args.Length == 1 && args[0].ToLower() == "-h")
            {
                ShowUsage();
                return;
            }

            DisplayHeader();

            string startUrl = null;
            string filePath = null;

            foreach (var arg in args)
            {
                if (arg.StartsWith("-d=")) baseDir = arg.Substring(3);
                else if (arg.StartsWith("-u=")) startUrl = arg.Substring(3);
                else if (arg.StartsWith("-f=")) filePath = arg.Substring(3);
                else if (arg.StartsWith("-e")) crawlExternal = true;
                else if (arg.StartsWith("-ndc")) deduplicationEnabled = false; // Disable deduplication if -no-dedup is passed
            }

            PromptBaseDirectory();

            if (string.IsNullOrWhiteSpace(startUrl))
            {
                Console.Write("Enter the website URL (e.g., https://website.com): ");
                startUrl = Console.ReadLine();
            }

            startDomain = new Uri(startUrl).Host;

            PromptCrawlExternalUrls();
            PromptDeduplication();

            stopwatch.Start();

            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                var urls = File.ReadAllLines(filePath);
                foreach (var url in urls)
                {
                    await CrawlPage(url, baseDir);
                }
            }
            else
            {
                await CrawlPage(startUrl, baseDir);
            }

            stopwatch.Stop();

            DisplaySummary();
        }

        static void DisplayHeader()
        {
            Console.WriteLine(@"  _____                                                 _   ");
            Console.WriteLine(@"  \_   \_ __ ___   __ _  /\  /\__ _ _ ____   _____  ___| |_ ");
            Console.WriteLine(@"   / /\/ '_ ` _ \ / _` |/ /_/ / _` | '__\ \ / / _ \/ __| __|");
            Console.WriteLine(@"/\/ /_ | | | | | | (_| / __  / (_| | |   \ V /  __/\__ \ |_ ");
            Console.WriteLine(@"\____/ |_| |_| |_|\__, \/ /_/ \__,_|_|    \_/ \___||___/\__|");
            Console.WriteLine(@"                  |___/                                     ");
            Console.WriteLine();
            Console.WriteLine($"    ImgHarvest {version} by .pwl");
            Console.WriteLine(@"    https://github.com/dotPawel");
            Console.WriteLine();
        }

        static void ShowUsage()
        {
            Console.WriteLine("Usage: ImgHarvest [-u=<website URL>] [-d=<directory path>] [-f=<file path>] [-e] [-no-dedup]");
            Console.WriteLine("  -u=<website URL>  The URL of the website to crawl.");
            Console.WriteLine("  -d=<directory>    The directory to save images (default: C:\\ImgHarvest).");
            Console.WriteLine("  -f=<file path>    The path to a text file containing a list of URLs to crawl. Each URL should be on a new line.");
            Console.WriteLine("  -e                Enable crawling of external URLs.");
            Console.WriteLine("  -ndc              Disable deduplication of downloaded images.");
            Console.WriteLine("  -h                Show this help message.");
        }

        static void PromptBaseDirectory()
        {
            if (string.IsNullOrWhiteSpace(baseDir))
            {
                Console.Write("Enter the directory to save images (default: C:\\ImgHarvest): ");
                baseDir = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(baseDir))
                {
                    baseDir = "C:\\ImgHarvest";
                }
            }

            Directory.CreateDirectory(baseDir);
        }

        static void PromptCrawlExternalUrls()
        {
            Console.Write("Do you want to crawl external URLs? (y/n, default: n): ");
            var externalChoice = Console.ReadLine()?.ToLower();
            crawlExternal = externalChoice == "y";
        }

        static void PromptDeduplication()
        {
            Console.Write("Enable deduplication to avoid duplicate image downloads? (y/n, default: y): ");
            var dedupChoice = Console.ReadLine()?.ToLower();
            deduplicationEnabled = dedupChoice != "n";
        }

        static async Task CrawlPage(string url, string baseDir)
        {
            if (visitedUrls.Contains(url))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SKIP] Already visited: {url}");
                return;
            }

            // Skip non-HTML (non-image) content types
            if (url.EndsWith(".mp4") || url.EndsWith(".avi") || url.EndsWith(".mov") ||
                url.EndsWith(".pdf") || url.EndsWith(".zip") || url.EndsWith(".exe"))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SKIP] Non-HTML content detected: {url}");
                return;
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [INFO] Crawling: {url}");
            visitedUrls.Add(url);
            totalUrlsCrawled++;

            try
            {
                // Download content from the URL
                var htmlContent = await client.GetStringAsync(url);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlContent);

                await DownloadImagesFromPage(htmlDoc, url, baseDir);
                await CrawlLinksFromPage(htmlDoc, url, baseDir);

                if (url.EndsWith(".jpg") || url.EndsWith(".png") || url.EndsWith(".gif"))
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [INFO] Found direct image URL: {url}");
                    await DownloadImage(url, baseDir, url);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] Failed to crawl {url}: {ex.Message}");
            }
        }
        static async Task DownloadImagesFromPage(HtmlDocument htmlDoc, string url, string baseDir)
        {
            var imgTags = htmlDoc.DocumentNode.SelectNodes("//img");
            if (imgTags != null)
            {
                foreach (var img in imgTags)
                {
                    var imgUrl = img.GetAttributeValue("src", null);
                    if (!string.IsNullOrEmpty(imgUrl))
                    {
                        string imageUrl = new Uri(new Uri(url), imgUrl).ToString();

                        if (deduplicationEnabled && downloadedImageUrls.Contains(imageUrl))
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SKIP] Duplicate image found: {imageUrl}");
                            continue;
                        }

                        await DownloadImage(imageUrl, baseDir, url);
                        if (deduplicationEnabled)
                        {
                            downloadedImageUrls.Add(imageUrl);
                        }
                    }
                }
            }
        }

        static async Task CrawlLinksFromPage(HtmlDocument htmlDoc, string url, string baseDir)
        {
            var linkTags = htmlDoc.DocumentNode.SelectNodes("//a[@href]");
            if (linkTags != null)
            {
                foreach (var link in linkTags)
                {
                    var linkUrl = link.GetAttributeValue("href", null);
                    if (!string.IsNullOrEmpty(linkUrl))
                    {
                        Uri resolvedUri = new Uri(new Uri(url), linkUrl);
                        string resolvedUrl = resolvedUri.ToString();

                        if (resolvedUri.IsAbsoluteUri && resolvedUri.Host != startDomain && !crawlExternal)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SKIP] External link found: {resolvedUrl} (Crawling external URLs is disabled)");
                            continue;
                        }

                        if (!visitedUrls.Contains(resolvedUrl))
                        {
                            await CrawlPage(resolvedUrl, baseDir);
                        }
                    }
                }
            }
        }

        static async Task DownloadImage(string imageUrl, string baseDir, string pageUrl)
        {
            string fileName = Path.GetFileName(new Uri(imageUrl).LocalPath);
            Uri pageUri = new Uri(pageUrl);
            string categoryDir = Path.Combine(baseDir, pageUri.Host + pageUri.AbsolutePath.Replace("/", "\\"));
            Directory.CreateDirectory(categoryDir);

            string filePath = Path.Combine(categoryDir, fileName);

            if (failedImageDownloads.Contains(imageUrl))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SKIP] {imageUrl} already failed 3 times, skipping...");
                return;
            }

            int retryCount = 0;

            while (retryCount < 3)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SKIP] {fileName} already exists, skipping...");
                        return;
                    }

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DOWNLOAD] {imageUrl}");
                    var imgBytes = await client.GetByteArrayAsync(imageUrl);
                    await File.WriteAllBytesAsync(filePath, imgBytes);

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SUCCESS] {fileName} saved to {filePath}");
                    totalImagesDownloaded++;
                    return;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] Failed to download {imageUrl} (Attempt {retryCount}): {ex.Message}");
                    if (retryCount >= 3)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SKIP] {imageUrl} failed 3 times, skipping...");
                        failedImageDownloads.Add(imageUrl);
                        totalFailedDownloads++;
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [INFO] Retrying... ({retryCount}/3)");
                    }
                }
            }
        }

        static void DisplaySummary()
        {
            Console.WriteLine();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SUMMARY] Total URLs crawled: {totalUrlsCrawled}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SUMMARY] Total images downloaded: {totalImagesDownloaded}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SUMMARY] Total failed downloads: {totalFailedDownloads}");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SUMMARY] Total time taken: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        }
    }
}
