namespace RecruitPro.Application.Interfaces;

public interface IApplicationSemanticScoringService
{
    Task ProcessAsync(Guid applicationId, CancellationToken cancellationToken = default);
}
