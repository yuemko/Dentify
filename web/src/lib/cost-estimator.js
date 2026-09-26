// ========================================================
// DENTIFY — AKILLI ONARIM & SERVİS MALİYET MOTORU
// Türkiye otomotiv yetkili servis ve PDR (Boyasız Onarım)
// ortalama piyasa parça/işçilik fiyatlandırma modeli (2026).
// ========================================================

export const DAMAGE_COST_RATES = {
  0: { className: 'Çizik', baseLabor: 650, baseMaterial: 600, repairType: 'Pasta-Cila / Lokal Rötüş', unit: 'Parça Başı' },
  1: { className: 'Göçük', baseLabor: 1800, baseMaterial: 600, repairType: 'Boyasız Göçük Düzeltme (PDR)', unit: 'Bölge Başı' },
  2: { className: 'Çatlak / Yırtık', baseLabor: 2200, baseMaterial: 1600, repairType: 'Plastik Kaynağı & Fırın Boya', unit: 'Parça Başı' },
  3: { className: 'Eksik Parça', baseLabor: 1200, baseMaterial: 2300, repairType: 'Orijinal Parça Temini & Montaj', unit: 'Adet' },
  4: { className: 'Kırık Far', baseLabor: 1500, baseMaterial: 5000, repairType: 'Far Grubu Değişimi & Kalibrasyon', unit: 'Adet' },
  5: { className: 'Delik', baseLabor: 2500, baseMaterial: 2000, repairType: 'Saç Kaynağı, Macun & Boya', unit: 'Bölge' },
  6: { className: 'Kırık Cam', baseLabor: 2200, baseMaterial: 6000, repairType: 'Orijinal Cam Değişimi & Fitil', unit: 'Cam' },
};

/**
 * Tek bir hasar için tahmini onarım maliyetini hesaplar.
 */
export function estimateSingleDamageCost(classId, confidence = 0.8) {
  const rate = DAMAGE_COST_RATES[classId] || DAMAGE_COST_RATES[0];
  // Güven oranı ve şiddete göre küçük çarpan (%90 - %110)
  const multiplier = 0.9 + Math.min(0.2, (confidence - 0.5) * 0.4);

  const labor = Math.round(rate.baseLabor * multiplier);
  const material = Math.round(rate.baseMaterial * multiplier);
  const total = labor + material;

  return {
    classId,
    className: rate.className,
    repairType: rate.repairType,
    unit: rate.unit,
    labor,
    material,
    total,
  };
}

/**
 * Bir listedeki tüm hasarların toplam onarım maliyetini ve kırılımını hesaplar.
 */
export function estimateTotalDamageCost(damages = []) {
  let totalLabor = 0;
  let totalMaterial = 0;
  const items = [];

  damages.forEach((d) => {
    const classId = d.classId ?? d.class_id ?? 0;
    const conf = d.confidence ?? 0.8;
    const est = estimateSingleDamageCost(classId, conf);

    totalLabor += est.labor;
    totalMaterial += est.material;
    items.push({ ...est, angleName: d.angleName || d.angle_name || 'Genel' });
  });

  const grandTotal = totalLabor + totalMaterial;
  const kdv = Math.round(grandTotal * 0.20); // %20 KDV
  const grandTotalWithKdv = grandTotal + kdv;

  return {
    items,
    totalLabor,
    totalMaterial,
    grandTotal,
    kdv,
    grandTotalWithKdv,
  };
}

/**
 * Araç Sağlık / Kondisyon Skorunu (%0 - %100) hesaplar.
 */
export function calculateHealthScore(damages = []) {
  if (!damages || damages.length === 0) return 100;

  // Hasar sınıfı ağırlıkları
  const weights = {
    0: 3,  // Çizik (-3 puan)
    1: 6,  // Göçük (-6 puan)
    2: 10, // Çatlak (-10 puan)
    3: 12, // Eksik Parça (-12 puan)
    4: 15, // Kırık Far (-15 puan)
    5: 14, // Delik (-14 puan)
    6: 18, // Kırık Cam (-18 puan)
  };

  let penalty = 0;
  damages.forEach((d) => {
    const classId = d.classId ?? d.class_id ?? 0;
    penalty += weights[classId] || 5;
  });

  const score = Math.max(15, 100 - penalty);
  return score;
}

export function getHealthStatus(score) {
  if (score >= 90) return { label: 'Kusursuz / Mükemmel', variant: 'success', color: '#22c55e' };
  if (score >= 75) return { label: 'İyi Durumda', variant: 'info', color: '#38bdf8' };
  if (score >= 55) return { label: 'Orta / Küçük Hasarlar', variant: 'warning', color: '#f59e0b' };
  return { label: 'Servis / Onarım Gerekli', variant: 'danger', color: '#ef4444' };
}
