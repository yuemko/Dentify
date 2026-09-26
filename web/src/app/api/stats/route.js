import { NextResponse } from 'next/server';
import { getDb } from '@/lib/db';

// GET /api/stats — Dashboard verileri
export async function GET() {
  const db = getDb();

  const totalSessions = db.prepare('SELECT COUNT(*) as c FROM sessions').get().c;
  const activeRentals = db.prepare("SELECT COUNT(*) as c FROM sessions WHERE status IN ('teslim_bekleniyor', 'teslim_oncesi_devam', 'teslim_sonrasi_devam')").get().c;
  const completedRentals = db.prepare("SELECT COUNT(*) as c FROM sessions WHERE status = 'tamamlandi'").get().c;

  // Toplam hasar istatistikleri
  const totalDamages = db.prepare('SELECT COUNT(*) as c FROM damages').get().c;
  const totalNewDamages = db.prepare('SELECT COALESCE(SUM(total_new), 0) as c FROM reports').get().c;
  const totalResolvedDamages = db.prepare('SELECT COALESCE(SUM(total_resolved), 0) as c FROM reports').get().c;

  // Sınıf bazlı hasar dağılımı
  const damagesByClass = db.prepare(`
    SELECT class_name, COUNT(*) as count
    FROM damages
    GROUP BY class_name
    ORDER BY count DESC
  `).all();

  // Son 10 aktivite
  const recentSessions = db.prepare(`
    SELECT id, plate_number, customer_name, status, created_date, return_date
    FROM sessions
    ORDER BY created_date DESC
    LIMIT 10
  `).all();

  // Aylık hasar trendi (son 6 ay)
  const monthlyTrend = db.prepare(`
    SELECT
      strftime('%Y-%m', c.timestamp) as month,
      COUNT(d.id) as damage_count
    FROM captures c
    JOIN damages d ON d.capture_id = c.id
    WHERE c.timestamp >= date('now', '-6 months')
    GROUP BY month
    ORDER BY month ASC
  `).all();

  return NextResponse.json({
    kpi: {
      totalSessions,
      activeRentals,
      completedRentals,
      totalDamages,
      totalNewDamages,
      totalResolvedDamages,
    },
    damagesByClass,
    recentSessions,
    monthlyTrend,
  });
}
