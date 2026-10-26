import { BrandMarkIcon } from './icons/Icons';
import { UpdateNotice } from './UpdateNotice';

export function AppHeader() {
  return (
    <header className="app-header">
      <div className="brand">
        <span className="brand-mark">
          <BrandMarkIcon />
        </span>
        <span>TermForge</span>
      </div>
      <div className="app-header-spacer" />
      <UpdateNotice />
    </header>
  );
}
