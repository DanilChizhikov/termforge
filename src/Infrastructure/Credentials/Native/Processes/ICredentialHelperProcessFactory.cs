using System.Diagnostics;

namespace DTech.Credentials.Native.Processes;

public interface ICredentialHelperProcessFactory
{
    Process Start();
}
