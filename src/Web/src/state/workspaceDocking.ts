import { WORKSPACE_ROOT, type DockTarget } from './dockTarget';
import { normalizeFloatingRect, type FloatingRect } from './floatingGeometry';

interface DockTab {
  panelId: string;
  slotId: string;
}

export interface DockStack {
  kind: 'stack';
  id: string;
  origin: boolean;
  tabs: readonly DockTab[];
  selected: string | null;
}

interface DockSplit {
  kind: 'split';
  id: string;
  orientation: 'horizontal' | 'vertical';
  first: DockNode;
  second: DockNode;
  firstSize: number;
}

export type DockNode = DockStack | DockSplit;

export type PanelPlacement =
  | { mode: 'tiled'; slotId: string }
  | { mode: 'floating'; rect: FloatingRect; returnSlotId: string; returnStackId?: string };

export interface DockLayout {
  placements: ReadonlyMap<string, PanelPlacement>;
  origins: ReadonlyMap<string, string>;
  trees: ReadonlyMap<string, DockNode>;
  nextId: number;
}

export function emptyDockLayout(): DockLayout {
  return { placements: new Map(), origins: new Map(), trees: new Map(), nextId: 0 };
}

export function originStack(id: string): DockStack {
  return { kind: 'stack', id, origin: true, tabs: [], selected: null };
}

function findStack(node: DockNode, match: (stack: DockStack) => boolean): DockStack | undefined {
  if (node.kind === 'stack') return match(node) ? node : undefined;
  return findStack(node.first, match) ?? findStack(node.second, match);
}

function findInLayout(layout: DockLayout, match: (stack: DockStack) => boolean) {
  for (const [origin, tree] of layout.trees) {
    const stack = findStack(tree, match);
    if (stack !== undefined) return { origin, stack };
  }
  return undefined;
}

function updateTree(node: DockNode, update: (stack: DockStack) => DockNode | null): DockNode | null {
  if (node.kind === 'stack') return update(node);
  const first = updateTree(node.first, update);
  const second = updateTree(node.second, update);
  if (first === null) return second;
  if (second === null) return first;
  return first === node.first && second === node.second ? node : { ...node, first, second };
}

function replaceStack(layout: DockLayout, stack: DockStack, replacement: DockNode) {
  const entry = findInLayout(layout, (item) => item.id === stack.id);
  const origin = entry?.origin ?? stack.id;
  const tree = layout.trees.get(origin) ?? stack;
  const updated = updateTree(tree, (item) => item.id === stack.id ? replacement : item);
  return new Map(layout.trees).set(origin, updated ?? originStack(origin));
}

function removeDockTab(layout: DockLayout, panelId: string): DockLayout {
  const entry = findInLayout(layout, (stack) => stack.tabs.some((tab) => tab.panelId === panelId));
  if (entry === undefined) return layout;

  const tree = layout.trees.get(entry.origin)!;
  const updated = updateTree(tree, (stack) => {
    if (stack !== entry.stack) return stack;
    const tabs = stack.tabs.filter((tab) => tab.panelId !== panelId);
    if (tabs.length === 0 && !stack.origin) return null;
    const selected = stack.selected === panelId ? null : stack.selected;
    return { ...stack, tabs, selected };
  });
  return { ...layout, trees: new Map(layout.trees).set(entry.origin, updated ?? originStack(entry.origin)) };
}

export function registerDockPanel(layout: DockLayout, id: string, slotId: string): DockLayout {
  return {
    ...layout,
    origins: new Map(layout.origins).set(id, slotId),
    placements: new Map(layout.placements).set(id, { mode: 'tiled', slotId }),
  };
}

export function unregisterDockPanel(layout: DockLayout, id: string): DockLayout {
  const next = removeDockTab(layout, id);
  const placements = new Map(next.placements);
  const origins = new Map(next.origins);
  const trees = new Map(next.trees);
  const origin = origins.get(id);
  const owned = origin === undefined ? undefined : trees.get(origin);
  let nextId = next.nextId;
  if (origin !== undefined && owned !== undefined) {
    trees.delete(origin);
    const remaining = updateTree(owned, (stack) => {
      if (!stack.origin) return stack;
      if (stack.tabs.length === 0) return null;
      return { ...stack, id: `dock-stack-${++nextId}`, origin: false };
    });
    if (remaining !== null) {
      nextId++;
      trees.set(WORKSPACE_ROOT, {
        kind: 'split', id: `dock-split-${nextId}`, orientation: 'horizontal', firstSize: 70,
        first: trees.get(WORKSPACE_ROOT) ?? originStack(WORKSPACE_ROOT), second: remaining,
      });
    }
  }
  placements.delete(id);
  origins.delete(id);
  return { ...next, placements, origins, trees, nextId };
}

function setPlacement(layout: DockLayout, id: string, placement: PanelPlacement): DockLayout {
  return { ...layout, placements: new Map(layout.placements).set(id, placement) };
}

function floatPanel(layout: DockLayout, id: string, rect: FloatingRect): DockLayout {
  const placement = layout.placements.get(id);
  if (placement === undefined) return layout;
  if (placement.mode === 'floating') return setPlacement(layout, id, { ...placement, rect });

  const previous = findInLayout(layout, (stack) => stack.tabs.some((tab) => tab.panelId === id));
  return setPlacement(removeDockTab(layout, id), id, {
    mode: 'floating',
    rect,
    returnSlotId: placement.slotId,
    ...(previous === undefined ? {} : { returnStackId: previous.stack.id }),
  });
}

