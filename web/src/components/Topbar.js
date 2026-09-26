'use client';

import { useState, useEffect, useRef } from 'react';
import Link from 'next/link';

const BRANCHES = [
  { id: 'all', name: 'Tüm Filo (Genel Merkez)', code: 'HQ' },
  { id: 'ist', name: 'İstanbul Havalimanı (İST)', code: 'İST' },
  { id: 'saw', name: 'Sabiha Gökçen Havalimanı (SAW)', code: 'SAW' },
  { id: 'esb', name: 'Ankara Esenboğa (ESB)', code: 'ESB' },
  { id: 'adb', name: 'İzmir Adnan Menderes (ADB)', code: 'ADB' },
];

export default function Topbar({ user }) {
  const [selectedBranch, setSelectedBranch] = useState(BRANCHES[0]);
  const [showBranchMenu, setShowBranchMenu] = useState(false);
  const [showNotifications, setShowNotifications] = useState(false);
  const [showSearchModal, setShowSearchModal] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState([]);
  const [vehicles, setVehicles] = useState([]);

  const [notifications, setNotifications] = useState([
    { id: 1, title: 'Yeni Hasar Tespiti', desc: '06 ABG 274 aracında 3 yeni hasar kaydedildi.', time: '5 dk önce', read: false, type: 'danger', link: '/reports' },
    { id: 2, title: 'Saha Senkronizasyonu', desc: 'Saha Tableti #1 başarıyla verileri aktardı.', time: '12 dk önce', read: false, type: 'info', link: '/sync' },
    { id: 3, title: 'Kiralama Tamamlandı', desc: '34 DNT 2026 teslim işlemi onaylandı.', time: '1 saat önce', read: true, type: 'success', link: '/vehicles' },
  ]);

  const branchRef = useRef(null);
  const notifRef = useRef(null);

  useEffect(() => {
    fetch('/api/vehicles')
      .then(r => r.json())
      .then(d => { if (Array.isArray(d)) setVehicles(d); })
      .catch(() => {});
  }, []);

  // Ctrl + K Kısayol Dinleyicisi
  useEffect(() => {
    const handleKeyDown = (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        setShowSearchModal(prev => !prev);
      }
      if (e.key === 'Escape') {
        setShowSearchModal(false);
        setShowNotifications(false);
        setShowBranchMenu(false);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  // Arama filtreleme
  useEffect(() => {
    if (!searchQuery.trim()) {
      setSearchResults([]);
      return;
    }
    const q = searchQuery.toLowerCase();
    const filtered = vehicles.filter(v => 
      v.plate_number?.toLowerCase().includes(q) || 
      v.customer_name?.toLowerCase().includes(q)
    );
    setSearchResults(filtered);
  }, [searchQuery, vehicles]);

  const unreadCount = notifications.filter(n => !n.read).length;

  const markAllRead = () => {
    setNotifications(notifications.map(n => ({ ...n, read: true })));
  };

  return (
    <header className="topbar-container no-print">
      {/* Sol: Şube Seçici & Hızlı Arama Butonu */}
      <div className="topbar-left">
        {/* Şube Seçici Dropdown */}
        <div className="branch-selector" ref={branchRef}>
          <button 
            className="branch-btn"
            onClick={() => setShowBranchMenu(!showBranchMenu)}
          >
            <span className="branch-icon">🏢</span>
            <span className="branch-name">{selectedBranch.name}</span>
            <span className="dropdown-arrow">{showBranchMenu ? '▲' : '▼'}</span>
          </button>

          {showBranchMenu && (
            <div className="branch-dropdown animate-in">
              <div className="dropdown-header">OPERASYON ŞUBESİ SEÇİN</div>
              {BRANCHES.map(b => (
                <div
                  key={b.id}
                  className={`branch-option ${b.id === selectedBranch.id ? 'active' : ''}`}
                  onClick={() => { setSelectedBranch(b); setShowBranchMenu(false); }}
                >
                  <span className="badge neutral">{b.code}</span>
                  <span>{b.name}</span>
                  {b.id === selectedBranch.id && <span className="check-mark">✓</span>}
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Global Hızlı Arama Çubuğu */}
        <button className="global-search-btn" onClick={() => setShowSearchModal(true)}>
          <span className="search-icon">🔍</span>
          <span className="search-text">Plaka, müşteri veya rapor ara...</span>
          <kbd className="search-kbd">Ctrl K</kbd>
        </button>
      </div>

      {/* Sağ: Canlı Bildirimler & Sistem Durumu */}
      <div className="topbar-right">
        {/* Canlı Edge AI Durum Rozeti */}
        <div className="system-live-badge">
          <span className="live-pulse"></span>
          <span>Edge AI: Çevrimiçi</span>
        </div>

        {/* Bildirim Zili (🔔) */}
        <div className="notif-wrapper" ref={notifRef}>
          <button 
            className="notif-btn" 
            onClick={() => setShowNotifications(!showNotifications)}
            aria-label="Bildirimler"
          >
            <span style={{ fontSize: '18px' }}>🔔</span>
            {unreadCount > 0 && <span className="notif-badge">{unreadCount}</span>}
          </button>

          {showNotifications && (
            <div className="notif-drawer animate-in">
              <div className="notif-header">
                <strong>Canlı Saha Bildirimleri</strong>
                {unreadCount > 0 && (
                  <button className="mark-read-btn" onClick={markAllRead}>Tümünü Okundu Say</button>
                )}
              </div>

              <div className="notif-list">
                {notifications.map(n => (
                  <Link
                    key={n.id}
                    href={n.link}
                    className={`notif-item ${n.read ? 'read' : 'unread'}`}
                    onClick={() => setShowNotifications(false)}
                  >
                    <div className={`notif-indicator ${n.type}`}></div>
                    <div className="notif-body">
                      <div className="notif-item-title">{n.title}</div>
                      <div className="notif-item-desc">{n.desc}</div>
                      <div className="notif-item-time">{n.time}</div>
                    </div>
                  </Link>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Kullanıcı Profili Minisi */}
        {user && (
          <div className="user-pill">
            <div className="user-avatar-mini">{user.name ? user.name[0] : 'U'}</div>
            <div className="user-info-mini">
              <span className="u-name">{user.name}</span>
              <span className="u-role">{user.role === 'admin' ? 'Filo Müdürü' : 'Saha Yetkilisi'}</span>
            </div>
          </div>
        )}
      </div>

      {/* Ctrl + K Arama Modalı */}
      {showSearchModal && (
        <div className="search-modal-overlay" onClick={() => setShowSearchModal(false)}>
          <div className="search-modal-box" onClick={e => e.stopPropagation()}>
            <div className="search-input-wrap">
              <span style={{ fontSize: '20px', color: '#38bdf8' }}>🔍</span>
              <input
                type="text"
                autoFocus
                placeholder="Plaka numarası veya müşteri adı yazın... (Örn: 06 ABG, Ahmet)"
                value={searchQuery}
                onChange={e => setSearchQuery(e.target.value)}
                className="search-modal-input"
              />
              <button className="close-search-btn" onClick={() => setShowSearchModal(false)}>ESC</button>
            </div>

            <div className="search-modal-results">
              {searchResults.length > 0 ? (
                searchResults.map(v => (
                  <Link
                    key={v.id}
                    href={`/vehicles/${v.id}`}
                    className="search-result-row"
                    onClick={() => setShowSearchModal(false)}
                  >
                    <div>
                      <strong className="plate-cyan">{v.plate_number}</strong>
                      <span style={{ marginLeft: '12px', color: '#cbd5e1' }}>{v.customer_name}</span>
                    </div>
                    <span className={`badge ${v.status === 'tamamlandi' ? 'neutral' : 'warning'}`}>
                      {v.status === 'tamamlandi' ? 'Raporlu' : 'Kirada'}
                    </span>
                  </Link>
                ))
              ) : searchQuery ? (
                <div className="search-empty">Eşleşen araç veya rapor bulunamadı.</div>
              ) : (
                <div className="search-quick-links">
                  <div className="quick-title">HIZLI MENÜ GEÇİŞİ:</div>
                  <div className="quick-grid">
                    <Link href="/vehicles" onClick={() => setShowSearchModal(false)} className="quick-chip">🚗 Filo Araçları</Link>
                    <Link href="/reports" onClick={() => setShowSearchModal(false)} className="quick-chip">📄 Hasar Raporları</Link>
                    <Link href="/analytics" onClick={() => setShowSearchModal(false)} className="quick-chip">📊 Şube Analitiği</Link>
                    <Link href="/sync" onClick={() => setShowSearchModal(false)} className="quick-chip">🔄 Saha Senkronizasyonu</Link>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </header>
  );
}
