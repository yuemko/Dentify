import Database from 'better-sqlite3';
import path from 'path';
import { seedDatabase } from './seed';

let db = null;

export function getDb() {
  if (db) return db;

  const dbPath = path.join(process.cwd(), 'dentify.db');
  db = new Database(dbPath);

  // WAL modu — performans ve eş zamanlılık
  db.pragma('journal_mode = WAL');
  db.pragma('foreign_keys = ON');

  // Şemayı oluştur
  initSchema(db);

  // Demo veri kontrolü
  const count = db.prepare('SELECT COUNT(*) as c FROM sessions').get();
  if (count.c === 0) {
    seedDatabase(db);
  }

  return db;
}

function initSchema(db) {
  db.exec(`
    CREATE TABLE IF NOT EXISTS sessions (
      id TEXT PRIMARY KEY,
      plate_number TEXT NOT NULL,
      customer_name TEXT NOT NULL,
      status TEXT DEFAULT 'teslim_oncesi_devam',
      created_date TEXT,
      return_date TEXT,
      folder_name TEXT
    );

    CREATE TABLE IF NOT EXISTS captures (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      session_id TEXT REFERENCES sessions(id) ON DELETE CASCADE,
      phase TEXT,
      angle_id TEXT,
      angle_name TEXT,
      photo_path TEXT,
      mask_path TEXT,
      marked_path TEXT,
      timestamp TEXT
    );

    CREATE TABLE IF NOT EXISTS damages (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      capture_id INTEGER REFERENCES captures(id) ON DELETE CASCADE,
      class_id INTEGER,
      class_name TEXT,
      confidence REAL,
      center_x REAL,
      center_y REAL,
      width REAL,
      height REAL
    );

    CREATE TABLE IF NOT EXISTS reports (
      id TEXT PRIMARY KEY,
      session_id TEXT REFERENCES sessions(id) ON DELETE CASCADE,
      total_new INTEGER DEFAULT 0,
      total_existing INTEGER DEFAULT 0,
      total_resolved INTEGER DEFAULT 0,
      report_date TEXT,
      report_json TEXT
    );
  `);
}
