'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState('admin@dentify.com');
  const [password, setPassword] = useState('dentify2026');
  const [role, setRole] = useState('Filo Müdürü');
  const [loading, setLoading] = useState(false);

  const handleLogin = (e) => {
    e.preventDefault();
    setLoading(true);

    setTimeout(() => {
      // LocalStorage üzerinden oturum kaydet
      if (typeof window !== 'undefined') {
        localStorage.setItem('dentify_user', JSON.stringify({
          email,
          name: role === 'Filo Müdürü' ? 'Ahmet Yılmaz (Yönetici)' : 'Operasyon Ekibi',
          role,
          loginTime: new Date().toISOString()
        }));
      }
      router.push('/');
    }, 600);
  };

  const handleQuickLogin = (selectedRole, defaultEmail) => {
    setRole(selectedRole);
    setEmail(defaultEmail);
    setPassword('dentify2026');
  };

  return (
    <div className="login-wrapper">
      <div className="login-card animate-in">
        <div className="login-header">
          <div className="login-brand">DENTIFY</div>
          <h2>Filo Yönetim Portalı</h2>
          <p>Yapay Zeka Destekli Hasar Takip ve Operasyon Merkezi</p>
        </div>

        <form onSubmit={handleLogin} className="login-form">
          <div className="form-group">
            <label>Rol Seçimi</label>
            <div className="role-selector">
              <button
                type="button"
                className={`role-btn ${role === 'Filo Müdürü' ? 'active' : ''}`}
                onClick={() => handleQuickLogin('Filo Müdürü', 'admin@dentify.com')}
              >
                🏢 Filo Müdürü
              </button>
              <button
                type="button"
                className={`role-btn ${role === 'Operasyon Yetkilisi' ? 'active' : ''}`}
                onClick={() => handleQuickLogin('Operasyon Yetkilisi', 'operasyon@dentify.com')}
              >
                🚗 Operasyon Sorumlusu
              </button>
            </div>
          </div>

          <div className="form-group">
            <label>E-Posta Adresi</label>
            <input
              type="email"
              className="login-input"
              value={email}
              onChange={e => setEmail(e.target.value)}
              placeholder="ornek@dentify.com"
              required
            />
          </div>

          <div className="form-group">
            <label>Şifre</label>
            <input
              type="password"
              className="login-input"
              value={password}
              onChange={e => setPassword(e.target.value)}
              placeholder="••••••••"
              required
            />
          </div>

          <button type="submit" className="btn btn-primary login-submit-btn" disabled={loading}>
            {loading ? 'Giriş Yapılıyor...' : '🚀 Güvenli Giriş Yap'}
          </button>
        </form>

        <div className="login-footer">
          <p>Dentify Edge AI Ekosistemi &bull; v1.0 Enterprise</p>
          <div className="demo-credentials-hint">
            <span>Demo Modu:</span> İstediğiniz rolü seçip doğrudan giriş yapabilirsiniz.
          </div>
        </div>
      </div>
    </div>
  );
}
