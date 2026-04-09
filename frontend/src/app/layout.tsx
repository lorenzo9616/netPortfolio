import type { Metadata } from 'next';
import './globals.css';
import Sidebar from '@/components/Sidebar';

export const metadata: Metadata = {
  title: 'OpenOCR',
  description: 'Open-source OCR document processing system',
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className="flex min-h-screen bg-gray-50 text-gray-900 antialiased">
        <Sidebar />
        <main className="flex-1 overflow-y-auto bg-gray-50 p-8">
          {children}
        </main>
      </body>
    </html>
  );
}
