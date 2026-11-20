import { useCallback, useContext, useId, useLayoutEffect, useRef, type ComponentType, type PointerEvent } from 'react';
import { RESIZE_DIRECTIONS, type FloatingDirection, type FloatingRect } from '../../state/floatingGeometry';
import { CloseIcon, type IconProps } from '../icons/Icons';
import { FloatingGestureContext } from './WorkspaceCanvas';

interface FloatingWindowProps {
  panelId: string;
  title: string;
  Icon: ComponentType<IconProps>;
  rect: FloatingRect;
  zIndex: number;
  bodyRef: (element: HTMLDivElement | null) => void;
  onDock: () => void;
  onClose: () => void;
}

export function FloatingWindow({ panelId, title, Icon, rect, zIndex, bodyRef, onDock, onClose }: FloatingWindowProps) {
  const startGesture = useContext(FloatingGestureContext);
  const id = `floating-window-${useId().replace(/[^a-z0-9_-]/gi, '')}`;
  const windowRef = useRef<HTMLElement>(null);
  const style = useRef<HTMLStyleElement>(null);
  const preview = useCallback((next: FloatingRect, dragging: boolean) => {
    windowRef.current?.toggleAttribute('data-dragging', dragging);
    const rule = style.current?.sheet?.cssRules[0] as CSSStyleRule | undefined;
    if (rule === undefined) return;
    rule.style.setProperty('--window-x', `${next.x}px`);
    rule.style.setProperty('--window-y', `${next.y}px`);
    rule.style.setProperty('--window-width', `${next.width}px`);
    rule.style.setProperty('--window-height', `${next.height}px`);
  }, []);

  useLayoutEffect(() => preview(rect, false), [preview, rect]);
  useLayoutEffect(() => {
    const rule = style.current?.sheet?.cssRules[0] as CSSStyleRule | undefined;
    rule?.style.setProperty('--window-z', String(zIndex));
  }, [zIndex]);

  const begin = (event: PointerEvent<HTMLElement>, direction: FloatingDirection) => {
    if (event.button === 0 && event.isPrimary) windowRef.current?.focus({ preventScroll: true });
    startGesture?.(event, { panelId, direction, rect, preview });
  };

  return (
    <section ref={windowRef} id={id} aria-label={title} className="floating-window" data-floating-panel={panelId} tabIndex={-1}>
      <style ref={style}>{`#${id} {}`}</style>
      <header className="floating-window-header">
        <div className="floating-window-title" onPointerDown={(event) => begin(event, 'move')}>
          <Icon />
          <span title={title}>{title}</span>
        </div>
        <button type="button" className="btn" onClick={onDock}>Dock</button>
        <button type="button" className="icon-btn" aria-label="Close floating window" onClick={onClose}>
          <CloseIcon />
        </button>
      </header>
      <div ref={bodyRef} className="floating-window-body" />
      {RESIZE_DIRECTIONS.map((direction) => (
        <span
          key={direction}
          className={`floating-window-resize floating-window-resize-${direction}`}
          data-resize-direction={direction}
          aria-hidden="true"
          onPointerDown={(event) => begin(event, direction)}
        />
      ))}
    </section>
  );
}
