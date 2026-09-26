import { NextResponse } from 'next/server';
import { getDb } from '@/lib/db';

// GET /api/sessions — Tüm oturumları listele
export async function GET(request) {
  const db = getDb();
  const { searchParams } = new URL(request.url);
  const status = searchParams.get('status');
  const search = searchParams.get('search');

  let query = `
    SELECT s.*,
      (SELECT COUNT(*) FROM captures c JOIN damages d ON d.capture_id = c.id WHERE c.session_id = s.id) as total_damage_count,
      (SELECT total_new FROM reports r WHERE r.session_id = s.id LIMIT 1) as new_damage_count
    FROM sessions s
    WHERE 1=1
  `;
  const params = [];

  if (status && status !== 'all') {
    query += ' AND s.status = ?';
    params.push(status);
  }

  if (search) {
    query += ' AND (s.plate_number LIKE ? OR s.customer_name LIKE ?)';
    params.push(`%${search}%`, `%${search}%`);
  }

  query += ' ORDER BY s.created_date DESC';

  const sessions = db.prepare(query).all(...params);
  return NextResponse.json(sessions);
}

// POST /api/sessions — Yeni oturum oluştur
export async function POST(request) {
  const db = getDb();
  const body = await request.json();

  const id = crypto.randomUUID();
  const createdDate = new Date().toISOString();
  const folderName = `${body.plate_number}_${body.customer_name.replace(/ /g, '_')}_${createdDate.slice(0,10).replace(/-/g,'')}`;

  db.prepare(`
    INSERT INTO sessions (id, plate_number, customer_name, status, created_date, return_date, folder_name)
    VALUES (?, ?, ?, 'teslim_oncesi_devam', ?, '', ?)
  `).run(id, body.plate_number, body.customer_name, createdDate, folderName);

  const session = db.prepare('SELECT * FROM sessions WHERE id = ?').get(id);
  return NextResponse.json(session, { status: 201 });
}
