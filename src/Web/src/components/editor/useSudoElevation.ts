import { useState } from 'react';
import { describeEditorError } from '../../api/editor';
import {
  forgetSudoPassword,
  getSudoStatus,
  NO_SUDO_PASSWORD,
  sudoPasswordProblemOf,
  type SudoCredentials,
  type SudoPasswordProblem,
  type SudoStatus,
} from '../../api/sudoEditor';

export interface ElevatedAction {
  label: string;
  run: (sudo: SudoCredentials) => Promise<void>;
  onError: (error: unknown) => void;
  onAbort?: () => void;
}

export interface SudoPrompt {
  action: ElevatedAction;
  problem: SudoPasswordProblem | null;
}

export interface SudoElevation {
  status: SudoStatus | null;
  notice: string | null;
  prompt: SudoPrompt | null;
  busy: boolean;
  check: () => Promise<boolean>;
  perform: (action: ElevatedAction) => Promise<void>;
  submit: (credentials: SudoCredentials) => Promise<void>;
  cancel: () => void;
  forget: () => Promise<void>;
  dismissNotice: () => void;
}

export function useSudoElevation(connectionId: string): SudoElevation {
  const [status, setStatus] = useState<SudoStatus | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [prompt, setPrompt] = useState<SudoPrompt | null>(null);
  const [busy, setBusy] = useState(false);

  async function ensureStatus(): Promise<SudoStatus | null> {
    if (status !== null) {
      return status;
    }
    try {
      const loaded = await getSudoStatus(connectionId);
      setStatus(loaded);
      return loaded;
    } catch (error) {
      setNotice(describeEditorError(error));
      return null;
    }
  }

  async function attempt(action: ElevatedAction, credentials: SudoCredentials): Promise<void> {
    setBusy(true);
    try {
      await action.run(credentials);
      setPrompt(null);
      if (credentials.rememberPassword) {
        setStatus((current) => (current === null ? current : { ...current, hasSavedPassword: true }));
      }
    } catch (error) {
      const problem = sudoPasswordProblemOf(error);
      if (problem === null) {
        setPrompt(null);
        action.onError(error);
      } else {
        action.onAbort?.();
        setPrompt({ action, problem });
      }
    } finally {
      setBusy(false);
    }
  }

  async function usableStatus(): Promise<SudoStatus | null> {
    setNotice(null);
    const current = await ensureStatus();
    if (current === null) {
      return null;
    }
    if (current.availability === 'Unavailable') {
      setNotice(current.message);
      return null;
    }
    return current;
  }

  async function check(): Promise<boolean> {
    const current = await usableStatus();
    return current !== null;
  }

  async function perform(action: ElevatedAction): Promise<void> {
    const current = await usableStatus();
    if (current === null) {
      return;
    }
    if (current.availability === 'PasswordRequired' && !current.hasSavedPassword) {
      setPrompt({ action, problem: null });
      return;
    }
    await attempt(action, NO_SUDO_PASSWORD);
  }

  async function submit(credentials: SudoCredentials): Promise<void> {
    if (prompt === null) {
      return;
    }
    await attempt(prompt.action, credentials);
  }

  async function forget(): Promise<void> {
    try {
      await forgetSudoPassword(connectionId);
      setStatus((current) => (current === null ? current : { ...current, hasSavedPassword: false }));
    } catch (error) {
      setNotice(describeEditorError(error));
    }
  }

  return {
    status,
    notice,
    prompt,
    busy,
    check,
    perform,
    submit,
    cancel: () => setPrompt(null),
    forget,
    dismissNotice: () => setNotice(null),
  };
}
