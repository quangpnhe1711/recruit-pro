using System.Collections.Concurrent;
using RecruitPro.Application.Interfaces;

namespace RecruitPro.Infrastructure.Service;

public class InMemoryEmbeddingCache : IEmbeddingCache
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<double>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string hash, out IReadOnlyList<double>? vector)
    {
        bool found = _cache.TryGetValue(hash, out IReadOnlyList<double>? cached);
        vector = cached;
        return found;
    }

    public void Set(string hash, IReadOnlyList<double> vector)
    {
        _cache[hash] = vector;
    }
}
