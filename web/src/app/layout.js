import './globals.css';
import AppShell from '@/components/AppShell';

export const metadata = {
  title: 'Dentify Web — Filo Hasar Yönetim Paneli',
  description: 'Yapay zeka destekli araç hasar tespit ve filo yönetim platformu.',
};

export default function RootLayout({ children }) {
  return (
    <html lang="tr">
      <body>
        <AppShell>
          {children}
        </AppShell>
      </body>
    </html>
  );
}
