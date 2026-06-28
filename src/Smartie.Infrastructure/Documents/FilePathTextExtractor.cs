using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace Smartie.Infrastructure.Documents;

/// <summary>Shared path-based text extraction used by document and chat attachment pipelines.</summary>
internal static class FilePathTextExtractor
{
    public static Task<string> ReadTextFileAsync(string absolutePath, CancellationToken cancellationToken = default) =>
        File.ReadAllTextAsync(absolutePath, Encoding.UTF8, cancellationToken);

    public static string ExtractPdfText(string absolutePath)
    {
        var builder = new StringBuilder();
        using var document = PdfDocument.Open(absolutePath);
        foreach (var page in document.GetPages())
        {
            var text = page.Text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.AppendLine(text);
            }
        }

        return builder.ToString().Trim();
    }

    public static string ExtractDocxText(string absolutePath)
    {
        var builder = new StringBuilder();
        using var wordDocument = WordprocessingDocument.Open(absolutePath, false);
        var body = wordDocument.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return string.Empty;
        }

        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var text = paragraph.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.AppendLine(text);
            }
        }

        return builder.ToString().Trim();
    }
}
