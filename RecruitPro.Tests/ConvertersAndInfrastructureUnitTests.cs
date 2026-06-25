using AutoMapper;
using RecruitPro.Application.Mappings;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data.Converters;
using RecruitPro.Infrastructure.Service;

namespace RecruitPro.Tests;

public sealed class ConvertersAndInfrastructureUnitTests
{
    [Fact]
    public void JsonStringListValueConverter_ReturnsEmptyList_ForBlankInput()
    {
        var converter = new JsonStringListValueConverter();

        converter.Convert(null, default!).Should().BeEmpty();
    }

    [Fact]
    public void JsonStringListValueConverter_ParsesJsonArray_AndRemovesBlankEntries()
    {
        var converter = new JsonStringListValueConverter();

        List<string> result = converter.Convert("[\"c#\", \"\", \"sql\"]", default!);

        result.Should().Equal("c#", "sql");
    }

    [Fact]
    public void JsonStringListValueConverter_FallsBackToOriginalString_WhenInputIsNotJson()
    {
        var converter = new JsonStringListValueConverter();

        List<string> result = converter.Convert("plain-text", default!);

        result.Should().Equal("plain-text");
    }

    [Theory]
    [InlineData("pending", ApplicationStatus.Applied)]
    [InlineData("under review", ApplicationStatus.Screening)]
    [InlineData("manager-review", ApplicationStatus.ManagerReview)]
    [InlineData("interview_scheduled", ApplicationStatus.Interview)]
    [InlineData("offer_sent", ApplicationStatus.Offer)]
    [InlineData("accepted", ApplicationStatus.Hired)]
    [InlineData("declined", ApplicationStatus.OfferDeclined)]
    [InlineData("withdrawn", ApplicationStatus.Withdrawn)]
    [InlineData("Withdrawn", ApplicationStatus.Withdrawn)]
    public void ApplicationStatusValueConverter_MapsLegacyStrings(string source, ApplicationStatus expected)
    {
        var converter = new ApplicationStatusValueConverter();

        ApplicationStatus converted = converter.ConvertFromProviderExpression.Compile().Invoke(source);

        converted.Should().Be(expected);
    }

    [Fact]
    public void ApplicationStatusValueConverter_ThrowsForUnknownValues()
    {
        var converter = new ApplicationStatusValueConverter();
        Action act = () => converter.ConvertFromProviderExpression.Compile().Invoke("mystery-status");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void InMemoryEmbeddingCache_IsCaseInsensitive()
    {
        var cache = new InMemoryEmbeddingCache();
        IReadOnlyList<double> vector = [0.1, 0.2];

        cache.Set("HASH-1", vector);
        bool found = cache.TryGet("hash-1", out IReadOnlyList<double>? cached);

        found.Should().BeTrue();
        cached.Should().BeSameAs(vector);
    }

    [Fact]
    public async Task InMemoryApplicationSemanticProcessingQueue_PreservesEnqueueOrder()
    {
        var queue = new InMemoryApplicationSemanticProcessingQueue();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();

        await queue.EnqueueAsync(first);
        await queue.EnqueueAsync(second);

        (await queue.DequeueAsync(CancellationToken.None)).Should().Be(first);
        (await queue.DequeueAsync(CancellationToken.None)).Should().Be(second);
    }
}
