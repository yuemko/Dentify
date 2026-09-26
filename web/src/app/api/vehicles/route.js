import { NextResponse } from 'next/server';
import { getDb } from '@/lib/db';

// GET /api/vehicles — Tüm araç listesi
export async function GET() {
  try {
    const db = getDb();
    const sessions = db.prepare(`
      SELECT s.*, 
             r.id as report_id, r.total_new, r.total_existing, r.total_resolved,
             COUNT(DISTINCT c.id) as capture_count,
             COUNT(d.id) as damage_count
      FROM sessions s
      LEFT JOIN reports r ON r.session_id = s.id
      LEFT JOIN captures c ON c.session_id = s.id
      LEFT JOIN damages d ON d.capture_id = c.id
      GROUP BY s.id
      ORDER BY s.created_date DESC
    `).all();

    return NextResponse.json(sessions);
  } catch (error) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
