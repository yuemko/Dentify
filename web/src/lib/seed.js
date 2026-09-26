// ========================================================
// DENTIFY WEB — DEMO VERİ OLUŞTURUCU
// İlk çalıştırmada veritabanını gerçekçi verilerle doldurur.
// ========================================================

const CLASS_NAMES = ['Çizik', 'Göçük', 'Çatlak / Yırtık', 'Eksik Parça', 'Kırık Far', 'Delik', 'Kırık Cam'];
const ANGLE_IDS = ['on', 'on_sag', 'sag', 'arka_sag', 'arka', 'arka_sol', 'sol', 'on_sol', 'tavan'];
const ANGLE_NAMES = ['Ön', 'Ön-Sağ Çeyrek', 'Sağ Yan', 'Arka-Sağ Çeyrek', 'Arka', 'Arka-Sol Çeyrek', 'Sol Yan', 'Ön-Sol Çeyrek', 'Tavan & Cam Tavan'];

const DEMO_IMAGE_FILES = [
  '/demo-photos/Dentify_34ABC2241_20260830_205659.png',
  '/demo-photos/Dentify_06ABG205_20260810_211750.png',
  '/demo-photos/Dentify_06BK29_20260814_102242.png',
  '/demo-photos/Dentify_06YUN91_20260814_101336.png',
  '/demo-photos/Dentify_124ASS132_20260810_210814.png',
  '/demo-photos/Dentify_12HB122_20260814_100656.png',
  '/demo-photos/Dentify_14AB23_20260814_093936.png',
  '/demo-photos/Dentify_21YNS24_20260814_102343.png',
  '/demo-photos/Dentify_23EMR05_20260810_212049.png',
  '/demo-photos/Dentify_23EN234_20260814_095634.png',
  '/demo-photos/Dentify_34AC242_20260824_110729.png',
  '/demo-photos/Dentify_82ENA48_20260814_102453.png',
  '/demo-photos/Dentify_82ENA48_20260830_205752.png',
];

function uuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = Math.random() * 16 | 0;
    return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
  });
}

function randomDate(daysAgo) {
  const d = new Date();
  d.setDate(d.getDate() - daysAgo);
  d.setHours(Math.floor(Math.random() * 14) + 8); // 08:00 - 22:00
  d.setMinutes(Math.floor(Math.random() * 60));
  return d.toISOString();
}

function randomConf() {
  return +(0.65 + Math.random() * 0.30).toFixed(2); // 0.65 - 0.95
}

function randomBox() {
  const cx = Math.floor(200 + Math.random() * 600);
  const cy = Math.floor(200 + Math.random() * 600);
  const w = Math.floor(80 + Math.random() * 300);
  const h = Math.floor(60 + Math.random() * 250);
  return { cx, cy, w, h };
}

const DEMO_SESSIONS = [
  { plate: '34ABC1234', customer: 'Ahmet Yılmaz', status: 'tamamlandi', daysAgo: 2, returnDaysAgo: 0, beforeDamages: [0, 1], afterDamages: [0, 1, 2], newDamages: 1, existingDamages: 2, resolvedDamages: 0 },
  { plate: '06ENA482', customer: 'Elif Demir', status: 'teslim_bekleniyor', daysAgo: 5, returnDaysAgo: null, beforeDamages: [1], afterDamages: [], newDamages: 0, existingDamages: 0, resolvedDamages: 0 },
  { plate: '35KRN567', customer: 'Kerem Öztürk', status: 'tamamlandi', daysAgo: 12, returnDaysAgo: 3, beforeDamages: [], afterDamages: [0, 4], newDamages: 2, existingDamages: 0, resolvedDamages: 0 },
  { plate: '01ADN321', customer: 'Selin Kaya', status: 'tamamlandi', daysAgo: 20, returnDaysAgo: 8, beforeDamages: [6, 1], afterDamages: [6, 1], newDamages: 0, existingDamages: 2, resolvedDamages: 0 },
  { plate: '21YNS240', customer: 'Yunus Arslan', status: 'teslim_bekleniyor', daysAgo: 3, returnDaysAgo: null, beforeDamages: [2, 5], afterDamages: [], newDamages: 0, existingDamages: 0, resolvedDamages: 0 },
  { plate: '16BRS899', customer: 'Burak Şahin', status: 'tamamlandi', daysAgo: 30, returnDaysAgo: 15, beforeDamages: [0], afterDamages: [0, 1, 3], newDamages: 2, existingDamages: 1, resolvedDamages: 0 },
  { plate: '34TKS007', customer: 'Ayşe Çelik', status: 'teslim_oncesi_devam', daysAgo: 0, returnDaysAgo: null, beforeDamages: [], afterDamages: [], newDamages: 0, existingDamages: 0, resolvedDamages: 0 },
  { plate: '55MRZ124', customer: 'Mert Yıldırım', status: 'tamamlandi', daysAgo: 45, returnDaysAgo: 25, beforeDamages: [1, 0, 2], afterDamages: [1, 0], newDamages: 0, existingDamages: 2, resolvedDamages: 1 },
];

