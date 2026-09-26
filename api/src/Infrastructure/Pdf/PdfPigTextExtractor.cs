using System.Text;
using FinanceFoodTracker.Application.Common.Interfaces;
using UglyToad.PdfPig;

namespace FinanceFoodTracker.Infrastructure.Pdf;

public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public string Extract(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        using var document = PdfDocument.Open(stream);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            builder.AppendLine($"--- page {page.Number} ---");

            var lines = page.GetWords()
                .GroupBy(word => Math.Round(word.BoundingBox.Bottom / 3.0))
                .OrderByDescending(group => group.Key);

            foreach (var group in lines)
            {
                var words = group.OrderBy(word => word.BoundingBox.Left).ToList();
                var line = new StringBuilder();
                double? previousRight = null;

                foreach (var word in words)
                {
                    if (previousRight is not null)
                    {
                        line.Append(word.BoundingBox.Left - previousRight > 6 ? "  |  " : " ");
                    }

                    line.Append(word.Text);
                    previousRight = word.BoundingBox.Right;
                }

                builder.AppendLine(line.ToString());
            }
        }

        return builder.ToString();
    }
}
