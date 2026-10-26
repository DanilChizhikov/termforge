using DTech.Core.Files;

namespace DTech.Core.Transfers.Execution;

public sealed record RemotePlanContext(IRemoteFileClient Client, Func<string, string, string> CombineTarget);
