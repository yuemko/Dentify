'use client';

import { useState, useEffect } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import VehicleBlueprint from '@/components/VehicleBlueprint';
import { estimateTotalDamageCost, calculateHealthScore, getHealthStatus } from '@/lib/cost-estimator';

function formatDate(iso) {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  });
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

export default function ReportDetailPage() {
  const params = useParams();
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [activeModalImage, setActiveModalImage] = useState(null);

  useEffect(() => {
    fetch(`/api/reports/${params.id}`)
      .then(r => r.json())
      .then(d => { setData(d); setLoading(false); })
      .catch(() => setLoading(false));
  }, [params.id]);

  if (loading) return <div className="empty-state"><div className="empty-icon">⏳</div><p>Rapor yükleniyor...</p></div>;
  if (!data || !data.report) return <div className="empty-state"><div className="empty-icon">❌</div><p>Rapor bulunamadı.</p></div>;

  const { report, session, captures } = data;
  const beforeCaptures = captures.filter(c => c.phase === 'teslim_oncesi');
  const afterCaptures = captures.filter(c => c.phase === 'teslim_sonrasi');

  // Açı bazlı birleştirme
  const angleMap = new Map();

  beforeCaptures.forEach(c => {
    if (!angleMap.has(c.angle_id)) {
      angleMap.set(c.angle_id, { angleId: c.angle_id, angleName: c.angle_name, before: c, after: null });
    } else {
      angleMap.get(c.angle_id).before = c;
    }
  });

  afterCaptures.forEach(c => {
    if (!angleMap.has(c.angle_id)) {
      angleMap.set(c.angle_id, { angleId: c.angle_id, angleName: c.angle_name, before: null, after: c });
    } else {
      angleMap.get(c.angle_id).after = c;
    }
  });

  const anglesList = Array.from(angleMap.values());

  // Tüm yeni hasarların maliyet hesaplaması
  const newDamages = afterCaptures.flatMap(c => (c.damages || []).map(d => ({ ...d, angleName: c.angle_name })));
  const costEstimate = estimateTotalDamageCost(newDamages);
  const healthScore = calculateHealthScore(newDamages);
  const healthInfo = getHealthStatus(healthScore);

  return (
    <div className="report-container">
      {/* Üst İşlem Çubuğu (Yazdırmada Gizlenir) */}
      <div className="report-actions no-print">
        <Link href="/reports" className="btn btn-secondary">← Raporlara Dön</Link>
        <div style={{ display: 'flex', gap: '10px' }}>
          <button onClick={() => window.print()} className="btn btn-primary">
            🖨️ Yazdır / PDF Olarak Kaydet
          </button>
        </div>
      </div>

      {/* Rapor Belgesi (Mobil Raporun Birebir Aynısı - Premium Koyu Tema) */}
      <div className="report-paper-dark">
        {/* Antet Header */}
        <div className="rep-header">
          <div>
            <h1 className="rep-brand">DENTIFY</h1>
            <div className="rep-subtitle">Yapay Zeka Destekli Araç Hasar Tespit ve Ekspertiz Raporu</div>
          </div>
          <div className="rep-badge">KOSGEB / Teknokent Raporu</div>
        </div>

        {/* Araç & Kiralama Bilgileri */}
        <div className="rep-info-grid">
          <div className="rep-info-item">
            <span>PLAKA</span>
            <strong className="plate-cyan">{formatPlate(session?.plate_number)}</strong>
          </div>
          <div className="rep-info-item">
            <span>MÜŞTERİ ADI SOYADI</span>
            <strong>{session?.customer_name}</strong>
          </div>
          <div className="rep-info-item">
            <span>TESLİM ÖNCESİ ÇEKİM TARİHİ</span>
            <strong>{formatDate(session?.created_date)}</strong>
          </div>
          <div className="rep-info-item">
            <span>TESLİM SONRASI ÇEKİM TARİHİ</span>
            <strong>{formatDate(session?.return_date)}</strong>
          </div>
        </div>

        {/* 3'lü Büyük İstatistik Kartları + Sağlık Skoru */}
        <div className="rep-stats-grid">
          <div className="rep-stat-card stat-new">
            <div className="stat-val red">{report.total_new}</div>
            <div className="stat-lbl">YENİ HASAR</div>
          </div>
          <div className="rep-stat-card stat-exist">
            <div className="stat-val gray">{report.total_existing}</div>
            <div className="stat-lbl">ESKİ HASAR</div>
          </div>
          <div className="rep-stat-card stat-resolved">
            <div className="stat-val green">{report.total_resolved}</div>
            <div className="stat-lbl">ÇÖZÜLEN / ONARILAN</div>
          </div>
        </div>

        {/* 2D İnteraktif Araç Hasar Haritası (Sadece Ekranda Gösterilir, Yazdırmada Gizlenir) */}
        <div className="no-print" style={{ marginBottom: '30px' }}>
          <VehicleBlueprint 
            captures={afterCaptures.length > 0 ? afterCaptures : beforeCaptures}
            onSelectAngle={(key) => {
              const el = document.getElementById(`angle-${key}`);
              if (el) {
                el.scrollIntoView({ behavior: 'smooth', block: 'center' });
                el.classList.add('highlight-glow');
                setTimeout(() => el.classList.remove('highlight-glow'), 2500);
              }
            }}
          />
        </div>

        {/* Açı Bazlı Detaylı Hasar Listesi */}
        <h3 className="rep-section-title">AÇI BAZLI DETAYLI HASAR LİSTESİ</h3>

        {anglesList.length === 0 ? (
          <div className="empty-state" style={{ padding: '30px' }}><p>Çekim kaydı bulunamadı.</p></div>
        ) : (
          <div className="rep-angles-container">
            {anglesList.map((item, idx) => {
              const beforeDamages = item.before?.damages || [];
              const afterDamages = item.after?.damages || [];

              const beforeImg = item.before?.photo_path;
              const afterImg = item.after?.photo_path;

              return (
                <div key={idx} id={`angle-${item.angleId}`} className="rep-angle-card page-break-inside-avoid">
                  <div className="rep-angle-title">{item.angleName}</div>

                  {/* Hasar Rozetleri */}
                  <div className="rep-damage-tags">
                    {afterDamages.length === 0 && beforeDamages.length === 0 && (
                      <span className="damage-tag tag-clean">✓ HASAR TESPİT EDİLMEDİ (TEMİZ)</span>
                    )}

                    {afterDamages.map((d, i) => (
                      <span key={`after-${i}`} className="damage-tag tag-new">
                        [YENİ HASAR] {d.class_name} (%{Math.round(d.confidence * 100)})
                      </span>
                    ))}

                    {beforeDamages.map((d, i) => (
                      <span key={`before-${i}`} className="damage-tag tag-exist">
                        [ESKİ HASAR] {d.class_name} (%{Math.round(d.confidence * 100)})
                      </span>
                    ))}
                  </div>

                  {/* Yan Yana Büyük Fotoğraflar */}
                  <div className="rep-photo-duo">
                    {/* Teslim Öncesi */}
                    <div className="rep-photo-box">
                      <div className="photo-box-header">
                        📸 TESLİM ÖNCESİ FOTOĞRAF
                      </div>
                      <div
                        className="photo-img-wrap"
                        onClick={() => beforeImg && setActiveModalImage({ url: beforeImg, title: `${item.angleName} (Teslim Öncesi)` })}
                      >
                        {beforeImg ? (
                          <>
                            <img src={beforeImg} alt={`${item.angleName} öncesi`} />
                            <div className="zoom-hint">🔍 Büyütmek İçin Tıkla</div>
                          </>
                        ) : (
                          <div className="no-photo-placeholder">Görsel Bulunamadı</div>
                        )}
                      </div>
                    </div>

                    {/* Teslim Sonrası */}
                    <div className="rep-photo-box after-box">
                      <div className="photo-box-header cyan">
                        📸 TESLİM SONRASI FOTOĞRAF (YAPAY ZEKA TESPİT HARİTASI)
                      </div>
                      <div
                        className="photo-img-wrap"
                        onClick={() => afterImg && setActiveModalImage({ url: afterImg, title: `${item.angleName} (Teslim Sonrası AI Haritası)` })}
                      >
                        {afterImg ? (
                          <>
                            <img src={afterImg} alt={`${item.angleName} sonrası`} />
                            <div className="zoom-hint">🔍 Büyütmek İçin Tıkla</div>
                          </>
                        ) : (
                          <div className="no-photo-placeholder">Görsel Bulunamadı</div>
                        )}
                      </div>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}

        {/* 💸 Tahmini Servis & Onarım Masraf Dökümü */}
        {newDamages.length > 0 && (
          <div className="rep-cost-section page-break-inside-avoid" style={{ marginTop: '35px' }}>
            <h3 className="rep-section-title">💸 TAHMİNİ ONARIM VE SERVİS FATURASI (DEPOZİTO KESİNTİSİ)</h3>
            <div className="cost-table-wrap">
              <table className="cost-table">
                <thead>
                  <tr>
                    <th>Bölge / Açı</th>
                    <th>Hasar Türü</th>
                    <th>Önerilen Onarım Yöntemi</th>
                    <th>İşçilik</th>
                    <th>Malzeme</th>
                    <th>Toplam</th>
                  </tr>
                </thead>
                <tbody>
                  {costEstimate.items.map((item, i) => (
                    <tr key={i}>
                      <td><strong>{item.angleName}</strong></td>
                      <td><span className="damage-tag tag-new" style={{ fontSize: '11px', padding: '3px 8px' }}>{item.className}</span></td>
                      <td style={{ color: '#cbd5e1' }}>{item.repairType}</td>
                      <td>{item.labor.toLocaleString('tr-TR')} ₺</td>
                      <td>{item.material.toLocaleString('tr-TR')} ₺</td>
                      <td><strong>{item.total.toLocaleString('tr-TR')} ₺</strong></td>
                    </tr>
                  ))}
                  <tr className="cost-subtotal-row">
                    <td colSpan="5" style={{ textAlign: 'right', fontWeight: 700 }}>Ara Toplam:</td>
                    <td><strong>{costEstimate.grandTotal.toLocaleString('tr-TR')} ₺</strong></td>
                  </tr>
                  <tr className="cost-subtotal-row">
                    <td colSpan="5" style={{ textAlign: 'right', color: '#94a3b8' }}>Hesaplanan KDV (%20):</td>
                    <td>{costEstimate.kdv.toLocaleString('tr-TR')} ₺</td>
                  </tr>
                  <tr className="cost-grand-total-row">
                    <td colSpan="5" style={{ textAlign: 'right', fontSize: '15px', fontWeight: 800, color: '#38bdf8' }}>
                      ÖNERİLEN TOPLAM TAZMİN / ONARIM BEDELİ:
                    </td>
                    <td>
                      <strong style={{ fontSize: '17px', color: '#38bdf8' }}>
                        {costEstimate.grandTotalWithKdv.toLocaleString('tr-TR')} ₺
                      </strong>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* İmza ve Onay Alanı */}
        <div className="rep-signatures page-break-inside-avoid">
          <div className="rep-sig-box">
            <div className="sig-header">KİRALAYAN FİRMA ONAYI / İMZA</div>
            <div className="sig-content">
              <div className="sig-status">Dentify Yapay Zeka Onaylı</div>
              <div className="sig-date">{formatDate(report.report_date)}</div>
            </div>
          </div>

          <div className="rep-sig-box">
            <div className="sig-header">MÜŞTERİ / TESLİM ALAN İMZA</div>
            <div className="sig-content">
              <div className="sig-client-name">{session?.customer_name}</div>
              <div className="sig-line-mark">İmza: ____________________</div>
            </div>
          </div>
        </div>

        {/* Alt Bilgi Footer */}
        <div className="rep-footer">
          Dentify Edge AI Araç Hasar Tespit Sistemi &bull; Bu rapor yapay zeka tarafından otomatik oluşturulmuştur.
        </div>
      </div>

      {/* Büyük Fotoğraf İnceleme Modalı (Lightbox) */}
      {activeModalImage && (
        <div
          className="lightbox-overlay no-print"
          onClick={() => setActiveModalImage(null)}
        >
          <div className="lightbox-content" onClick={e => e.stopPropagation()}>
            <div className="lightbox-header">
              <h3>{activeModalImage.title}</h3>
              <button onClick={() => setActiveModalImage(null)} className="btn btn-secondary btn-sm">✕ Kapat</button>
            </div>
            <div className="lightbox-img-wrapper">
              <img src={activeModalImage.url} alt={activeModalImage.title} />
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
