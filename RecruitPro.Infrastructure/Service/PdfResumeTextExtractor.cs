using System.Text;
using RecruitPro.Application.Interfaces;
using UglyToad.PdfPig;

namespace RecruitPro.Infrastructure.Service;

public class PdfResumeTextExtractor : IResumeTextExtractor
{
    public Task<string> ExtractTextAsync(Stream resumeStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resumeStream);

        if (resumeStream.CanSeek)
        {
            resumeStream.Position = 0;
        }

        StringBuilder builder = new();
        using PdfDocument document = PdfDocument.Open(resumeStream);

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.AppendLine(page.Text);
        }

        return Task.FromResult(builder.ToString().Trim());
    }
}
