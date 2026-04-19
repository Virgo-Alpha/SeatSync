using System.Globalization;
using System.Text;

namespace SeatSync.Api.Utilities;

public static class SimplePdfBuilder
{
    public static byte[] BuildSinglePageReceipt(IReadOnlyList<string> lines)
    {
        const int left = 50;
        const int top = 760;
        const int lineHeight = 16;

        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("BT");
        contentBuilder.AppendLine("/F1 11 Tf");
        contentBuilder.AppendLine($"{left} {top} Td");
        contentBuilder.AppendLine($"{lineHeight} TL");

        for (var i = 0; i < lines.Count; i++)
        {
            var escaped = EscapeText(lines[i]);
            if (i > 0)
            {
                contentBuilder.AppendLine("T*");
            }

            contentBuilder.AppendLine($"({escaped}) Tj");
        }

        contentBuilder.AppendLine("ET");
        var contentStream = contentBuilder.ToString();
        var contentBytes = Encoding.ASCII.GetBytes(contentStream);

        var objects = new List<byte[]>
        {
            Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Encoding.ASCII.GetBytes("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>"),
            Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"),
            Encoding.ASCII.GetBytes($"<< /Length {contentBytes.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n{contentStream}endstream")
        };

        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.ASCII, leaveOpen: true);

        writer.Write("%PDF-1.4\n");
        writer.Flush();

        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(ms.Position);
            writer.Write($"{i + 1} 0 obj\n");
            writer.Flush();
            ms.Write(objects[i], 0, objects[i].Length);
            writer.Write("\nendobj\n");
            writer.Flush();
        }

        var xrefPosition = ms.Position;
        writer.Write($"xref\n0 {objects.Count + 1}\n");
        writer.Write("0000000000 65535 f \n");
        for (var i = 1; i < offsets.Count; i++)
        {
            writer.Write($"{offsets[i]:D10} 00000 n \n");
        }

        writer.Write("trailer\n");
        writer.Write($"<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
        writer.Write("startxref\n");
        writer.Write($"{xrefPosition}\n");
        writer.Write("%%EOF");
        writer.Flush();

        return ms.ToArray();
    }

    private static string EscapeText(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
}
