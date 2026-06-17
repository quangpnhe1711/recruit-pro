using System.Text;
using RecruitPro.Application.Interfaces;
using UglyToad.PdfPig;

namespace RecruitPro.Infrastructure.Service;

public class PdfResumeTextExtractor : IResumeTextExtractor
{
    /// <summary>
    /// Extracts text.
    /// </summary>
    /// <param name="resumeStream">The <paramref name="resumeStream"/> value.</param>
    /// <param name="cancellationToken">The <paramref name="cancellationToken"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
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
