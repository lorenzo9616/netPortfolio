import HistoryClient from '@/components/history/HistoryClient';

export const metadata = {
  title: 'History — OpenOCR',
};

export default function HistoryPage() {
  return (
    <div className="mx-auto max-w-5xl">
      <h1 className="mb-6 text-2xl font-bold text-gray-900">Document History</h1>
      <HistoryClient />
    </div>
  );
}