export function seedDatabase(db) {
  const insertSession = db.prepare(`
    INSERT INTO sessions (id, plate_number, customer_name, status, created_date, return_date, folder_name)
    VALUES (?, ?, ?, ?, ?, ?, ?)
  `);

  const insertCapture = db.prepare(`
    INSERT INTO captures (session_id, phase, angle_id, angle_name, photo_path, mask_path, marked_path, timestamp)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?)
  `);

  const insertDamage = db.prepare(`
    INSERT INTO damages (capture_id, class_id, class_name, confidence, center_x, center_y, width, height)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?)
  `);

  const insertReport = db.prepare(`
    INSERT INTO reports (id, session_id, total_new, total_existing, total_resolved, report_date, report_json)
    VALUES (?, ?, ?, ?, ?, ?, ?)
  `);

  let imgIdx = 0;

  const seedTx = db.transaction(() => {
    // Önceki kayıtları temizle
    db.prepare('DELETE FROM damages').run();
    db.prepare('DELETE FROM captures').run();
    db.prepare('DELETE FROM reports').run();
    db.prepare('DELETE FROM sessions').run();

    for (const demo of DEMO_SESSIONS) {
      const sessionId = uuid();
      const createdDate = randomDate(demo.daysAgo);
      const returnDate = demo.returnDaysAgo !== null ? randomDate(demo.returnDaysAgo) : '';
      const folderName = `${demo.plate}_${demo.customer.replace(/ /g, '_')}_${new Date(createdDate).toISOString().slice(0,10).replace(/-/g,'')}`;

      insertSession.run(sessionId, demo.plate, demo.customer, demo.status, createdDate, returnDate, folderName);

      // Teslim öncesi çekimler
      if (demo.beforeDamages.length > 0 || demo.status !== 'teslim_oncesi_devam') {
        for (let a = 0; a < ANGLE_IDS.length; a++) {
          const capTimestamp = randomDate(demo.daysAgo);
          const photoPath = DEMO_IMAGE_FILES[imgIdx % DEMO_IMAGE_FILES.length];
          imgIdx++;

          const capResult = insertCapture.run(
            sessionId, 'teslim_oncesi', ANGLE_IDS[a], ANGLE_NAMES[a],
            photoPath, null, null, capTimestamp
          );

          if (a < demo.beforeDamages.length) {
            const box = randomBox();
            insertDamage.run(
              capResult.lastInsertRowid,
              demo.beforeDamages[a], CLASS_NAMES[demo.beforeDamages[a]],
              randomConf(), box.cx, box.cy, box.w, box.h
            );
          }
        }
      }

      // Teslim sonrası çekimler
      if (demo.status === 'tamamlandi' || demo.status === 'teslim_sonrasi_devam') {
        for (let a = 0; a < ANGLE_IDS.length; a++) {
          const capTimestamp = randomDate(demo.returnDaysAgo || 0);
          const photoPath = DEMO_IMAGE_FILES[imgIdx % DEMO_IMAGE_FILES.length];
          imgIdx++;

          const capResult = insertCapture.run(
            sessionId, 'teslim_sonrasi', ANGLE_IDS[a], ANGLE_NAMES[a],
            photoPath, null, null, capTimestamp
          );

          if (a < demo.afterDamages.length) {
            const box = randomBox();
            insertDamage.run(
              capResult.lastInsertRowid,
              demo.afterDamages[a], CLASS_NAMES[demo.afterDamages[a]],
              randomConf(), box.cx, box.cy, box.w, box.h
            );
          }
        }
      }

      // Tamamlanan oturumlar için rapor
      if (demo.status === 'tamamlandi') {
        const reportId = uuid();
        const reportJson = JSON.stringify({
          sessionId, plateNumber: demo.plate, customerName: demo.customer,
          totalNew: demo.newDamages, totalExisting: demo.existingDamages,
          totalResolved: demo.resolvedDamages,
          createdDate,
          returnDate,
          angleResults: ANGLE_IDS.map((id, i) => ({
            angleId: id,
            angleName: ANGLE_NAMES[i],
            newDamages: i < demo.newDamages ? [{ className: CLASS_NAMES[i % CLASS_NAMES.length], confidence: randomConf() }] : [],
            existingDamages: i < demo.existingDamages ? [{ className: CLASS_NAMES[(i+1) % CLASS_NAMES.length], confidence: randomConf() }] : [],
            resolvedDamages: i < demo.resolvedDamages ? [{ className: CLASS_NAMES[(i+2) % CLASS_NAMES.length], confidence: randomConf() }] : []
          }))
        });

        insertReport.run(reportId, sessionId, demo.newDamages, demo.existingDamages, demo.resolvedDamages, randomDate(demo.returnDaysAgo || 0), reportJson);
      }
    }
  });

  seedTx();
  console.log(`[Dentify Seed] ${DEMO_SESSIONS.length} demo oturum güncel fotoğraflarla oluşturuldu.`);
}