function appendTab(layout: DockLayout, stack: DockStack, tab: DockTab): DockLayout {
  const updated = { ...stack, tabs: [...stack.tabs, tab], selected: tab.panelId };
  const trees = replaceStack(layout, stack, updated);
  return setPlacement({ ...layout, trees }, tab.panelId, { mode: 'tiled', slotId: tab.slotId });
}

export function returnDockPanel(layout: DockLayout, id: string, registeredSlots: Pick<ReadonlySet<string>, 'has'>): DockLayout {
  const placement = layout.placements.get(id);
  const original = layout.origins.get(id);
  if (placement?.mode !== 'floating' || original === undefined) return layout;
  if (placement.returnStackId !== undefined) {
    const previous = findInLayout(layout, (stack) => stack.id === placement.returnStackId)?.stack;
    if (previous !== undefined) return appendTab(layout, previous, { panelId: id, slotId: placement.returnSlotId });
    return setPlacement(layout, id, { mode: 'tiled', slotId: original });
  }
  const slotId = registeredSlots.has(placement.returnSlotId) ? placement.returnSlotId : original;
  return setPlacement(layout, id, { mode: 'tiled', slotId });
}

export function selectDockTab(layout: DockLayout, stackId: string, selected: string): DockLayout {
  const stack = findInLayout(layout, (item) => item.id === stackId)?.stack;
  if (stack === undefined || stack.selected === selected) return layout;
  const native = stack.origin && layout.origins.get(selected) === stackId;
  if (!native && !stack.tabs.some((tab) => tab.panelId === selected)) return layout;
  return { ...layout, trees: replaceStack(layout, stack, { ...stack, selected }) };
}

function resizeTree(node: DockNode, splitId: string, firstSize: number): DockNode {
  if (node.kind === 'stack') return node;
  if (node.id === splitId) return node.firstSize === firstSize ? node : { ...node, firstSize };
  const first = resizeTree(node.first, splitId, firstSize);
  const second = resizeTree(node.second, splitId, firstSize);
  return first === node.first && second === node.second ? node : { ...node, first, second };
}

export function resizeDockSplit(layout: DockLayout, splitId: string, firstSize: number): DockLayout {
  if (!Number.isFinite(firstSize) || firstSize <= 0 || firstSize >= 100) return layout;
  for (const [origin, tree] of layout.trees) {
    const resized = resizeTree(tree, splitId, firstSize);
    if (resized !== tree) return { ...layout, trees: new Map(layout.trees).set(origin, resized) };
  }
  return layout;
}

interface PanelDrop {
  panelId: string;
  mode: PanelPlacement['mode'];
  rect: FloatingRect;
  target: DockTarget | null;
}

export function applyPanelDrop(layout: DockLayout, drop: PanelDrop): DockLayout {
  const { panelId, mode, target } = drop;
  const rect = normalizeFloatingRect(drop.rect);
  if (rect === null || layout.placements.get(panelId)?.mode !== mode) return layout;
  if (target === null) return floatPanel(layout, panelId, rect);

  const anchorId = target.kind === 'tab' ? target.slotId : target.anchorId;
  if (target.kind === 'tab' && anchorId === WORKSPACE_ROOT) return layout;
  const entry = findInLayout(layout, (stack) => stack.id === anchorId);
  const isOrigin = [...layout.origins.values()].includes(anchorId);
  if (anchorId !== WORKSPACE_ROOT && entry === undefined && !isOrigin) return layout;

  const next = removeDockTab(layout, panelId);
  const anchor = findInLayout(next, (stack) => stack.id === anchorId)?.stack;
  if (anchor === undefined && anchorId !== WORKSPACE_ROOT && !isOrigin) return layout;
  const id = next.nextId + 1;
  const tab = { panelId, slotId: `dock-slot-${id}` };
  const state = { ...next, nextId: id };
  if (target.kind === 'tab') return appendTab(state, anchor ?? originStack(anchorId), tab);

  const stack: DockStack = { kind: 'stack', id: `dock-stack-${id}`, origin: false, tabs: [tab], selected: panelId };
  const root = anchorId === WORKSPACE_ROOT;
  const existing = root ? next.trees.get(WORKSPACE_ROOT) ?? originStack(WORKSPACE_ROOT) : anchor ?? originStack(anchorId);
  const before = target.side === 'left' || target.side === 'top';
  const percentage = root ? 30 : 50;
  const split: DockSplit = {
    kind: 'split',
    id: `dock-split-${id}`,
    orientation: target.side === 'left' || target.side === 'right' ? 'horizontal' : 'vertical',
    first: before ? stack : existing,
    second: before ? existing : stack,
    firstSize: before ? percentage : 100 - percentage,
  };
  const trees = root
    ? new Map(state.trees).set(WORKSPACE_ROOT, split)
    : replaceStack(state, anchor ?? originStack(anchorId), split);
  return setPlacement({ ...state, trees }, panelId, { mode: 'tiled', slotId: tab.slotId });
}
