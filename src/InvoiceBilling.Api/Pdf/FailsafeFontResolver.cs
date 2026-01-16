using System;
using System.Collections.Generic;
using System.IO;
using PdfSharp.Fonts;

namespace InvoiceBilling.Api.Pdf;

/// <summary>
/// File-based font resolver for PDFsharp Core builds.
/// Tries DejaVu Sans (Linux/Docker) then Arial (Windows).
/// </summary>
internal sealed class FailsafeFontResolver : IFontResolver
{
    // Map faceName -> font file path (so GetFont can return bytes)
    private static readonly Dictionary<string, string> FaceToFile = new(StringComparer.OrdinalIgnoreCase);

    public string DefaultFontName => "DejaVu Sans";

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var requested = string.IsNullOrWhiteSpace(familyName) ? DefaultFontName : familyName.Trim();

        // We map many requests to a small known set of fonts.
        // Prefer DejaVu Sans (common in Linux). Fall back to Arial (Windows).
        var candidates = BuildCandidateFileNames(isBold, isItalic);

        var fontFile = FindFirstExistingFontFile(candidates);
        if (fontFile is null)
        {
            throw new InvalidOperationException(
                "PDFsharp could not resolve a usable font. " +
                "Install fonts (e.g., DejaVu/Arial) or set INVOICEBILLING_PDF_FONT_DIR to a folder containing TTF files.");
        }

        // Use file path as a stable face key.
        var faceName = fontFile;

        // Cache to allow GetFont to read bytes
        FaceToFile[faceName] = fontFile;

        return new FontResolverInfo(faceName);
    }

    public byte[] GetFont(string faceName)
    {
        if (!FaceToFile.TryGetValue(faceName, out var file))
        {
            // In practice faceName is the file path (set above).
            file = faceName;
        }

        return File.ReadAllBytes(file);
    }

    private static string[] BuildCandidateFileNames(bool isBold, bool isItalic)
    {
        // DejaVu (common on Linux)
        if (isBold && isItalic) return new[] { "DejaVuSans-BoldOblique.ttf", "DejaVuSans-BoldItalic.ttf", "arialbi.ttf" };
        if (isBold) return new[] { "DejaVuSans-Bold.ttf", "arialbd.ttf" };
        if (isItalic) return new[] { "DejaVuSans-Oblique.ttf", "DejaVuSans-Italic.ttf", "ariali.ttf" };

        return new[] { "DejaVuSans.ttf", "arial.ttf" };
    }

    private static string? FindFirstExistingFontFile(IEnumerable<string> fileNames)
    {
        foreach (var dir in EnumerateFontDirs())
        {
            foreach (var name in fileNames)
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path))
                    return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateFontDirs()
    {
        // 1) User override: point to a folder containing TTF files
        var overrideDir = Environment.GetEnvironmentVariable("INVOICEBILLING_PDF_FONT_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDir) && Directory.Exists(overrideDir))
            yield return overrideDir;

        // 2) Windows fonts (no yield inside try/catch)
        string? winFonts = null;
        try
        {
            winFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        }
        catch
        {
            winFonts = null;
        }

        if (!string.IsNullOrWhiteSpace(winFonts) && Directory.Exists(winFonts))
            yield return winFonts;

        // 3) Common Linux font dirs
        var linuxDirs = new[]
        {
            "/usr/share/fonts",
            "/usr/share/fonts/truetype",
            "/usr/share/fonts/truetype/dejavu",
            "/usr/local/share/fonts",
            "/usr/share/fonts/dejavu",
        };

        foreach (var d in linuxDirs)
        {
            if (Directory.Exists(d))
                yield return d;
        }
    }
}
