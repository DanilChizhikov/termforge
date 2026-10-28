import { useCallback, useState } from 'react';

export interface RowSelection {
  selected: ReadonlySet<string>;
  allSelected: boolean;
  someSelected: boolean;
  toggle: (key: string) => void;
  toggleAll: () => void;
  clear: () => void;
}

const NOTHING_SELECTED: ReadonlySet<string> = new Set();

export function useRowSelection(keys: readonly string[]): RowSelection {
  const [chosen, setChosen] = useState<ReadonlySet<string>>(NOTHING_SELECTED);
  const selected: ReadonlySet<string> = new Set(keys.filter((key) => chosen.has(key)));
  const allSelected = keys.length > 0 && selected.size === keys.length;

  const toggle = (key: string) => {
    const next = new Set(selected);
    if (next.has(key)) {
      next.delete(key);
    } else {
      next.add(key);
    }
    setChosen(next);
  };

  const toggleAll = () => {
    setChosen(allSelected ? NOTHING_SELECTED : new Set(keys));
  };

  const clear = useCallback(() => setChosen(NOTHING_SELECTED), []);

  return { selected, allSelected, someSelected: selected.size > 0, toggle, toggleAll, clear };
}
