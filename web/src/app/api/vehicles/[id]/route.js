import { NextResponse } from 'next/server';
import { getDb } from '@/lib/db';

// GET /api/vehicles/[id] — ID veya Plaka ile tek araç detayı + çekimler + hasarlar
export async function GET(request, { params }) {
  try {
    const db = getDb();
    const { id } = await params;

    // 1. Session bul (önce id ile, bulamazsa plate_number ile)
    let session = db.prepare('SELECT * FROM sessions WHERE id = ?').get(id);
    if (!session) {
      session = db.prepare('SELECT * FROM sessions WHERE plate_number = ?').get(id);
    }
    if (!session) {
      // Cleaned plate comparison (boşluksuz)
      session = db.prepare(`SELECT * FROM sessions WHERE REPLACE(plate_number, ' ', '') = ?`).get(id.replace(/ /g, ''));
    }

    if (!session) {
      return NextResponse.json({ error: 'Araç bulunamadı' }, { status: 404 });
    }

    // 2. Çekimler ve hasarlar
    const captures = db.prepare(`
      SELECT * FROM captures WHERE session_id = ? ORDER BY phase, angle_id
    `).all(session.id);

    const capturesWithDamages = captures.map(cap => {
      const damages = db.prepare('SELECT * FROM damages WHERE capture_id = ?').all(cap.id);
      return { ...cap, damages };
    });

    // 3. Rapor
    const report = db.prepare('SELECT * FROM reports WHERE session_id = ?').get(session.id);

    return NextResponse.json({
      ...session,
      captures: capturesWithDamages,
      report: report || null,
    });
  } catch (error) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
