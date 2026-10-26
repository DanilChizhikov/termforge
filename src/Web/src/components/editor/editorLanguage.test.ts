import { describe, expect, it } from 'vitest';
import { languageForPath } from './editorLanguage';

describe('languageForPath', () => {
  it.each([
    ['/opt/deploy.sh', 'shell'],
    ['/home/user/.bashrc', 'shell'],
    ['/srv/compose.yml', 'yaml'],
    ['/srv/k8s/deployment.YAML', 'yaml'],
    ['/etc/app/settings.json', 'json'],
    ['/etc/nginx/nginx.conf', 'ini'],
    ['/etc/systemd/system/app.service', 'ini'],
    ['/srv/app/.env', 'ini'],
    ['/srv/app/Dockerfile', 'dockerfile'],
    ['/srv/app/Dockerfile.prod', 'dockerfile'],
    ['/etc/hosts', 'plaintext'],
    ['/var/log/app.log', 'plaintext'],
  ])('maps %s to %s', (path, language) => {
    expect(languageForPath(path).id).toBe(language);
  });
});
