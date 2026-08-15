# ScanBot

[![.NET](https://github.com/shkim90/ScanBot/actions/workflows/dotnet.yml/badge.svg)](https://github.com/shkim90/ScanBot/actions/workflows/dotnet.yml)

ScanBot automates end-to-end digitization of industrial NDT (non-destructive testing) film:
it drives a film digitizer/scanner, reads the identifying tags engraved on each film via OCR,
converts the result to DICOM, and sends it to a PACS server - with a Blazor web UI for
configuration, review, and manual correction.

See the **[Wiki](https://github.com/shkim90/ScanBot/wiki)** for the full pipeline walkthrough,
OCR engine/template configuration, and settings reference.

## Pipeline

```
Digitizer/scanner (Vidar or Mt hardware, or an imported image file)
        -> OcrService.FindTags       - reads engraved tags off the film via OCR
        -> StoreService              - saves the image, tracks it in the local DB
        -> ImageTemplate film-type match
        -> DicomService              - builds a DICOM file from the matched tags
        -> StoreService.SendFile     - sends to the configured PACS server
```

Each stage is a separate `Services/*.cs` class wired up in `Startup.cs`; `BotService` is the
top-level orchestrator that reacts to the scanner's `FilmScanned` event.

## OCR

`OcrService` calls one of four interchangeable `IOcrEngine` implementations (selected by
`Settings.Ocr.Engine`), then merges the returned character boxes into whole tag values and
matches them against `ImageTemplate.yml`'s patterns. See the Wiki's **OCR pipeline** and
**ImageTemplate.yml** pages for how tag patterns, merge distance, and locking work together.

## Installation

Open https://dotnet.microsoft.com/en-us/download/dotnet/6.0, download and install two packages:
* ASP.NET Core Runtime
* .NET Desktop Runtime

Copy the latest ScanBot release to a folder and run `ScanBot`.

## Configuration

On first run, ScanBot writes a `Settings.json` next to the executable with default values; the
web UI's **Config** page (Scan / OCR / Store / Control tabs) edits it live. See the Wiki's
**Settings reference** page for what each field does.
