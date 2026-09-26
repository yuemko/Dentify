'use client';

import { usePathname } from 'next/navigation';
import { useState, useEffect } from 'react';
import Sidebar from '@/components/Sidebar';
import Topbar from '@/components/Topbar';

export default function AppShell({ children }) {
  const pathname = usePathname();
  const isLoginPage = pathname === '/login';
  const [user, setUser] = useState(null);

  useEffect(() => {
    if (typeof window !== 'undefined') {
      const stored = localStorage.getItem('dentify_user');
      if (stored) {
        try { setUser(JSON.parse(stored)); } catch (e) {}
      }
    }
  }, [pathname]);

  if (isLoginPage) {
    return <main style={{ width: '100%', minHeight: '100vh' }}>{children}</main>;
  }

  return (
    <div className="app-layout">
      <Sidebar />
      <div className="main-content-wrapper">
        <Topbar user={user} />
        <main className="main-content">
          {children}
        </main>
      </div>
    </div>
  );
}

