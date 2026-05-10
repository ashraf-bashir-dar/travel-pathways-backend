using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace TravelPathways.Api.Services;

/// <summary>
/// Strips common document-level PDF features that make Chrome/Edge show
/// "This file may be dangerous" / unsafe download prompts (OpenAction, catalog AA, JavaScript name trees, AcroForm scripts).
/// </summary>
public static class PackagePdfSanitizer
{
    /// <summary>Best-effort sanitizer; returns original bytes if the PDF cannot be opened for modify.</summary>
    public static byte[] StripDangerousCatalogEntries(byte[] pdfBytes)
    {
        if (pdfBytes.Length == 0) return pdfBytes;
        try
        {
            var buffer = pdfBytes.ToArray();
            using var input = new MemoryStream(buffer, 0, buffer.Length, writable: true, publiclyVisible: true);
            using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
            var catalog = doc.Internals.Catalog;

            catalog.Elements.Remove("/OpenAction");
            catalog.Elements.Remove("/AA");
            catalog.Elements.Remove("/JavaScript");
            catalog.Elements.Remove("/Names");
            catalog.Elements.Remove("/AcroForm");

            using var output = new MemoryStream();
            doc.Save(output, false);
            return output.ToArray();
        }
        catch
        {
            return pdfBytes;
        }
    }
}
