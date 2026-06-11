namespace RecruitPro.Application.Interfaces;

public interface IResumeTextExtractor
{
    Task<string> ExtractTextAsync(Stream resumeStream, CancellationToken cancellationToken = default);
}
