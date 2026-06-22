using RecruitPro.Application.Common;

namespace RecruitPro.Tests;

public sealed class ApplicationCommonTests
{
    [Fact]
    public void NormalizeOptionalText_ReturnsNull_ForNullOrWhitespace()
    {
        TextNormalizationHelper.NormalizeOptionalText(null).Should().BeNull();
        TextNormalizationHelper.NormalizeOptionalText(string.Empty).Should().BeNull();
        TextNormalizationHelper.NormalizeOptionalText("   ").Should().BeNull();
    }

    [Fact]
    public void NormalizeOptionalText_TrimsValue_WhenPresent()
    {
        TextNormalizationHelper.NormalizeOptionalText("  hello world  ").Should().Be("hello world");
    }

    [Fact]
    public void NormalizeOptionalText_RemovesNullBytes_WhenPresent()
    {
        TextNormalizationHelper.NormalizeOptionalText("  hello\0world  ").Should().Be("helloworld");
    }

    [Fact]
    public void RemoveInvalidDatabaseCharacters_StripsNullBytes()
    {
        TextNormalizationHelper.RemoveInvalidDatabaseCharacters("a\0b\0c").Should().Be("abc");
    }

    [Fact]
    public void ExtractDisplayFileName_ReturnsEmpty_ForBlankInput()
    {
        StoredFileNameHelper.ExtractDisplayFileName(null).Should().BeEmpty();
        StoredFileNameHelper.ExtractDisplayFileName(" ").Should().BeEmpty();
    }

    [Theory]
    [InlineData("123_resume.pdf", "resume.pdf")]
    [InlineData("resume.pdf", "resume.pdf")]
    [InlineData("https://cdn.example.com/files/456_cv.docx", "cv.docx")]
    public void ExtractDisplayFileName_ExtractsFriendlyName(string storedValue, string expected)
    {
        StoredFileNameHelper.ExtractDisplayFileName(storedValue).Should().Be(expected);
    }

    [Fact]
    public void BuildSalaryLabel_ReturnsNegotiable_WhenNoBoundsProvided()
    {
        CompensationLabelHelper.BuildSalaryLabel(null, null).Should().Be("Thương lượng");
    }

    [Fact]
    public void BuildSalaryLabel_ReturnsRange_WhenBothBoundsProvided()
    {
        CompensationLabelHelper.BuildSalaryLabel(1000000m, 2000000m).Should().Be("1,000,000 - 2,000,000 VNĐ");
    }

    [Fact]
    public void BuildSalaryLabel_ReturnsMinimumPlus_WhenOnlyMinimumProvided()
    {
        CompensationLabelHelper.BuildSalaryLabel(1500000m, null).Should().Be("1,500,000+ VNĐ");
    }

    [Fact]
    public void BuildSalaryLabel_ReturnsMaximum_WhenOnlyMaximumProvided()
    {
        CompensationLabelHelper.BuildSalaryLabel(null, 3000000m).Should().Be("Up to 3,000,000 VNĐ");
    }

    [Fact]
    public void BuildPaginationMeta_CalculatesTotalPages()
    {
        var meta = PaginationMetaBuilder.Build(page: 2, pageSize: 10, totalItems: 25);

        meta.Page.Should().Be(2);
        meta.PageSize.Should().Be(10);
        meta.TotalItems.Should().Be(25);
        meta.TotalPages.Should().Be(3);
    }

    [Fact]
    public void GenerateTemporaryPassword_UsesExpectedFormat()
    {
        string password = CredentialUtility.GenerateTemporaryPassword();

        password.Should().StartWith("Rp!");
        password.Should().HaveLength(12);
    }

    [Fact]
    public void DbDateTime_ProducesDatabaseFriendlyValues()
    {
        DbDateTime.Now.Kind.Should().Be(DateTimeKind.Unspecified);
        DbDateTime.Today.Kind.Should().Be(DateTimeKind.Unspecified);
        DbDateTime.Today.TimeOfDay.Should().Be(TimeSpan.Zero);
        DbDateTime.CurrentYear.Should().Be(DbDateTime.Now.Year);
    }
}
