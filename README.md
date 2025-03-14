# ImgHarvest

A messy but functional web scraper that downloads images from a given website. It crawls pages, grabs images, and saves them in folders. Can also crawl external links (if enabled) and avoid downloading duplicate images.

## Features
- Scrapes images from a website and saves them.
- Can process multiple URLs from a file.
- Avoids downloading duplicate images.
- Can optionally crawl external links (disabled by default).
- Runs from the command line and asks for missing arguments.

## How to Use
Run it from the command line. It takes arguments but will prompt for any missing ones:
```
ImgHarvest [-u=<website URL>] [-d=<directory path>] [-f=<file path>] [-e] [-ndc]
```

### Options:
- `-u=<website URL>` → Website to scrape (if not provided, will prompt).
- `-d=<directory>` → Where to save images (defaults to `C:\ImgHarvest`, if not provided, will prompt).
- `-f=<file path>` → File with list of URLs.
- `-e` → Crawl external links too.
- `-ndc` → No deduplication (download everything, even duplicates).

## Example Usage
```
ImgHarvest -u=https://example.com -d=D:\MyImages -e
```
This will scrape `example.com`, save images to `D:\MyImages`, and also grab images from external links.

If you run it without any arguments, it will prompt you to enter the necessary information.

## Requirements
- .NET 8.0
- HtmlAgilityPack (used for parsing HTML)

