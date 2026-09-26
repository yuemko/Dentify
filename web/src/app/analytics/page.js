'use client';

import { useState, useEffect } from 'react';

export default function AnalyticsPage() {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetch('/api/analytics')
      .then(r => r.json())
      .then(d => { setData(d); setLoading(false); })
      .catch(() => setLoading(false));
  }, []);

  if (loading) return <div className="empty-state"><div className="empty-icon">⏳</div><p>Analitik verileri hesaplanıyor...</p></div>;
  if (!data) return <div className="empty-state"><div className="empty-icon">❌</div><p>Veri yüklenemedi.</p></div>;

  const { branches, staffPerformance, costAnalysis, damageTypeCounts } = data;

  const totalEstimatedCost = branches.reduce((acc, b) => acc + b.estimatedDamageCost, 0);
  const totalFleetVehicles = branches.reduce((acc, b) => acc + b.totalVehicles, 0);
  const avgHealth = Math.round(branches.reduce((acc, b) => acc + b.healthAverage, 0) / branches.length);

  return (
    <div>
      <div className="page-header">
        <div>
          <h2>Şube & Operasyon Analitiği</h2>
          <p>Filo bazlı hasar maliyetleri, şube performansları ve saha personeli denetim istatistikleri</p>
        </div>
      </div>

      {/* 4'lü Üst KPI Kartları */}
      <div className="kpi-grid" style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', marginBottom: '28px' }}>
        <div className="kpi-card">
          <div className="kpi-icon blue">💰</div>
          <div className="kpi-content">
            <span className="kpi-value">{Math.round(totalEstimatedCost).toLocaleString('tr-TR')} ₺</span>
            <span className="kpi-label">Toplam Tahmini Hasar & Onarım Tutarı</span>
          </div>
        </div>

        <div className="kpi-card">
          <div className="kpi-icon green">🛡️</div>
          <div className="kpi-content">
            <span className="kpi-value">%{avgHealth}</span>
            <span className="kpi-label">Filo Genel Sağlık Endeksi</span>
          </div>
        </div>

        <div className="kpi-card">
          <div className="kpi-icon yellow">🚗</div>
          <div className="kpi-content">
            <span className="kpi-value">{totalFleetVehicles} Araç</span>
            <span className="kpi-label">4 Aktif Havalimanı Şubesi</span>
          </div>
        </div>

        <div className="kpi-card">
          <div className="kpi-icon red">🎯</div>
          <div className="kpi-content">
            <span className="kpi-value">%99.1</span>
            <span className="kpi-label">AI Hasar Tespit Doğruluk Skoru</span>
          </div>
        </div>
      </div>

      {/* Şube Hasar ve Maliyet Karşılaştırması */}
      <div className="section-card" style={{ marginBottom: '28px' }}>
        <h3>🏢 Şube Bazlı Hasar & Maliyet Performansı</h3>
        <p style={{ color: 'var(--text-muted)', fontSize: '13px', marginBottom: '20px' }}>
          Hangi şubede ne kadar hasar tespit edildiğini ve tahmini onarım bütçesini takip edin.
        </p>

        <div className="table-responsive">
          <table className="data-table">
            <thead>
              <tr>
                <th>Şube Adı</th>
                <th>Toplam Araç</th>
                <th>Aktif Kirada</th>
                <th>Tespit Edilen Hasar</th>
                <th>Şube Sağlık Puanı</th>
                <th>Tahmini Hasar Maliyeti</th>
                <th>Durum</th>
              </tr>
            </thead>
            <tbody>
              {branches.map(b => (
                <tr key={b.id}>
                  <td><strong>{b.name}</strong></td>
                  <td>{b.totalVehicles} araç</td>
                  <td><span className="badge warning">{b.activeRentals} kirada</span></td>
                  <td><strong style={{ color: '#ef4444' }}>{b.totalDamagesCount} adet</strong></td>
                  <td>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <div style={{ flex: 1, height: '6px', background: 'var(--bg-secondary)', borderRadius: '3px', overflow: 'hidden', minWidth: '60px' }}>
                        <div style={{ width: `${b.healthAverage}%`, height: '100%', background: b.healthAverage >= 90 ? '#22c55e' : '#f59e0b' }}></div>
                      </div>
                      <span style={{ fontSize: '12px', fontWeight: 700 }}>%{b.healthAverage}</span>
                    </div>
                  </td>
                  <td>
                    <strong style={{ color: '#38bdf8' }}>{Math.round(b.estimatedDamageCost).toLocaleString('tr-TR')} ₺</strong>
                  </td>
                  <td>
                    <span className="badge success">Operasyonel</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* İki Kolonlu Alt Analitik: Personel Performansı & Hasar Türü Dağılımı */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(400px, 1fr))', gap: '24px' }}>
        {/* Saha Denetmenleri Performansı */}
        <div className="section-card">
          <h3>👥 Saha Personeli Denetim & Doğruluk Skoru</h3>
          <p style={{ color: 'var(--text-muted)', fontSize: '13px', marginBottom: '16px' }}>
            Tabletle teslimat yapan personelin denetim hacmi ve AI destekli başarı oranı
          </p>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            {staffPerformance.map(s => (
              <div key={s.id} className="damage-item" style={{ padding: '12px 16px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                  <div className="user-avatar-mini">{s.name[0]}</div>
                  <div>
                    <div style={{ fontWeight: 700 }}>{s.name}</div>
                    <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>{s.role} &bull; Şube: {s.branch}</div>
                  </div>
                </div>

                <div style={{ textAlign: 'right' }}>
                  <div style={{ fontWeight: 800, color: '#22c55e' }}>%{s.accuracyScore} Doğruluk</div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>{s.inspectionsCount} Çekim &bull; {s.damagesCaught} Hasar</div>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Hasar Türü Dağılım Grafiği */}
        <div className="section-card">
          <h3>📊 Hasar Türü & Servis Dağılımı</h3>
          <p style={{ color: 'var(--text-muted)', fontSize: '13px', marginBottom: '16px' }}>
            Filoda en sık karşılaşılan hasar kategorileri
          </p>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
            {Object.entries(damageTypeCounts).map(([type, count], i) => {
              const maxCount = Math.max(...Object.values(damageTypeCounts), 5);
              const pct = Math.round((count / maxCount) * 100);
              return (
                <div key={i}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', marginBottom: '4px' }}>
                    <span style={{ fontWeight: 600 }}>{type}</span>
                    <span style={{ color: 'var(--text-muted)' }}>{count} tespit</span>
                  </div>
                  <div style={{ height: '8px', background: 'var(--bg-secondary)', borderRadius: '4px', overflow: 'hidden' }}>
                    <div style={{ width: `${Math.max(8, pct)}%`, height: '100%', background: i % 2 === 0 ? '#38bdf8' : '#ef4444' }}></div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
}
