import { NextResponse } from 'next/server';
import { getDb } from '@/lib/db';
import fs from 'fs';
import path from 'path';

// POST /api/sync — Unity mobil uygulamasından gelen tüm oturum, fotoğraf ve hasar verilerini kaydet
export async function POST(request) {
  try {
    const db = getDb();
    const payload = await request.json();

    const {
      id = crypto.randomUUID(),
      plateNumber,
      customerName,
      status = 'tamamlandi',
      createdDate = new Date().toISOString(),
      returnDate = new Date().toISOString(),
      captures = [],
      report = null,
    } = payload;

    if (!plateNumber || !customerName) {
      return NextResponse.json({ error: 'Plaka ve müşteri adı zorunludur' }, { status: 400 });
    }

    const folderName = `${plateNumber}_${customerName.replace(/ /g, '_')}_${createdDate.slice(0, 10).replace(/-/g, '')}`;
    const uploadsDir = path.join(process.cwd(), 'public', 'uploads');

    if (!fs.existsSync(uploadsDir)) {
      fs.mkdirSync(uploadsDir, { recursive: true });
    }

    const syncTx = db.transaction(() => {
      // 1. Session kaydet veya güncelle
      db.prepare(`
        INSERT OR REPLACE INTO sessions (id, plate_number, customer_name, status, created_date, return_date, folder_name)
        VALUES (?, ?, ?, ?, ?, ?, ?)
      `).run(id, plateNumber, customerName, status, createdDate, returnDate, folderName);

      // Eski çekimleri temizle (varsa)
      const existingCaps = db.prepare('SELECT id FROM captures WHERE session_id = ?').all(id);
      for (const cap of existingCaps) {
        db.prepare('DELETE FROM damages WHERE capture_id = ?').run(cap.id);
      }
      db.prepare('DELETE FROM captures WHERE session_id = ?').run(id);

      // 2. Çekimleri ve hasarları işle
      for (const cap of captures) {
        let photoPath = cap.photoPath || '';

        // Base64 fotoğraf geldiyse diske kaydet
        if (cap.imageBase64 && cap.imageBase64.length > 50) {
          try {
            const fileName = `${id.slice(0, 8)}_${cap.phase || 'faz'}_${cap.angleId || 'on'}.png`;
            const filePath = path.join(uploadsDir, fileName);
            const base64Data = cap.imageBase64.replace(/^data:image\/\w+;base64,/, '');
            fs.writeFileSync(filePath, Buffer.from(base64Data, 'base64'));
            photoPath = `/uploads/${fileName}`;
          } catch (e) {
            console.error('Fotoğraf kayıt hatası:', e);
          }
        }

        const capResult = db.prepare(`
          INSERT INTO captures (session_id, phase, angle_id, angle_name, photo_path, mask_path, marked_path, timestamp)
          VALUES (?, ?, ?, ?, ?, ?, ?, ?)
        `).run(
          id,
          cap.phase || 'teslim_oncesi',
          cap.angleId || 'on',
          cap.angleName || 'Ön',
          photoPath,
          null,
          null,
          cap.timestamp || new Date().toISOString()
        );

        if (Array.isArray(cap.damages)) {
          for (const d of cap.damages) {
            db.prepare(`
              INSERT INTO damages (capture_id, class_id, class_name, confidence, center_x, center_y, width, height)
              VALUES (?, ?, ?, ?, ?, ?, ?, ?)
            `).run(
              capResult.lastInsertRowid,
              d.classId ?? 0,
              d.className || 'Hasar',
              d.confidence ?? 0.8,
              d.centerX ?? 500,
              d.centerY ?? 500,
              d.width ?? 100,
              d.height ?? 100
            );
          }
        }
      }

      // 3. Raporu kaydet
      if (report) {
        const reportId = report.id || crypto.randomUUID();
        db.prepare(`
          INSERT OR REPLACE INTO reports (id, session_id, total_new, total_existing, total_resolved, report_date, report_json)
          VALUES (?, ?, ?, ?, ?, ?, ?)
        `).run(
          reportId,
          id,
          report.totalNew ?? 0,
          report.totalExisting ?? 0,
          report.totalResolved ?? 0,
          report.reportDate || new Date().toISOString(),
          JSON.stringify(report)
        );
      }
    });

    syncTx();

    return NextResponse.json({
      success: true,
      sessionId: id,
      message: `${plateNumber} plakalı aracın fotoğrafları ve hasar detayları başarıyla senkronize edildi.`,
    });
  } catch (error) {
    console.error('Senkronizasyon hatası:', error);
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
