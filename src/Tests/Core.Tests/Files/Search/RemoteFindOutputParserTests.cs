using DTech.Core.Diagnostics;
using DTech.Core.Files;
using DTech.Core.Files.Search;
using FluentAssertions;

namespace DTech.Core.Tests.Files.Search;

[TestClass]
public sealed class RemoteFindOutputParserTests
{
    [TestMethod]
    public void Parse_RecordsWithKindMarkers_BecomeMatches()
    {
        var result = new RemoteCommandResult(0, "d/srv/logs\0f/srv/logs/app.log\0f/top.log\0", string.Empty);

        var report = RemoteFindOutputParser.Parse(result, 100);

        report.Outcome.Should().Be(FileSearchOutcome.Completed);
        report.Matches.Should().HaveCount(3);
        report.Matches[0].Entry.Kind.Should().Be(FileEntryKind.Directory);
        report.Matches[0].Entry.Name.Should().Be("logs");
        report.Matches[0].ParentPath.Should().Be("/srv");
        report.Matches[1].Entry.Kind.Should().Be(FileEntryKind.File);
        report.Matches[1].Entry.Path.Should().Be("/srv/logs/app.log");
        report.Matches[2].ParentPath.Should().Be("/");
    }

    [TestMethod]
    public void Parse_NameWithNewline_IsKeptIntact()
    {
        var result = new RemoteCommandResult(0, "f/srv/odd\nname.log\0", string.Empty);

        var report = RemoteFindOutputParser.Parse(result, 100);

        report.Matches.Should().ContainSingle().Which.Entry.Name.Should().Be("odd\nname.log");
    }

    [TestMethod]
    public void Parse_TruncatedOutput_DropsIncompleteRecordAndReportsResultLimit()
    {
        var result = new RemoteCommandResult(0, "f/srv/a.log\0f/srv/b.l", string.Empty) { OutputTruncated = true };

        var report = RemoteFindOutputParser.Parse(result, 100);

        report.Matches.Should().ContainSingle().Which.Entry.Path.Should().Be("/srv/a.log");
        report.Outcome.Should().Be(FileSearchOutcome.ResultLimitReached);
    }

    [TestMethod]
    public void Parse_MoreRecordsThanLimit_KeepsLimitAndReportsResultLimit()
    {
        var result = new RemoteCommandResult(0, "f/a\0f/b\0f/c\0", string.Empty);

        var report = RemoteFindOutputParser.Parse(result, 2);

        report.Matches.Should().HaveCount(2);
        report.Outcome.Should().Be(FileSearchOutcome.ResultLimitReached);
    }

    [TestMethod]
    public void Parse_KilledByTimer_ReportsTimeLimitWithCollectedMatches()
    {
        var result = new RemoteCommandResult(RemoteFindCommand.TimeLimitExitCode, "f/srv/a.log\0", string.Empty);

        var report = RemoteFindOutputParser.Parse(result, 100);

        report.Outcome.Should().Be(FileSearchOutcome.TimeLimitReached);
        report.Matches.Should().ContainSingle();
    }

    [TestMethod]
    public void Parse_PermissionDeniedLines_BecomeWarningsAndSearchIsNotFailed()
    {
        var standardError = "find: '/root': Permission denied\nfind: /var/lib/secret: Permission denied\nfind: ‘/etc/ssl/private’: Permission denied\n";
        var result = new RemoteCommandResult(1, "f/srv/a.log\0", standardError);

        var report = RemoteFindOutputParser.Parse(result, 100);

        report.Outcome.Should().Be(FileSearchOutcome.Completed);
        report.Warnings.Select(warning => warning.Path).Should().Equal("/root", "/var/lib/secret", "/etc/ssl/private");
        report.Warnings.Should().OnlyContain(warning => warning.Error.Code == FileErrorCode.PermissionDenied);
    }

    [TestMethod]
    public void Parse_UnknownFailure_ReportsFailedWithStandardError()
    {
        var result = new RemoteCommandResult(1, string.Empty, "find: unrecognized: -iname\n");

        var report = RemoteFindOutputParser.Parse(result, 100);

        report.Outcome.Should().Be(FileSearchOutcome.Failed);
        report.FailureMessage.Should().Contain("unrecognized: -iname");
    }

    [TestMethod]
    public void Parse_GarbageRecords_AreIgnored()
    {
        var result = new RemoteCommandResult(0, "x/srv/a\0frelative\0d/\0\0f/srv/ok\0", string.Empty);

        var report = RemoteFindOutputParser.Parse(result, 100);

        report.Matches.Should().ContainSingle().Which.Entry.Path.Should().Be("/srv/ok");
    }
}
