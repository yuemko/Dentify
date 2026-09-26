'use client';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { useState, useEffect } from 'react';

const navItems = [
  { href: '/', label: 'Dashboard', icon: '📊' },
  { href: '/vehicles', label: 'Filo & Araçlar', icon: '🚗' },
  { href: '/reports', label: 'Hasar Raporları', icon: '📋' },
  { href: '/analytics', label: 'Şube & Analitik', icon: '📈' },
  { href: '/sync', label: 'Saha Senkronizasyonu', icon: '🔄' },
];

export default function Sidebar() {
  const pathname = usePathname();
  const router = useRouter();
  const [user, setUser] = useState(null);

  useEffect(() => {
    if (typeof window !== 'undefined') {
      const stored = localStorage.getItem('dentify_user');
      if (stored) {
        try {
          setUser(JSON.parse(stored));
        } catch (e) {}
      }
    }
  }, [pathname]);

  // Login sayfasında sidebar gösterilmez
  if (pathname === '/login') {
    return null;
  }

  const handleLogout = () => {
    if (typeof window !== 'undefined') {
      localStorage.removeItem('dentify_user');
    }
    router.push('/login');
  };

  return (
    <aside className="sidebar">
      <div className="sidebar-brand">
        <h1>DENTIFY</h1>
        <p>Filo Hasar Yönetim Paneli</p>
      </div>

      <nav className="sidebar-nav">
        {navItems.map((item) => {
          const isActive =
            item.href === '/'
              ? pathname === '/'
              : pathname.startsWith(item.href);

          return (
            <Link
              key={item.href}
              href={item.href}
              className={`sidebar-link ${isActive ? 'active' : ''}`}
            >
              <span className="icon">{item.icon}</span>
              {item.label}
            </Link>
          );
        })}
      </nav>

      <div className="sidebar-footer">
        <div style={{ marginBottom: '12px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div>
            <div style={{ fontWeight: '700', fontSize: '13px', color: 'var(--text-primary)' }}>
              {user ? user.name : 'Filo Yöneticisi'}
            </div>
            <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
              {user ? user.role : 'Yönetici'}
            </div>
          </div>
          <button
            onClick={handleLogout}
            title="Oturumu Kapat"
            style={{
              background: 'transparent',
              border: '1px solid var(--border)',
              borderRadius: '6px',
              padding: '4px 8px',
              color: 'var(--danger)',
              cursor: 'pointer',
              fontSize: '12px'
            }}
          >
            Çıkış
          </button>
        </div>
        <p style={{ fontSize: '11px', color: 'var(--text-dim)' }}>Dentify Web v1.0 Enterprise</p>
      </div>
    </aside>
  );
}
