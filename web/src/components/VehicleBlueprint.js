'use client';

import { useState } from 'react';

// 8 + 1 açının araç kuşbakışı SVG koordinatları (X, Y yüzde cinsinden)
const ANGLE_COORDINATES = {
  on: { x: 50, y: 12, label: 'Ön' },
  on_sag: { x: 78, y: 22, label: 'Ön-Sağ' },
  sag: { x: 84, y: 50, label: 'Sağ Yan' },
  arka_sag: { x: 78, y: 78, label: 'Arka-Sağ' },
  arka: { x: 50, y: 88, label: 'Arka' },
  arka_sol: { x: 22, y: 78, label: 'Arka-Sol' },
  sol: { x: 16, y: 50, label: 'Sol Yan' },
  on_sol: { x: 22, y: 22, label: 'Ön-Sol' },
  tavan: { x: 50, y: 50, label: 'Tavan & Cam' },
};

export default function VehicleBlueprint({ captures = [], onSelectAngle = null }) {
  const [activeHover, setActiveHover] = useState(null);

  // Açılara göre hasarları ve fotoğrafları haritala
  const statusByAngle = {};
  Object.keys(ANGLE_COORDINATES).forEach(k => {
    statusByAngle[k] = { damages: [], hasDamage: false, photoPath: null, angleName: ANGLE_COORDINATES[k].label };
  });

  captures.forEach(c => {
    const aid = c.angle_id || c.angleId;
    if (statusByAngle[aid]) {
      const damages = c.damages || [];
      if (damages.length > 0) {
        statusByAngle[aid].hasDamage = true;
        statusByAngle[aid].damages.push(...damages);
      }
      if (c.photo_path) statusByAngle[aid].photoPath = c.photo_path;
    }
  });

  return (
    <div className="blueprint-wrapper">
      <div className="blueprint-header">
        <div className="blueprint-title">
          <span>🗺️ 2D İnteraktif Araç Hasar Haritası</span>
          <span className="blueprint-legend">
            <span className="legend-item"><span className="dot red-dot"></span> Hasarlı Bölge</span>
            <span className="legend-item"><span className="dot green-dot"></span> Temiz Bölge</span>
          </span>
        </div>
      </div>

      <div className="blueprint-stage">
        {/* Kuşbakışı Araç SVG Silüeti */}
        <svg viewBox="0 0 400 600" className="car-svg">
          <defs>
            {/* Gövde Gradyanı */}
            <linearGradient id="carBodyGrad" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stopColor="#1e293b" />
              <stop offset="50%" stopColor="#0f172a" />
              <stop offset="100%" stopColor="#162032" />
            </linearGradient>

            {/* Cam Gradyanı */}
            <linearGradient id="glassGrad" x1="0%" y1="0%" x2="0%" y2="100%">
              <stop offset="0%" stopColor="#38bdf8" stopOpacity="0.25" />
              <stop offset="100%" stopColor="#0284c7" stopOpacity="0.1" />
            </linearGradient>

            {/* Neon Glow Filtresi */}
            <filter id="glow" x="-20%" y="-20%" width="140%" height="140%">
              <feGaussianBlur stdDeviation="6" result="blur" />
              <feComposite in="SourceGraphic" in2="blur" operator="over" />
            </filter>
          </defs>

          {/* Dış Gövde Konturu */}
          <path
            d="M 120 70 
               C 120 50, 160 40, 200 40 
               C 240 40, 280 50, 280 70 
               C 295 90, 310 160, 315 220 
               C 320 280, 320 340, 315 420 
               C 310 490, 290 540, 275 555 
               C 255 565, 225 570, 200 570 
               C 175 570, 145 565, 125 555 
               C 110 540, 90 490, 85 420 
               C 80 340, 80 280, 85 220 
               C 90 160, 105 90, 120 70 Z"
            fill="url(#carBodyGrad)"
            stroke="#38bdf8"
            strokeWidth="2.5"
            strokeOpacity="0.8"
          />

          {/* Ön Cam */}
          <path
            d="M 135 170 C 150 155, 250 155, 265 170 L 255 240 C 240 245, 160 245, 145 240 Z"
            fill="url(#glassGrad)"
            stroke="#38bdf8"
            strokeWidth="1.5"
            strokeOpacity="0.5"
          />

          {/* Tavan Paneli */}
          <rect x="145" y="248" width="110" height="120" rx="10" fill="#0f172a" stroke="#334155" strokeWidth="1.5" />

          {/* Arka Cam */}
          <path
            d="M 145 375 C 160 370, 240 370, 255 375 L 265 440 C 250 455, 150 455, 135 440 Z"
            fill="url(#glassGrad)"
            stroke="#38bdf8"
            strokeWidth="1.5"
            strokeOpacity="0.5"
          />

          {/* Ön Farlar */}
          <polygon points="125,58 145,52 140,75 122,70" fill="#38bdf8" opacity="0.85" filter="url(#glow)" />
          <polygon points="275,58 255,52 260,75 278,70" fill="#38bdf8" opacity="0.85" filter="url(#glow)" />

          {/* Arka Stop Lambaları */}
          <polygon points="125,555 145,558 142,545 124,548" fill="#ef4444" opacity="0.85" filter="url(#glow)" />
          <polygon points="275,555 255,558 258,545 276,548" fill="#ef4444" opacity="0.85" filter="url(#glow)" />

          {/* Yan Aynalar */}
          <ellipse cx="80" cy="180" rx="12" ry="7" fill="#1e293b" stroke="#38bdf8" strokeWidth="1.5" />
          <ellipse cx="320" cy="180" rx="12" ry="7" fill="#1e293b" stroke="#38bdf8" strokeWidth="1.5" />

          {/* Tekerlekler */}
          <rect x="68" y="110" width="14" height="42" rx="4" fill="#0a0e1a" stroke="#475569" strokeWidth="1.5" />
          <rect x="318" y="110" width="14" height="42" rx="4" fill="#0a0e1a" stroke="#475569" strokeWidth="1.5" />
          <rect x="68" y="440" width="14" height="42" rx="4" fill="#0a0e1a" stroke="#475569" strokeWidth="1.5" />
          <rect x="318" y="440" width="14" height="42" rx="4" fill="#0a0e1a" stroke="#475569" strokeWidth="1.5" />
        </svg>

        {/* 8 Açılı Canlı Radar Hotspot Noktaları */}
        {Object.entries(ANGLE_COORDINATES).map(([key, pos]) => {
          const info = statusByAngle[key] || {};
          const isDamaged = info.hasDamage;

          return (
            <div
              key={key}
              className={`blueprint-hotspot ${isDamaged ? 'damaged' : 'clean'}`}
              style={{ left: `${pos.x}%`, top: `${pos.y}%` }}
              onMouseEnter={() => setActiveHover({ key, pos, info })}
              onMouseLeave={() => setActiveHover(null)}
              onClick={() => onSelectAngle && onSelectAngle(key, info)}
            >
              <div className="hotspot-pulse"></div>
              <div className="hotspot-core">
                {isDamaged ? '⚠️' : '✓'}
              </div>
              <span className="hotspot-label">{pos.label}</span>
            </div>
          );
        })}

        {/* Canlı Hover Detay Kartı Tooltip */}
        {activeHover && (
          <div
            className="blueprint-tooltip animate-in"
            style={{
              left: `${Math.min(75, Math.max(25, activeHover.pos.x))}%`,
              top: `${Math.min(75, Math.max(20, activeHover.pos.y))}%`,
            }}
          >
            <div className="tooltip-header">
              <strong>{activeHover.info.angleName} Bölgesi</strong>
              <span className={`badge ${activeHover.info.hasDamage ? 'danger' : 'success'}`}>
                {activeHover.info.hasDamage ? `${activeHover.info.damages.length} Hasar` : 'Temiz'}
              </span>
            </div>

            {activeHover.info.damages.length > 0 ? (
              <div className="tooltip-damages">
                {activeHover.info.damages.map((d, i) => (
                  <div key={i} className="tooltip-damage-row">
                    <span>{d.class_name || d.className}</span>
                    <strong style={{ color: '#ef4444' }}>%{Math.round((d.confidence || 0.8) * 100)}</strong>
                  </div>
                ))}
              </div>
            ) : (
              <div style={{ fontSize: '12px', color: '#86efac', marginTop: '6px' }}>Bu bölgede hasar kaydı yoktur.</div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
