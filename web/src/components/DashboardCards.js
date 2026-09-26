'use client';

export default function DashboardCards({ kpi }) {
  const cards = [
    { label: 'Toplam Araç', value: kpi.totalSessions, subtitle: 'Tüm kayıtlı oturumlar', variant: 'accent' },
    { label: 'Aktif Kiralama', value: kpi.activeRentals, subtitle: 'Müşteride / Devam eden', variant: 'warning' },
    { label: 'Yeni Hasar', value: kpi.totalNewDamages, subtitle: 'Teslim sonrası tespit edilen', variant: 'danger' },
    { label: 'Çözülen Hasar', value: kpi.totalResolvedDamages, subtitle: 'Onarılmış / giderilmiş', variant: 'success' },
  ];

  return (
    <div className="kpi-grid">
      {cards.map((card, i) => (
        <div key={i} className={`kpi-card ${card.variant} animate-in`}>
          <div className="kpi-label">{card.label}</div>
          <div className="kpi-value">{card.value}</div>
          <div className="kpi-subtitle">{card.subtitle}</div>
        </div>
      ))}
    </div>
  );
}
