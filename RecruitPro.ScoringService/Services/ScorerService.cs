using Grpc.Core;
using RecruitPro.ScoringService.Grpc;

namespace RecruitPro.ScoringService.Services;

// gRPC endpoint implementing the Scorer service defined in Protos/scoring.proto.
public class ScorerService : Scorer.ScorerBase
{
    public override Task<ScoreReply> ScoreCandidate(ScoreRequest request, ServerCallContext context)
    {
        var (score, label, matched, missing) = ScoreCalculator.Compute(
            request.CandidateSkills,
            request.RequiredSkills,
            request.CandidateExperienceYears,
            request.RequiredExperienceYears);

        var reply = new ScoreReply { Score = score, Label = label };
        reply.MatchedSkills.AddRange(matched);
        reply.MissingSkills.AddRange(missing);
        return Task.FromResult(reply);
    }
}
