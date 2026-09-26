import { NextResponse } from 'next/server';
import { getDb } from '@/lib/db';

// GET /api/reports/[id] — Tek rapor ve ilişkili oturum bilgileri
export async function GET(request, { params }) {
  const db = getDb();
  const { id } = await params;

  let report = db.prepare('SELECT * FROM reports WHERE id = ?').get(id);
  if (!report) {
    report = db.prepare('SELECT * FROM reports WHERE session_id = ?').get(id);
  }
  if (!report) {
    // Plakadan session bul
    const sess = db.prepare('SELECT id FROM sessions WHERE plate_number = ?').get(id);
    if (sess) {
      report = db.prepare('SELECT * FROM reports WHERE session_id = ?').get(sess.id);
    }
  }

  if (!report) {
    return NextResponse.json({ error: 'Rapor bulunamadı' }, { status: 404 });
  }

  const session = db.prepare('SELECT * FROM sessions WHERE id = ?').get(report.session_id);

  const captures = db.prepare(`
    SELECT c.*
    FROM captures c
    WHERE c.session_id = ?
    ORDER BY c.phase, c.angle_id
  `).all(report.session_id);

  const capturesWithDamages = captures.map(cap => {
    const damages = db.prepare('SELECT * FROM damages WHERE capture_id = ?').all(cap.id);
    return { ...cap, damages };
  });

  return NextResponse.json({
    report,
    session,
    captures: capturesWithDamages,
  });
}
