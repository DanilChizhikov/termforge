using DTech.Core.Files;
using DTech.Core.Files.Search;
using FluentAssertions;

namespace DTech.Core.Tests.Files.Search;

[TestClass]
public sealed class FileSearchCriteriaParserTests
{
    [TestMethod]
    public void Parse_WithoutLimits_UsesDefaults()
    {
        var request = new FileSearchRequest("/srv", "log");

        var parsed = FileSearchCriteriaParser.Parse(request);

        parsed.IsSuccess.Should().BeTrue(parsed.Error?.Message);
        parsed.Value.Limits.MaxDepth.Should().Be(FileSearchLimits.DefaultMaxDepth);
        parsed.Value.Limits.MaxResults.Should().Be(FileSearchLimits.DefaultMaxResults);
        parsed.Value.Limits.TimeLimit.Should().Be(TimeSpan.FromSeconds(FileSearchLimits.DefaultTimeLimitSeconds));
    }

    [TestMethod]
    public void Parse_WithLimits_UsesThem()
    {
        var request = new FileSearchRequest("/srv", "log") { MaxDepth = 3, MaxResults = 10, TimeLimitSeconds = 5 };

        var parsed = FileSearchCriteriaParser.Parse(request);

        parsed.Value.Limits.Should().Be(new FileSearchLimits(3, 10, TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    [DataRow(0, 10, 5)]
    [DataRow(FileSearchLimits.UpperMaxDepth + 1, 10, 5)]
    [DataRow(3, 0, 5)]
    [DataRow(3, FileSearchLimits.UpperMaxResults + 1, 5)]
    [DataRow(3, 10, 0)]
    [DataRow(3, 10, FileSearchLimits.UpperTimeLimitSeconds + 1)]
    public void Parse_OutOfRangeLimit_IsRejected(int maxDepth, int maxResults, int timeLimitSeconds)
    {
        var request = new FileSearchRequest("/srv", "log") { MaxDepth = maxDepth, MaxResults = maxResults, TimeLimitSeconds = timeLimitSeconds };

        var parsed = FileSearchCriteriaParser.Parse(request);

        parsed.Error!.Code.Should().Be(FileErrorCode.Invalid);
    }
}
