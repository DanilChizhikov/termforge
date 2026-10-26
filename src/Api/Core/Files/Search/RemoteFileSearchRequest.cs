namespace DTech.Core.Files.Search;

public sealed record RemoteFileSearchRequest(FileSearchRequest Search, bool UseRemoteFind);
