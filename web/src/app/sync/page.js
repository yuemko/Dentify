'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';

export default function SyncPage() {
  const [syncing, setSyncing] = useState(false);
  const [syncResult, setSyncResult] = useState(null);
  const [syncHistory, setSyncHistory] = useState([]);

  useEffect(() => {
    // Son senkronizasyon geçmişini yükle
    fetch('/api/sessions?limit=5')
      .then(r => r.json())
      .then(data => setSyncHistory(data.slice(0, 5)))
      .catch(() => {});
  }, [syncResult]);

  const refreshHistory = () => {
    fetch('/api/sessions?limit=5')
      .then(r => r.json())
      .then(data => setSyncHistory(Array.isArray(data) ? data.slice(0, 5) : []))
      .catch(() => {});
  };

  const devices = [
    { id: 'DEV-01', name: 'Saha Tableti #1 (Havalimanı Şubesi)', status: 'online', battery: '%94', lastSync: 'Şimdi', version: 'v1.0.4 APK' },
    { id: 'DEV-02', name: 'Mobil El Terminali #2 (Merkez Şube)', status: 'online', battery: '%78', lastSync: '14 dk önce', version: 'v1.0.4 APK' },
    { id: 'DEV-03', name: 'Masaüstü Ekspertiz Terminali (Windows)', status: 'online', battery: 'AC Şarj', lastSync: 'Aktif', version: 'v1.0.4 Win' },
  ];

  return (
    <>
      <div className="page-header">
        <h2>Saha & Cihaz Senkronizasyonu</h2>
        <p>Mobil APK ve masaüstü terminallerinin bağlantı durumu ve anlık veri akışı kontrol merkezi</p>
      </div>

      {/* Durum & Canlı Aksiyon */}
      <div className="section-grid">
        <div className="section-card">
          <h3>📡 Canlı Sunucu Veri Dinleyici</h3>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '16px' }}>
            <div style={{ width: '12px', height: '12px', borderRadius: '50%', background: '#22c55e', boxShadow: '0 0 10px #22c55e' }}></div>
            <span style={{ fontWeight: '600', color: '#22c55e' }}>Bağlantı Açık & Aktif (HTTP POST /api/sync)</span>
          </div>

          <p style={{ fontSize: '13px', color: 'var(--text-secondary)', lineHeight: 1.5, marginBottom: '20px' }}>
            Mobil veya masaüstü uygulamasında araç çekimi tamamlanıp <strong>"Raporu Kaydet ve Senkronize Et"</strong> butonuna basıldığında, veriler anında bu yönetim merkezine düşer ve SQLite veritabanında arşivlenir.
          </p>

          <div style={{ display: 'flex', gap: '10px' }}>
            <button
              onClick={refreshHistory}
              className="btn btn-secondary"
              style={{ width: '100%', padding: '12px' }}
            >
              🔄 Veri Akışını ve Listeyi Yenile
            </button>
            <Link href="/reports" className="btn btn-primary" style={{ width: '100%', padding: '12px', textAlign: 'center' }}>
              📋 Raporlara Git →
            </Link>
          </div>
        </div>

        <div className="section-card">
          <h3>📱 Kayıtlı Saha Terminalleri (3 Cihaz)</h3>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
            {devices.map(d => (
              <div key={d.id} className="damage-item" style={{ padding: '12px 16px' }}>
                <div>
                  <div style={{ fontWeight: '700', fontSize: '14px', color: 'var(--text-primary)' }}>{d.name}</div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>
                    Sürüm: {d.version} &bull; Batarya: {d.battery} &bull; Son İletişim: {d.lastSync}
                  </div>
                </div>
                <span className={`badge ${d.status === 'online' ? 'success' : 'neutral'}`}>
                  {d.status === 'online' ? '● Çevrimiçi' : '○ Beklemede'}
                </span>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Son Gelen Senkronizasyon Kayıtları */}
      <div className="section-card full-width">
        <h3>📋 Son Aktarılan Oturumlar</h3>
        {syncHistory.length === 0 ? (
          <div className="empty-state" style={{ padding: '20px' }}><p>Henüz aktarım yapılmadı.</p></div>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Plaka</th>
                <th>Müşteri</th>
                <th>Tarih</th>
                <th>Hasar Sayısı</th>
                <th>Senkronizasyon Durumu</th>
                <th>İşlem</th>
              </tr>
            </thead>
            <tbody>
              {syncHistory.map(s => (
                <tr key={s.id}>
                  <td className="plate">{s.plate_number}</td>
                  <td className="customer">{s.customer_name}</td>
                  <td style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>
                    {new Date(s.created_date).toLocaleDateString('tr-TR')}
                  </td>
                  <td>
                    {s.total_damage_count > 0 ? (
                      <span className="badge danger">{s.total_damage_count} hasar</span>
                    ) : (
                      <span className="badge success">Temiz</span>
                    )}
                  </td>
                  <td><span className="badge success">✓ Merkeze İletildi</span></td>
                  <td>
                    <Link href={`/vehicles/${s.id}`} className="btn btn-secondary btn-sm">
                      İncele →
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}
