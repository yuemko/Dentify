'use client';

import { PieChart, Pie, Cell, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, LineChart, Line } from 'recharts';

const COLORS = ['#ef4444', '#f59e0b', '#22c55e', '#3b82f6', '#8b5cf6', '#ec4899', '#06b6d4'];

export function DamageDistributionChart({ data }) {
  if (!data || data.length === 0) {
    return <div className="empty-state"><p>Veri bulunamadı</p></div>;
  }

  return (
    <ResponsiveContainer width="100%" height={280}>
      <PieChart>
        <Pie
          data={data}
          cx="50%"
          cy="50%"
          innerRadius={65}
          outerRadius={110}
          paddingAngle={3}
          dataKey="count"
          nameKey="class_name"
          label={({ class_name, percent }) => `${class_name} ${(percent * 100).toFixed(0)}%`}
        >
          {data.map((entry, i) => (
            <Cell key={i} fill={COLORS[i % COLORS.length]} />
          ))}
        </Pie>
        <Tooltip
          contentStyle={{ background: '#1e293b', border: '1px solid #334155', borderRadius: '8px', color: '#f8fafc' }}
        />
      </PieChart>
    </ResponsiveContainer>
  );
}

export function MonthlyTrendChart({ data }) {
  if (!data || data.length === 0) {
    return <div className="empty-state"><p>Henüz yeterli veri yok</p></div>;
  }

  const formatted = data.map(d => ({
    ...d,
    monthLabel: d.month ? d.month.slice(5) + '/' + d.month.slice(0, 4) : d.month,
  }));

  return (
    <ResponsiveContainer width="100%" height={280}>
      <BarChart data={formatted}>
        <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
        <XAxis dataKey="monthLabel" stroke="#64748b" fontSize={12} />
        <YAxis stroke="#64748b" fontSize={12} />
        <Tooltip
          contentStyle={{ background: '#1e293b', border: '1px solid #334155', borderRadius: '8px', color: '#f8fafc' }}
        />
        <Bar dataKey="damage_count" fill="#38bdf8" radius={[4, 4, 0, 0]} name="Hasar Sayısı" />
      </BarChart>
    </ResponsiveContainer>
  );
}
