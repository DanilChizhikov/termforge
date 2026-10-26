using DTech.Core.Connections;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace DTech.Ssh.IntegrationTests.Harness;

internal static class SshTransportTestServices
{
    public static ServiceProvider Create(IHostKeyVerifier hostKeyVerifier)
    {
        return Create(hostKeyVerifier, static _ => { });
    }

    public static ServiceProvider Create(IHostKeyVerifier hostKeyVerifier, Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton(hostKeyVerifier);
        services.AddSshTransport();
        configure(services);
        return services.BuildServiceProvider();
    }

    public static IHostKeyVerifier CreatePinnedVerifier(params HostKey[] trustedHostKeys)
    {
        var verifier = Substitute.For<IHostKeyVerifier>();
        verifier.Verify(Arg.Any<HostKeyVerificationRequest>()).Returns(call =>
        {
            var request = call.Arg<HostKeyVerificationRequest>();
            return Decide(request, trustedHostKeys);
        });
        return verifier;
    }

    public static IHostKeyVerifier CreateRejectingVerifier()
    {
        var verifier = Substitute.For<IHostKeyVerifier>();
        var unknown = new HostKeyVerification(HostKeyStatus.Unknown, []);
        verifier.Verify(Arg.Any<HostKeyVerificationRequest>()).Returns(unknown);
        return verifier;
    }

    private static HostKeyVerification Decide(HostKeyVerificationRequest request, IReadOnlyList<HostKey> trustedHostKeys)
    {
        for (var i = 0; i < trustedHostKeys.Count; i++)
        {
            var trustedHostKey = trustedHostKeys[i];
            if (trustedHostKey == request.HostKey)
            {
                return new HostKeyVerification(HostKeyStatus.Trusted, trustedHostKeys);
            }
        }

        return new HostKeyVerification(HostKeyStatus.Unknown, trustedHostKeys);
    }
}
