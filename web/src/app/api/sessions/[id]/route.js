import { NextResponse } from 'next/server';
import { getDb } from '@/lib/db';

// GET /api/sessions/[id] — Tek oturum detayı + çekimler + hasarlar
export async function GET(request, { params }) {
  const db = getDb();
  const { id } = await params;

  const session = db.prepare('SELECT * FROM sessions WHERE id = ?').get(id);
  if (!session) {
    return NextResponse.json({ error: 'Oturum bulunamadı' }, { status: 404 });
  }

  // Çekimler ve hasarlar
  const captures = db.prepare(`
    SELECT c.*, GROUP_CONCAT(d.id) as damage_ids
    FROM captures c
    LEFT JOIN damages d ON d.capture_id = c.id
    WHERE c.session_id = ?
    GROUP BY c.id
    ORDER BY c.phase, c.angle_id
  `).all(id);

  // Her çekim için hasarlarını al
  const capturesWithDamages = captures.map(cap => {
    const damages = db.prepare('SELECT * FROM damages WHERE capture_id = ?').all(cap.id);
    return { ...cap, damages };
  });

  // Rapor
  const report = db.prepare('SELECT * FROM reports WHERE session_id = ?').get(id);

  return NextResponse.json({
    session,
    captures: capturesWithDamages,
    report: report || null,
  });
}

// DELETE /api/sessions/[id] — Oturum sil
export async function DELETE(request, { params }) {
  const db = getDb();
  const { id } = await params;

  db.prepare('DELETE FROM sessions WHERE id = ?').run(id);
  return NextResponse.json({ success: true });
}
