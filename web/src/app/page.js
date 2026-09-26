'use client';

import { useState, useEffect } from 'react';
import DashboardCards from '@/components/DashboardCards';
import { DamageDistributionChart, MonthlyTrendChart } from '@/components/DamageChart';
import Link from 'next/link';

const STATUS_LABELS = {
  teslim_oncesi_devam: { text: 'Çekim Devam', variant: 'info' },
  teslim_bekleniyor: { text: 'Müşteride', variant: 'warning' },
  teslim_sonrasi_devam: { text: 'Teslim Çekimi', variant: 'info' },
  tamamlandi: { text: 'Tamamlandı', variant: 'success' },
};

function formatDate(iso) {
  if (!iso) return '—';
  const d = new Date(iso);
  return d.toLocaleDateString('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

function formatPlate(plate) {
  if (!plate || plate.length < 5) return plate || '';
  const il = plate.slice(0, 2);
  let rest = plate.slice(2);
  let letters = '';
  let digits = '';
  let lettersDone = false;
  for (const c of rest) {
    if (!lettersDone && /[A-ZÇĞİÖŞÜ]/i.test(c)) letters += c;
    else { lettersDone = true; digits += c; }
  }
  return `${il} ${letters} ${digits}`.trim();
}

export default function DashboardPage() {
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetch('/api/stats')
      .then(r => r.json())
      .then(data => { setStats(data); setLoading(false); })
      .catch(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div className="empty-state">
        <div className="empty-icon">⏳</div>
        <p>Dashboard yükleniyor...</p>
      </div>
    );
  }

  if (!stats) {
    return (
      <div className="empty-state">
        <div className="empty-icon">⚠️</div>
        <p>Veriler yüklenemedi.</p>
      </div>
    );
  }

  return (
    <>
      <div className="page-header">
        <div>
          <h2>Filo Operasyon Dashboard</h2>
          <p>Yapay zeka destekli filo hasar takibi, kondisyon analizleri ve anlık saha operasyonları</p>
        </div>

        <div style={{ display: 'flex', gap: '10px' }}>
          <Link href="/reports" className="btn btn-secondary">
            📋 Rapor Arşivi
          </Link>
          <Link href="/analytics" className="btn btn-primary">
            📈 Şube Analitiği
          </Link>
        </div>
      </div>

      <DashboardCards kpi={stats.kpi} />

      <div className="section-grid">
        <div className="section-card">
          <h3>📊 Hasar Sınıf Dağılımı</h3>
          <DamageDistributionChart data={stats.damagesByClass} />
        </div>
        <div className="section-card">
          <h3>📈 Aylık Hasar Trendi</h3>
          <MonthlyTrendChart data={stats.monthlyTrend} />
        </div>
      </div>

      <div className="section-card full-width">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
          <h3 style={{ margin: 0 }}>🕐 Son Saha Aktiviteleri & Teslimatlar</h3>
          <Link href="/vehicles" style={{ fontSize: '13px', color: 'var(--accent-cyan)', fontWeight: 600 }}>Tümünü Gör →</Link>
        </div>

        <div className="table-responsive">
          <table className="data-table">
            <thead>
              <tr>
                <th>Plaka</th>
                <th>Kiracı / Müşteri</th>
                <th>Durum</th>
                <th>Tarih</th>
                <th>İşlem</th>
              </tr>
            </thead>
            <tbody>
              {stats.recentSessions.map((s) => {
                const st = STATUS_LABELS[s.status] || { text: s.status, variant: 'neutral' };
                return (
                  <tr key={s.id}>
                    <td>
                      <strong className="plate-cyan">{formatPlate(s.plate_number)}</strong>
                    </td>
                    <td>{s.customer_name}</td>
                    <td>
                      <span className={`badge ${st.variant}`}>{st.text}</span>
                    </td>
                    <td>{formatDate(s.created_date)}</td>
                    <td>
                      <div style={{ display: 'flex', gap: '8px' }}>
                        <Link href={`/vehicles/${s.id}`} className="btn btn-sm btn-secondary">
                          Araç Detayı
                        </Link>
                        {s.status === 'tamamlandi' && (
                          <Link href={`/reports/${s.id}`} className="btn btn-sm btn-primary">
                            Rapor Aç
                          </Link>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </>
  );
}
