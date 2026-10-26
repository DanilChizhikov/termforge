namespace DTech.Core.Files.Search;

public sealed record FileSearchQuery(string RootPath, FileSearchCriteria Criteria)
{
    public FileNamePattern Pattern => Criteria.Pattern;

    public FileSearchLimits Limits => Criteria.Limits;
}
