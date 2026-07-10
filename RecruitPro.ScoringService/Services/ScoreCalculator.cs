namespace RecruitPro.ScoringService.Services;

// Pure, deterministic scoring. Kept separate from the gRPC plumbing so it is trivially testable
// (see SelfTest, run via `dotnet run --selftest`).
public static class ScoreCalculator
{
    public static (double Score, string Label, List<string> Matched, List<string> Missing) Compute(
        IEnumerable<string> candidateSkills, IEnumerable<string> requiredSkills, int candidateExp, int requiredExp)
    {
        var cand = candidateSkills.Select(Norm).Where(s => s.Length > 0).ToHashSet();
        var required = requiredSkills.Select(Norm).Where(s => s.Length > 0).Distinct().ToList();

        var matched = required.Where(cand.Contains).ToList();
        var missing = required.Where(s => !cand.Contains(s)).ToList();

        double skillScore = required.Count == 0 ? 60.0 : (double)matched.Count / required.Count * 60.0;
        double expScore = requiredExp <= 0 ? 40.0 : Math.Min(1.0, (double)candidateExp / requiredExp) * 40.0;
        double total = Math.Round(skillScore + expScore, 1);
        string label = total >= 75 ? "Strong" : total >= 50 ? "Moderate" : "Weak";
        return (total, label, matched, missing);
    }

    private static string Norm(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();

    // ponytail: one runnable check for the scoring branch/loop. `dotnet run --selftest`.
    public static void SelfTest()
    {
        // 2 of 4 required skills matched, exp 3/2 (capped at 1.0):
        // skill = 2/4*60 = 30 ; exp = min(1,3/2)*40 = 40 ; total = 70 -> Moderate
        var (score, label, matched, missing) = Compute(
            new[] { "c#", "sql", "docker" }, new[] { "C#", "SQL", "Azure", "Kafka" }, 3, 2);
        if (Math.Abs(score - 70.0) > 0.001) throw new Exception($"score expected 70 got {score}");
        if (label != "Moderate") throw new Exception($"label expected Moderate got {label}");
        if (matched.Count != 2 || missing.Count != 2) throw new Exception("matched/missing mismatch");
    }
}
