import type { Metadata } from 'next';
import UploadClient from '@/components/upload/UploadClient';

export const metadata: Metadata = {
  title: 'Upload Document — OpenOCR',
};

export default function UploadPage() {
  return (
    <div>
      <h1 className="mb-6 text-2xl font-bold text-gray-900">Upload Document</h1>
      <UploadClient />
    </div>
  );
}
