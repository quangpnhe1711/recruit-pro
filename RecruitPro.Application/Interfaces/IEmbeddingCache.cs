namespace RecruitPro.Application.Interfaces;

public interface IEmbeddingCache
{
    bool TryGet(string hash, out IReadOnlyList<double>? vector);
    void Set(string hash, IReadOnlyList<double> vector);
}
