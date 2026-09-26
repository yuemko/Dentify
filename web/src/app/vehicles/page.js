'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';

const STATUS_LABELS = {
  teslim_oncesi_devam: { text: 'Çekim Devam', variant: 'info' },
  teslim_bekleniyor: { text: 'Müşteride', variant: 'warning' },
  teslim_sonrasi_devam: { text: 'Teslim Çekimi', variant: 'info' },
  tamamlandi: { text: 'Tamamlandı', variant: 'success' },
};

const FILTERS = [
  { key: 'all', label: 'Tümü' },
  { key: 'teslim_bekleniyor', label: 'Müşteride' },
  { key: 'tamamlandi', label: 'Tamamlandı' },
  { key: 'teslim_oncesi_devam', label: 'Çekim Devam' },
];

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

export default function VehiclesPage() {
  const [sessions, setSessions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('all');

  useEffect(() => {
    const params = new URLSearchParams();
    if (filter !== 'all') params.set('status', filter);
    if (search) params.set('search', search);

    fetch(`/api/sessions?${params}`)
      .then(r => r.json())
      .then(data => {
        setSessions(Array.isArray(data) ? data : []);
        setLoading(false);
      })
      .catch(() => {
        setSessions([]);
        setLoading(false);
      });
  }, [search, filter]);

  const handleDelete = async (id) => {
    if (!confirm('Bu oturumu silmek istediğinize emin misiniz?')) return;
    await fetch(`/api/sessions/${id}`, { method: 'DELETE' });
    setSessions(sessions.filter(s => s.id !== id));
  };

  return (
    <>
      <div className="page-header">
        <h2>Araç Listesi</h2>
        <p>Tüm kiralama oturumlarını görüntüleyin ve yönetin</p>
      </div>

      <div className="toolbar">
        <div className="toolbar-left">
          <div className="search-bar" style={{ maxWidth: 360 }}>
            <span>🔍</span>
            <input
              type="text"
              placeholder="Plaka veya müşteri ara..."
              value={search}
              onChange={e => setSearch(e.target.value)}
            />
          </div>

          {FILTERS.map(f => (
            <button
              key={f.key}
              className={`filter-chip ${filter === f.key ? 'active' : ''}`}
              onClick={() => setFilter(f.key)}
            >
              {f.label}
            </button>
          ))}
        </div>
        <div className="toolbar-right">
          <span style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
            {sessions.length} kayıt
          </span>
        </div>
      </div>

      {loading ? (
        <div className="empty-state"><div className="empty-icon">⏳</div><p>Yükleniyor...</p></div>
      ) : sessions.length === 0 ? (
        <div className="empty-state"><div className="empty-icon">🚗</div><p>Kayıt bulunamadı.</p></div>
      ) : (
        <div className="section-card">
          <table className="data-table">
            <thead>
              <tr>
                <th>Plaka</th>
                <th>Müşteri</th>
                <th>Durum</th>
                <th>Kiralama Tarihi</th>
                <th>Teslim Tarihi</th>
                <th>Hasar</th>
                <th>İşlem</th>
              </tr>
            </thead>
            <tbody>
              {sessions.map(s => {
                const st = STATUS_LABELS[s.status] || { text: s.status, variant: 'neutral' };
                return (
                  <tr key={s.id}>
                    <td className="plate">{formatPlate(s.plate_number)}</td>
                    <td className="customer">{s.customer_name}</td>
                    <td><span className={`badge ${st.variant}`}>{st.text}</span></td>
                    <td style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>{formatDate(s.created_date)}</td>
                    <td style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>{formatDate(s.return_date)}</td>
                    <td>
                      {s.total_damage_count > 0 ? (
                        <span className="badge danger">{s.total_damage_count} hasar</span>
                      ) : (
                        <span className="badge success">Temiz</span>
                      )}
                    </td>
                    <td style={{ display: 'flex', gap: '6px' }}>
                      <Link href={`/vehicles/${s.id}`} className="btn btn-secondary btn-sm">Detay</Link>
                      <button onClick={() => handleDelete(s.id)} className="btn btn-danger btn-sm">Sil</button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
