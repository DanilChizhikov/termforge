using System.Globalization;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace DTech.Entrypoint.Security.Network;

internal sealed class PasswordLoginHandler
{
    private const string UnknownClient = "unknown";

    private readonly IOptions<NetworkAccessOptions> _options;
    private readonly LoginAttemptLimiter _attemptLimiter;
    private readonly NetworkPasswordVerifier _passwordVerifier;
    private readonly BrowserSessionStore _sessionStore;
    private readonly ILogger<PasswordLoginHandler> _logger;

    public PasswordLoginHandler(
        IOptions<NetworkAccessOptions> options,
        LoginAttemptLimiter attemptLimiter,
        NetworkPasswordVerifier passwordVerifier,
        BrowserSessionStore sessionStore,
        ILogger<PasswordLoginHandler> logger)
    {
        _options = options;
        _attemptLimiter = attemptLimiter;
        _passwordVerifier = passwordVerifier;
        _sessionStore = sessionStore;
        _logger = logger;
    }

    public PasswordLoginStatusResponse GetStatus(HttpContext context)
    {
        var isEnabled = _options.Value.Enabled;
        var session = context.Features.Get<BrowserSession>();
        var signedInWithPassword = session?.Kind == BrowserSessionKind.Password;
        return new PasswordLoginStatusResponse(isEnabled, signedInWithPassword);
    }

    public IResult Login(PasswordLoginRequest request, HttpContext context)
    {
        var networkAccess = _options.Value;
        if (!networkAccess.Enabled)
        {
            return Results.NotFound();
        }

        var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient;
        var decision = _attemptLimiter.TryBegin(clientKey);
        if (!decision.IsAllowed)
        {
            return RejectLockedOut(context, clientKey, decision);
        }

        if (!_passwordVerifier.Verify(request.Password))
        {
            _logger.LogWarning($"Rejected a password sign-in from {clientKey}: wrong password");
            return Results.Text("Wrong password", statusCode: StatusCodes.Status401Unauthorized);
        }

        _attemptLimiter.RecordSuccess(clientKey);
        var sessionLifetime = TimeSpan.FromHours(networkAccess.SessionLifetimeHours);
        var session = _sessionStore.Create(BrowserSessionKind.Password, sessionLifetime);
        SessionCookie.Append(context, session.SessionToken);
        _logger.LogInformation($"Password sign-in succeeded from {clientKey}");
        return Results.NoContent();
    }

    private IResult RejectLockedOut(HttpContext context, string clientKey, LoginAttemptDecision decision)
    {
        var retryAfterSeconds = (int)Math.Ceiling(decision.RetryAfter.TotalSeconds);
        context.Response.Headers[HeaderNames.RetryAfter] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        _logger.LogWarning($"Rejected a password sign-in from {clientKey}: locked out for {retryAfterSeconds} seconds after too many attempts");
        return Results.Text("Too many sign-in attempts. Try again later.", statusCode: StatusCodes.Status429TooManyRequests);
    }
}
