import { act, renderHook } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { useRowSelection } from './useRowSelection';

describe('useRowSelection', () => {
  it('toggles single keys and reports partial selection', () => {
    const { result } = renderHook(() => useRowSelection(['web', 'db']));

    act(() => result.current.toggle('web'));
    expect([...result.current.selected]).toEqual(['web']);
    expect(result.current.someSelected).toBe(true);
    expect(result.current.allSelected).toBe(false);

    act(() => result.current.toggle('web'));
    expect(result.current.someSelected).toBe(false);
  });

  it('selects every key and clears them again', () => {
    const { result } = renderHook(() => useRowSelection(['web', 'db']));

    act(() => result.current.toggleAll());
    expect(result.current.allSelected).toBe(true);

    act(() => result.current.toggleAll());
    expect(result.current.selected.size).toBe(0);

    act(() => result.current.toggleAll());
    act(() => result.current.clear());
    expect(result.current.selected.size).toBe(0);
  });

  it('drops keys that are no longer listed', () => {
    const { result, rerender } = renderHook(({ keys }) => useRowSelection(keys), { initialProps: { keys: ['web', 'db'] } });

    act(() => result.current.toggleAll());
    rerender({ keys: ['db'] });

    expect([...result.current.selected]).toEqual(['db']);
    expect(result.current.allSelected).toBe(true);
  });
});
