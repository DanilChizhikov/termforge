export interface ActionNotice {
  tone: 'ok' | 'danger';
  message: string;
}

export function DockerNotice({ notice }: { notice: ActionNotice | null }) {
  if (notice === null) {
    return null;
  }
  const isDanger = notice.tone === 'danger';
  return (
    <p role={isDanger ? 'alert' : 'status'} className={isDanger ? 'notice notice-danger' : 'notice'}>
      {notice.message}
    </p>
  );
}
