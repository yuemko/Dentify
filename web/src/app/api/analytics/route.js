import { NextResponse } from 'next/server';
import { getDb } from '@/lib/db';
import { estimateTotalDamageCost } from '@/lib/cost-estimator';

export async function GET() {
  try {
    const db = getDb();

    // 1. Tüm hasarları çek
    const allDamages = db.prepare(`
      SELECT d.*, c.phase, c.angle_name, s.plate_number, s.customer_name
      FROM damages d
      JOIN captures c ON d.capture_id = c.id
      JOIN sessions s ON c.session_id = s.id
    `).all();

    // 2. Maliyet analizi
    const costAnalysis = estimateTotalDamageCost(allDamages);

    // 3. Şube bazlı istatistikler (Simüle edilmiş ve gerçek veriler harmanlı)
    const branches = [
      {
        id: 'ist',
        name: 'İstanbul Havalimanı (İST)',
        totalVehicles: 48,
        activeRentals: 34,
        totalDamagesCount: allDamages.filter(d => d.phase === 'teslim_sonrasi').length + 14,
        estimatedDamageCost: costAnalysis.grandTotalWithKdv * 0.45 + 18500,
        staffCount: 6,
        healthAverage: 88,
      },
      {
        id: 'saw',
        name: 'Sabiha Gökçen (SAW)',
        totalVehicles: 36,
        activeRentals: 22,
        totalDamagesCount: 9,
        estimatedDamageCost: costAnalysis.grandTotalWithKdv * 0.25 + 12000,
        staffCount: 4,
        healthAverage: 92,
      },
      {
        id: 'esb',
        name: 'Ankara Esenboğa (ESB)',
        totalVehicles: 24,
        activeRentals: 15,
        totalDamagesCount: 6,
        estimatedDamageCost: costAnalysis.grandTotalWithKdv * 0.18 + 7500,
        staffCount: 3,
        healthAverage: 94,
      },
      {
        id: 'adb',
        name: 'İzmir Adnan Menderes (ADB)',
        totalVehicles: 18,
        activeRentals: 11,
        totalDamagesCount: 4,
        estimatedDamageCost: costAnalysis.grandTotalWithKdv * 0.12 + 5000,
        staffCount: 2,
        healthAverage: 96,
      },
    ];

    // 4. Saha Personeli Performansı
    const staffPerformance = [
      { id: 1, name: 'Ahmet Yılmaz', branch: 'İST', role: 'Saha Denetmeni', inspectionsCount: 142, damagesCaught: 28, accuracyScore: 99.4 },
      { id: 2, name: 'Selin Demir', branch: 'SAW', role: 'Operasyon Sorumlusu', inspectionsCount: 118, damagesCaught: 19, accuracyScore: 98.8 },
      { id: 3, name: 'Burak Şahin', branch: 'ESB', role: 'Saha Yetkilisi', inspectionsCount: 84, damagesCaught: 12, accuracyScore: 97.9 },
      { id: 4, name: 'Merve Aydın', branch: 'ADB', role: 'Saha Yetkilisi', inspectionsCount: 62, damagesCaught: 8, accuracyScore: 99.1 },
    ];

    // 5. En çok tespit edilen hasar türleri
    const damageTypeCounts = {
      'Çizik': 0,
      'Göçük': 0,
      'Çatlak / Yırtık': 0,
      'Kırık Far': 0,
      'Kırık Cam': 0,
      'Diğer': 0,
    };

    allDamages.forEach(d => {
      const name = d.class_name || 'Diğer';
      if (damageTypeCounts[name] !== undefined) {
        damageTypeCounts[name]++;
      } else {
        damageTypeCounts['Diğer']++;
      }
    });

    return NextResponse.json({
      totalDamagesCount: allDamages.length,
      costAnalysis,
      branches,
      staffPerformance,
      damageTypeCounts,
    });
  } catch (error) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
