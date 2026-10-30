import { Spinner } from './Spinner';

export interface LoadingStateProps {
  message: string;
  className?: string;
}

export function LoadingState({ message, className }: LoadingStateProps) {
  const classNames = className === undefined ? 'loading-state' : `loading-state ${className}`;
  return (
    <p className={classNames}>
      <Spinner />
      {message}
    </p>
  );
}
