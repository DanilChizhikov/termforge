import { Fragment, useCallback, useEffect, useState, type FormEvent } from 'react';
import { describeError } from '../../api/connections';
import { getDockerImages, removeDockerImage, type DockerContainer, type DockerImage } from '../../api/docker';
import { isConfirmationSkipped, setConfirmationSkipped } from '../../state/dockerConfirmPreferences';
import { EmptyState } from '../EmptyState';
import { ConfirmDockerAction } from './ConfirmDockerAction';
import { DockerNotice, type ActionNotice } from './DockerNotice';
import { imageReference, imageUsage, shortImageId } from './dockerStatus';
import { ImagePullProgress } from './ImagePullProgress';

export interface ImagesPanelProps {
  serverId: string;
  serverName: string;
  containers: DockerContainer[];
  readOnly: boolean;
  reloadKey: number;
  onPulled: () => void;
}

interface ImagesState {
  images: DockerImage[] | null;
  error: string | null;
  isLoading: boolean;
}

interface PullRequest {
  image: string;
  attempt: number;
}

const COLUMN_COUNT = 7;

export function ImagesPanel({ serverId, serverName, containers, readOnly, reloadKey, onPulled }: ImagesPanelProps) {
  const [state, setState] = useState<ImagesState>({ images: null, error: null, isLoading: true });
  const [localReloadKey, setLocalReloadKey] = useState(0);
  const [pendingRemoval, setPendingRemoval] = useState<string | null>(null);
  const [busyImage, setBusyImage] = useState<string | null>(null);
  const [notice, setNotice] = useState<ActionNotice | null>(null);
  const [reference, setReference] = useState('');
  const [pull, setPull] = useState<PullRequest | null>(null);
  const [isPulling, setIsPulling] = useState(false);

  useEffect(() => {
    let active = true;
    getDockerImages(serverId)
      .then((loaded) => {
        if (active) {
          setState({ images: loaded.images, error: null, isLoading: false });
        }
      })
      .catch((failure: unknown) => {
        if (active) {
          setState((previous) => ({ ...previous, error: describeError(failure), isLoading: false }));
        }
      });
    return () => {
      active = false;
    };
  }, [serverId, reloadKey, localReloadKey]);

  const reload = useCallback(() => {
    setState((previous) => ({ ...previous, error: null, isLoading: true }));
    setLocalReloadKey((key) => key + 1);
  }, []);

  const remove = useCallback(
    (image: string) => {
      setBusyImage(image);
      setNotice(null);
      removeDockerImage(serverId, image)
        .then(() => {
          setNotice({ tone: 'ok', message: `Removed ${image}. Recorded in the journal.` });
          reload();
        })
        .catch((failure: unknown) => setNotice({ tone: 'danger', message: describeError(failure) }))
        .finally(() => setBusyImage(null));
    },
    [serverId, reload],
  );

  const requestRemoval = (image: string) => {
    if (isConfirmationSkipped('imageRemove')) {
      remove(image);
      return;
    }
    setPendingRemoval(image);
  };

  const confirmRemoval = (skipNextTime: boolean) => {
    if (pendingRemoval !== null) {
      if (skipNextTime) {
        setConfirmationSkipped('imageRemove', true);
      }
      remove(pendingRemoval);
    }
    setPendingRemoval(null);
  };

  const startPull = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const image = reference.trim();
    if (image === '' || isPulling) {
      return;
    }
    setNotice(null);
    setIsPulling(true);
    setPull((previous) => ({ image, attempt: (previous?.attempt ?? 0) + 1 }));
  };

  const finishPull = useCallback(
    (succeeded: boolean) => {
      setIsPulling(false);
      if (succeeded) {
        setReference('');
        onPulled();
      }
    },
    [onPulled],
  );

  const images = state.images ?? [];

  return (
    <>
      <form className="docker-pull-form" aria-label="Pull image" onSubmit={startPull}>
        <label className="field docker-pull-field">
          <span className="field-label">Image reference</span>
          <input
            type="text"
            className="input input-mono"
            placeholder="nginx:1.27-alpine"
            autoComplete="off"
            spellCheck={false}
            value={reference}
            disabled={readOnly}
            onChange={(event) => setReference(event.target.value)}
          />
        </label>
        <button type="submit" className="btn btn-primary" disabled={readOnly || isPulling || reference.trim() === ''}>
          {isPulling ? 'Pulling…' : 'Pull'}
        </button>
      </form>
      {pull !== null ? (
        <ImagePullProgress
          key={pull.attempt}
          connectionId={serverId}
          image={pull.image}
          onFinished={finishPull}
          onClose={() => setPull(null)}
        />
      ) : null}
      {state.isLoading && state.images === null ? <p className="text-secondary">Loading images...</p> : null}
      {state.error !== null ? (
        <p role="alert" className="notice notice-danger">
          {state.error}
        </p>
      ) : null}
      <DockerNotice notice={notice} />
      {state.images !== null && images.length === 0 ? <EmptyState message="No images on this server." /> : null}
      {images.length > 0 ? (
        <div className="table-card">
          <table className="data-table docker-table">
            <thead>
              <tr>
                <th>Repository</th>
                <th>Tag</th>
                <th>Image ID</th>
                <th>Size</th>
                <th>Created</th>
                <th>Used by</th>
                <th className="cell-end">Actions</th>
              </tr>
            </thead>
            <tbody>
              {images.map((image) => {
                const target = imageReference(image);
                const usage = imageUsage(image, containers);
                return (
                  <Fragment key={`${image.id}:${target}`}>
                    <tr>
                      <td className="docker-image">
                        <code>{image.repository}</code>
                      </td>
                      <td className="docker-image">
                        <code>{image.tag}</code>
                      </td>
                      <td>
                        <code>{shortImageId(image.id)}</code>
                      </td>
                      <td className="cell-muted">{image.size}</td>
                      <td className="cell-muted">{image.createdSince}</td>
                      <td className="cell-muted">{usage === 0 ? '—' : `${usage} ${usage === 1 ? 'container' : 'containers'}`}</td>
                      <td className="cell-end">
                        <div className="docker-actions">
                          <button
                            type="button"
                            className={
                              pendingRemoval === target
                                ? 'btn btn-sm docker-row-button docker-row-button-armed'
                                : 'btn btn-sm docker-row-button'
                            }
                            disabled={readOnly || busyImage === target}
                            aria-label={`Remove image ${target}`}
                            aria-expanded={pendingRemoval === target}
                            onClick={() => requestRemoval(target)}
                          >
                            Remove
                          </button>
                        </div>
                      </td>
                    </tr>
                    {pendingRemoval === target ? (
                      <tr className="docker-confirm-row">
                        <td colSpan={COLUMN_COUNT}>
                          <ConfirmDockerAction
                            title={`Remove image ${target} on ${serverName}?`}
                            consequence={
                              usage > 0
                                ? 'Containers still use this image, so Docker refuses until they are removed. The image is deleted from the server; pull it again to restore it.'
                                : 'The image is deleted from the server; pull it again to restore it.'
                            }
                            confirmLabel="Remove image"
                            offerSkip
                            onConfirm={confirmRemoval}
                            onCancel={() => setPendingRemoval(null)}
                          />
                        </td>
                      </tr>
                    ) : null}
                  </Fragment>
                );
              })}
            </tbody>
          </table>
        </div>
      ) : null}
    </>
  );
}
