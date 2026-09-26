'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';

function formatDate(iso) {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

function formatPlate(plate) {
  if (!plate || plate.length < 5) return plate || '';
  const il = plate.slice(0, 2);
  let rest = plate.slice(2);
  let letters = '', digits = '', lettersDone = false;
  for (const c of rest) {
    if (!lettersDone && /[A-ZÇĞİÖŞÜ]/i.test(c)) letters += c;
    else { lettersDone = true; digits += c; }
  }
  return `${il} ${letters} ${digits}`.trim();
}

export default function ReportsPage() {
  const [sessions, setSessions] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetch('/api/sessions?status=tamamlandi')
      .then(r => r.json())
      .then(data => {
        setSessions(Array.isArray(data) ? data : []);
        setLoading(false);
      })
      .catch(() => {
        setSessions([]);
        setLoading(false);
      });
  }, []);

  return (
    <>
      <div className="page-header">
        <h2>Raporlar</h2>
        <p>Tamamlanmış kiralama raporlarını görüntüleyin, inceleyin ve PDF olarak indirin</p>
      </div>

      {loading ? (
        <div className="empty-state"><div className="empty-icon">⏳</div><p>Yükleniyor...</p></div>
      ) : sessions.length === 0 ? (
        <div className="empty-state"><div className="empty-icon">📋</div><p>Tamamlanmış rapor bulunamadı.</p></div>
      ) : (
        <div className="section-card">
          <table className="data-table">
            <thead>
              <tr>
                <th>Plaka</th>
                <th>Müşteri</th>
                <th>Kiralama Tarihi</th>
                <th>Teslim Tarihi</th>
                <th>Yeni Hasar Durumu</th>
                <th>Rapor İşlemleri</th>
              </tr>
            </thead>
            <tbody>
              {sessions.map(s => (
                <tr key={s.id}>
                  <td className="plate">{formatPlate(s.plate_number)}</td>
                  <td className="customer">{s.customer_name}</td>
                  <td style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>{formatDate(s.created_date)}</td>
                  <td style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>{formatDate(s.return_date)}</td>
                  <td>
                    {(s.new_damage_count || 0) > 0
                      ? <span className="badge danger">{s.new_damage_count} yeni hasar</span>
                      : <span className="badge success">Temiz (0 yeni hasar)</span>}
                  </td>
                  <td>
                    <div style={{ display: 'flex', gap: '8px' }}>
                      <Link href={`/reports/${s.id}`} className="btn btn-primary btn-sm">
                        📄 Raporu Aç & PDF
                      </Link>
                      <Link href={`/vehicles/${s.id}`} className="btn btn-secondary btn-sm">
                        Araç Detayı
                      </Link>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
