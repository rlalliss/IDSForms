import React from 'react';
import { Link, useLocation } from 'react-router-dom';
import logoUrl from '../assets/ids-logo.png';

type Breadcrumb = { label: string; to?: string };

interface AppShellProps {
  title: string;
  subtitle?: string;
  breadcrumbs?: Breadcrumb[];
  actions?: React.ReactNode;
  children: React.ReactNode;
}

const navItems = [
  { label: 'Dashboard', to: '/dashboard' },
  { label: 'Forms', to: '/forms' },
  { label: 'Fill', to: '/fill' },
  { label: 'Sign', to: '/sign' }
];

export default function AppShell({
  title,
  subtitle,
  breadcrumbs,
  actions,
  children
}: AppShellProps) {
  const location = useLocation();

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="app-header__inner">
          <Link to="/dashboard" className="brand">
            <img src={logoUrl} alt="Independent Dealer Solutions" className="brand-logo" />
            <span className="brand-copy">
              IDS Forms
              <small>Independent Dealer Solutions</small>
            </span>
          </Link>
          <nav className="app-nav">
            {navItems.map((item) => {
              const active = location.pathname.startsWith(item.to);
              return (
                <Link
                  key={item.to}
                  to={item.to}
                  className={`app-nav__link ${active ? 'app-nav__link--active' : ''}`}
                >
                  {item.label}
                </Link>
              );
            })}
          </nav>
        </div>
      </header>

      <main className="app-main">
        <section className="app-hero">
          <div className="app-hero__breadcrumbs">
            {breadcrumbs?.length
              ? breadcrumbs.map((crumb, idx) => (
                  <React.Fragment key={crumb.label}>
                    {idx > 0 && <span> / </span>}
                    {crumb.to ? <Link to={crumb.to}>{crumb.label}</Link> : <span>{crumb.label}</span>}
                  </React.Fragment>
                ))
              : 'Workspace'}
          </div>
          <h1 className="app-hero__title">{title}</h1>
          {subtitle && <p className="app-hero__subtitle">{subtitle}</p>}
          {actions && <div className="app-hero__actions">{actions}</div>}
        </section>

        <section className="shell-content">{children}</section>
      </main>
    </div>
  );
}
