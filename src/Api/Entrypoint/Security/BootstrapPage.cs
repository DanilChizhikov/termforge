namespace DTech.Entrypoint.Security;

internal static class BootstrapPage
{
    public const string ContentSecurityPolicy = "default-src 'none'; script-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    public const string Html = """
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <title>TermForge</title>
        <script src="/bootstrap.js" defer></script>
        </head>
        <body>
        <p id="status">Opening TermForge...</p>
        </body>
        </html>
        """;

    public const string Script = """
        (async () => {
          const status = document.getElementById('status');
          const token = new URLSearchParams(window.location.hash.slice(1)).get('token');
          window.history.replaceState(null, '', window.location.pathname);
          if (!token) {
            const sessionResponse = await fetch('/api/session', { credentials: 'same-origin' });
            if (sessionResponse.ok) {
              window.location.replace('/');
              return;
            }
            status.textContent = 'The launch link is missing its token. Restart TermForge to open a new session.';
            return;
          }
          const exchangeResponse = await fetch('/api/session/bootstrap', {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ token }),
          });
          if (exchangeResponse.ok) {
            window.location.replace('/');
            return;
          }
          status.textContent = 'The launch link is invalid or was already used. Restart TermForge to open a new session.';
        })();
        """;
}
