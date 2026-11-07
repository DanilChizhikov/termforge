namespace DTech.Entrypoint.Updates.Installation;

internal static class RecoveryPage
{
    public const string Html = """
        <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>Restore TermForge</title><link rel="stylesheet" href="/recovery.css"><script src="/recovery.js" defer></script></head>
        <body><main class="dialog-backdrop"><section class="dialog-card" aria-labelledby="title">
        <div class="dialog-header"><h1 id="title" class="dialog-title">Restore TermForge</h1>
        <p>The update did not finish. Normal services and database migrations are paused.</p></div>
        <div class="dialog-body"><p id="error" role="alert" class="notice notice-danger">Checking recovery…</p>
        <p>Restoring replaces local data with the copy taken before the update. Changes made after that copy will be lost.</p>
        <label><input id="keep" type="checkbox" checked> Save a copy of current data before restoring</label>
        <p id="status" role="status">Open this page through the private launch link created by TermForge.</p></div>
        <div class="dialog-footer"><button id="restore" class="btn btn-primary" disabled>Restore and restart</button></div>
        </section></main></body></html>
        """;

    public const string Script = """
        (async () => {
          const status = document.getElementById('status');
          const button = document.getElementById('restore');
          const proof = sessionStorage.getItem('termforge.origin-proof');
          const headers = { 'X-CSRF-Token': proof || '', 'Content-Type': 'application/json' };
          try {
            const response = await fetch('/api/updates/recovery', { headers });
            if (!response.ok) throw new Error('Open the new private launch link to authenticate recovery.');
            const recovery = await response.json();
            document.getElementById('error').textContent = recovery.error;
            if (recovery.backupRequired && !recovery.backupAvailable) {
              status.textContent = 'The required data snapshot is missing or incomplete. Backups are retained; restore the snapshot before retrying recovery.';
              return;
            }
            status.textContent = recovery.backupAvailable
              ? 'Your previous application files are available. Data is restored only after confirmation.'
              : 'No complete data snapshot was created. Restart will keep existing data.';
            button.disabled = false;
            button.addEventListener('click', async () => {
              button.disabled = true;
              try {
                const result = await fetch('/api/updates/restore', {
                  method: 'POST', headers,
                  body: JSON.stringify({ saveCurrentData: document.getElementById('keep').checked }),
                });
                if (!result.ok) throw new Error('Recovery could not start. Your backups have been retained.');
                status.textContent = 'Restoring. TermForge will open a new private launch link when it restarts.';
              } catch (error) { status.textContent = error.message; button.disabled = false; }
            });
          } catch (error) { status.textContent = error.message; }
        })();
        """;
}
