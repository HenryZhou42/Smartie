using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Smartie.Tests;

internal static class TestDocumentFixtures
{
    private static readonly Lazy<string> TestDataRoot = new(() =>
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "TestData");
        return Path.GetFullPath(dir);
    });

    public static string GetTestDataPath(string fileName) =>
        Path.Combine(TestDataRoot.Value, fileName);

    public static void EnsureFixtures()
    {
        Directory.CreateDirectory(TestDataRoot.Value);
        EnsureResumeDocx();
        EnsureSamplePdf();
    }

    public static void EnsureResumeDocx()
    {
        var path = GetTestDataPath("Resume.docx");
        if (File.Exists(path))
        {
            return;
        }

        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document(new Body(
            new Paragraph(new Run(new Text("Alex Rivera"))),
            new Paragraph(new Run(new Text("Senior Software Developer"))),
            new Paragraph(new Run(new Text("Engineering Manager: Sarah Johnson"))),
            new Paragraph(new Run(new Text("15 vacation days for new employees."))),
            new Paragraph(new Run(new Text("20 vacation days after 3 years."))),
            new Paragraph(new Run(new Text("25 vacation days after 10 years.")))));
        mainPart.Document.Save();
    }

    public static void EnsureSamplePdf()
    {
        var path = GetTestDataPath("Sample.pdf");
        if (File.Exists(path))
        {
            return;
        }

        var pdf = """
            %PDF-1.4
            1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj
            2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj
            3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >> endobj
            4 0 obj << /Length 168 >> stream
            BT
            /F1 12 Tf
            72 720 Td
            (Vacation Policy Sample) Tj
            0 -18 Td
            (15 vacation days for first 3 years.) Tj
            0 -18 Td
            (20 vacation days after 3 years.) Tj
            0 -18 Td
            (25 vacation days after 10 years.) Tj
            0 -18 Td
            (Engineering Manager: Sarah Johnson) Tj
            ET
            endstream
            endobj
            5 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj
            xref
            0 6
            0000000000 65535 f 
            0000000009 00000 n 
            0000000058 00000 n 
            0000000115 00000 n 
            0000000261 00000 n 
            0000000482 00000 n 
            trailer << /Size 6 /Root 1 0 R >>
            startxref
            562
            %%EOF
            """;

        File.WriteAllText(path, pdf.Replace("\n", "\r\n"));
    }
}
