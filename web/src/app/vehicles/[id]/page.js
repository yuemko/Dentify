'use client';

import { useState, useEffect } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import VehicleBlueprint from '@/components/VehicleBlueprint';
import { estimateTotalDamageCost, calculateHealthScore, getHealthStatus } from '@/lib/cost-estimator';

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

function formatDate(iso) {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export default function VehicleDetailPage() {
  const params = useParams();
  const [vehicle, setVehicle] = useState(null);
  const [loading, setLoading] = useState(true);
  const [selectedPhoto, setSelectedPhoto] = useState(null);

  useEffect(() => {
    fetch(`/api/vehicles/${params.id}`)
      .then(r => r.json())
      .then(d => { setVehicle(d); setLoading(false); })
      .catch(() => setLoading(false));
  }, [params.id]);

  if (loading) return <div className="empty-state"><div className="empty-icon">⏳</div><p>Yükleniyor...</p></div>;
  if (!vehicle) return <div className="empty-state"><div className="empty-icon">❌</div><p>Araç bulunamadı.</p></div>;

  const beforeCaptures = vehicle.captures?.filter(c => c.phase === 'teslim_oncesi') || [];
  const afterCaptures = vehicle.captures?.filter(c => c.phase === 'teslim_sonrasi') || [];
  const allDamages = vehicle.captures?.flatMap(c => (c.damages || []).map(d => ({ ...d, angleName: c.angle_name }))) || [];

  const costEstimate = estimateTotalDamageCost(allDamages);
  const healthScore = calculateHealthScore(allDamages);
  const healthInfo = getHealthStatus(healthScore);

  return (
    <div>
      {/* Üst Başlık & Geri Dön Butonu */}
      <div className="page-header">
        <div>
          <Link href="/vehicles" style={{ color: 'var(--text-muted)', fontSize: '13px', display: 'inline-block', marginBottom: '8px' }}>
            ← Filo Listesine Dön
          </Link>
          <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
            <h2 className="plate-cyan" style={{ fontSize: '28px' }}>{formatPlate(vehicle.plate_number)}</h2>
            <span className={`badge ${vehicle.status === 'tamamlandi' ? 'neutral' : 'warning'}`}>
              {vehicle.status === 'tamamlandi' ? 'Teslim Edildi' : 'Kirada'}
            </span>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
          {vehicle.report && (
            <Link href={`/reports/${vehicle.report.id || vehicle.id}`} className="btn btn-primary">
              📋 Resmi Hasar Raporunu Aç
            </Link>
          )}
        </div>
      </div>

      {/* 3'lü Özet Kartları & Sağlık Skoru */}
      <div className="kpi-grid" style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', marginBottom: '24px' }}>
        {/* Sağlık Skoru Kartı */}
        <div className="kpi-card" style={{ border: `1px solid ${healthInfo.color}40` }}>
          <div className="kpi-icon" style={{ background: `${healthInfo.color}20`, color: healthInfo.color }}>🛡️</div>
          <div className="kpi-content">
            <span className="kpi-value" style={{ color: healthInfo.color }}>%{healthScore}</span>
            <span className="kpi-label">Araç Kondisyon / Sağlık Endeksi</span>
            <span style={{ fontSize: '11px', color: healthInfo.color, fontWeight: 700, marginTop: '2px' }}>{healthInfo.label}</span>
          </div>
        </div>

        <div className="kpi-card">
          <div className="kpi-icon blue">👤</div>
          <div className="kpi-content">
            <span className="kpi-value" style={{ fontSize: '18px' }}>{vehicle.customer_name}</span>
            <span className="kpi-label">Kiracı / Müşteri Bilgisi</span>
          </div>
        </div>

        <div className="kpi-card">
          <div className="kpi-icon yellow">📅</div>
          <div className="kpi-content">
            <span className="kpi-value" style={{ fontSize: '16px' }}>{formatDate(vehicle.created_date)}</span>
            <span className="kpi-label">Kiralama Çıkış Tarihi</span>
          </div>
        </div>

        <div className="kpi-card">
          <div className="kpi-icon red">💰</div>
          <div className="kpi-content">
            <span className="kpi-value" style={{ color: allDamages.length > 0 ? '#ef4444' : '#22c55e' }}>
              {allDamages.length > 0 ? `${costEstimate.grandTotalWithKdv.toLocaleString('tr-TR')} ₺` : '0 ₺'}
            </span>
            <span className="kpi-label">Tahmini Onarım Bedeli</span>
          </div>
        </div>
      </div>

      {/* 2D İnteraktif Kuşbakışı Araç Hasar Haritası */}
      <div style={{ marginBottom: '24px' }}>
        <VehicleBlueprint 
          captures={afterCaptures.length > 0 ? afterCaptures : beforeCaptures}
          onSelectAngle={(key, info) => {
            if (info.photoPath) {
              setSelectedPhoto({
                photo_path: info.photoPath,
                angle_name: info.angleName,
                phase: 'teslim_sonrasi',
                damages: info.damages,
              });
            }
          }}
        />
      </div>

      {/* Hasar Dağılımı ve Onarım Maliyet Dökümü */}
      {allDamages.length > 0 && (
        <div className="section-card" style={{ marginBottom: '24px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <h3 style={{ margin: 0 }}>💸 Tespit Edilen Hasarlar ve Tahmini Servis Masrafları</h3>
            <span className="badge danger">{allDamages.length} Hasar Kayıtlı</span>
          </div>

          <div className="table-responsive">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Bölge / Açı</th>
                  <th>Hasar Sınıfı</th>
                  <th>Onarım Türü</th>
                  <th>İşçilik</th>
                  <th>Malzeme</th>
                  <th>Toplam Tutar</th>
                </tr>
              </thead>
              <tbody>
                {costEstimate.items.map((item, i) => (
                  <tr key={i}>
                    <td><strong>{item.angleName}</strong></td>
                    <td><span className="damage-tag tag-new" style={{ fontSize: '11px', padding: '3px 8px' }}>{item.className}</span></td>
                    <td style={{ color: 'var(--text-muted)' }}>{item.repairType}</td>
                    <td>{item.labor.toLocaleString('tr-TR')} ₺</td>
                    <td>{item.material.toLocaleString('tr-TR')} ₺</td>
                    <td><strong style={{ color: '#38bdf8' }}>{item.total.toLocaleString('tr-TR')} ₺</strong></td>
                  </tr>
                ))}
                <tr>
                  <td colSpan="5" style={{ textAlign: 'right', fontWeight: 800 }}>KDV Dahil Toplam Servis Bedeli:</td>
                  <td><strong style={{ fontSize: '16px', color: '#38bdf8' }}>{costEstimate.grandTotalWithKdv.toLocaleString('tr-TR')} ₺</strong></td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Teslim Öncesi / Sonrası Karşılaştırma & Fotoğraf Galerisi */}
      <div className="comparison-grid">
        {/* Teslim Öncesi */}
        <div className="comparison-panel before">
          <h4>📷 Teslim Öncesi ({beforeCaptures.length} açı)</h4>
          {beforeCaptures.length === 0 ? (
            <div className="empty-state" style={{ padding: '30px' }}><p>Çekim yok</p></div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              {beforeCaptures.map((c, i) => (
                <div
                  key={i}
                  className="damage-item"
                  style={{ cursor: 'pointer', padding: '12px 16px', transition: 'all 0.2s', border: '1px solid var(--border)' }}
                  onClick={() => setSelectedPhoto(c)}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
                    {c.photo_path ? (
                      <div style={{ position: 'relative', width: '64px', height: '64px', borderRadius: '8px', overflow: 'hidden', border: '1px solid var(--border-light)', flexShrink: 0, background: '#000' }}>
                        <img
                          src={c.photo_path}
                          alt={c.angle_name}
                          style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                        />
                        <div style={{ position: 'absolute', bottom: '2px', right: '2px', background: 'rgba(0,0,0,0.75)', fontSize: '9px', padding: '1px 3px', borderRadius: '3px', color: '#38bdf8' }}>🔍</div>
                      </div>
                    ) : (
                      <div style={{ width: '64px', height: '64px', borderRadius: '8px', background: 'var(--bg-secondary)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '11px', color: 'var(--text-muted)' }}>Görsel Yok</div>
                    )}
                    <div>
                      <div style={{ fontWeight: 700, fontSize: '15px' }}>{c.angle_name}</div>
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>Tıklayarak tam boy incele</div>
                    </div>
                  </div>
                  <span className={`badge ${c.damages?.length > 0 ? 'warning' : 'success'}`}>
                    {c.damages?.length > 0 ? `${c.damages.length} hasar` : 'Temiz'}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Teslim Sonrası */}
        <div className="comparison-panel after">
          <h4>📷 Teslim Sonrası ({afterCaptures.length} açı)</h4>
          {afterCaptures.length === 0 ? (
            <div className="empty-state" style={{ padding: '30px' }}><p>Çekim yok</p></div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              {afterCaptures.map((c, i) => (
                <div
                  key={i}
                  className="damage-item"
                  style={{ cursor: 'pointer', padding: '12px 16px', transition: 'all 0.2s', border: '1px solid var(--border)' }}
                  onClick={() => setSelectedPhoto(c)}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
                    {c.photo_path ? (
                      <div style={{ position: 'relative', width: '64px', height: '64px', borderRadius: '8px', overflow: 'hidden', border: '1px solid var(--border-light)', flexShrink: 0, background: '#000' }}>
                        <img
                          src={c.photo_path}
                          alt={c.angle_name}
                          style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                        />
                        <div style={{ position: 'absolute', bottom: '2px', right: '2px', background: 'rgba(0,0,0,0.75)', fontSize: '9px', padding: '1px 3px', borderRadius: '3px', color: '#38bdf8' }}>🔍</div>
                      </div>
                    ) : (
                      <div style={{ width: '64px', height: '64px', borderRadius: '8px', background: 'var(--bg-secondary)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '11px', color: 'var(--text-muted)' }}>Görsel Yok</div>
                    )}
                    <div>
                      <div style={{ fontWeight: 700, fontSize: '15px' }}>{c.angle_name}</div>
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>Tıklayarak tam boy incele</div>
                    </div>
                  </div>
                  <span className={`badge ${c.damages?.length > 0 ? 'danger' : 'success'}`}>
                    {c.damages?.length > 0 ? `${c.damages.length} hasar` : 'Temiz'}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Fotoğraf İnceleme Modalı (Sinema Modu) */}
      {selectedPhoto && (
        <div
          className="lightbox-overlay"
          onClick={() => setSelectedPhoto(null)}
        >
          <div
            className="lightbox-content"
            onClick={e => e.stopPropagation()}
          >
            <div className="lightbox-header">
              <h3>
                📷 {selectedPhoto.angle_name} ({selectedPhoto.phase === 'teslim_sonrasi' ? 'Teslim Sonrası AI Haritası' : 'Teslim Öncesi'})
              </h3>
              <button onClick={() => setSelectedPhoto(null)} className="btn btn-secondary btn-sm">✕ Kapat</button>
            </div>

            <div className="lightbox-img-wrapper">
              <img
                src={selectedPhoto.photo_path}
                alt={selectedPhoto.angle_name}
              />
            </div>

            {selectedPhoto.damages?.length > 0 && (
              <div style={{ padding: '12px 24px', background: '#0f172a', borderTop: '1px solid #334155', display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap', flexShrink: 0 }}>
                <span style={{ fontSize: '13px', color: 'var(--text-muted)', fontWeight: 700 }}>Tespit Edilen Hasarlar:</span>
                {selectedPhoto.damages.map((d, i) => (
                  <span key={i} className="damage-tag tag-new">
                    {d.class_name} (%{Math.round((d.confidence || 0.8) * 100)})
                  </span>
                ))}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
