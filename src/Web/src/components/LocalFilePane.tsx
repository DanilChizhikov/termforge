import { useEffect, useMemo, useState } from 'react';
import { createLocalSource, getLocalRoots, type LocalRoot } from '../api/files';
import { setLocalPath, useLocalPath } from '../state/filePaths';
import { FilePane } from './FilePane';

export function LocalFilePane() {
  const path = useLocalPath();
  const source = useMemo(() => createLocalSource(), []);
  const [roots, setRoots] = useState<LocalRoot[]>([]);

  useEffect(() => {
    let cancelled = false;
    getLocalRoots()
      .then((list) => {
        if (!cancelled) {
          setRoots(list);
        }
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <FilePane
      title="Local"
      sourceKey="local"
      source={source}
      path={path}
      onPathChange={setLocalPath}
      roots={roots}
    />
  );
}
