using InvoiceBilling.Infrastructure.PdfTemplates;
using PdfSharp.Fonts;

namespace InvoiceBilling.Api.Pdf;

/// <summary>
/// Thin wrapper — delegates to the shared Infrastructure implementation.
/// </summary>
internal sealed class FailsafeFontResolver : IFontResolver
{
    private readonly Infrastructure.PdfTemplates.FailsafeFontResolver _inner = new();

    public string DefaultFontName => _inner.DefaultFontName;

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        => _inner.ResolveTypeface(familyName, isBold, isItalic);

    public byte[] GetFont(string faceName)
        => _inner.GetFont(faceName);
}
