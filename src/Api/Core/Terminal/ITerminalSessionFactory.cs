using DTech.Core.Connections;

namespace DTech.Core.Terminal;

public interface ITerminalSessionFactory
{
    Task<SshResult<ITerminalSession>> OpenAsync(TerminalSessionRequest request, CancellationToken cancellationToken);
}
