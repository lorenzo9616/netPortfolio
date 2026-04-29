import type { Metadata } from 'next';
import Link from 'next/link';
import { getDocuments, ApiError } from '@/lib/api';
import DocumentsClient from '@/components/documents/DocumentsClient';

export const metadata: Metadata = {
  title: 'Document History — OpenOCR',
};

export default async function DocumentsPage() {
  try {
    const documents = await getDocuments();
    return (
      <div>
        <div className="mb-6 flex items-center justify-between">
          <h1 className="text-2xl font-bold text-gray-900">Document History</h1>
          <Link
            href="/upload"
            className="rounded-xl bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
          >
            New Upload
          </Link>
        </div>
        <DocumentsClient documents={documents} />
      </div>
    );
  } catch (err) {
    const message =
      err instanceof ApiError ? err.message
      : err instanceof Error ? err.message
      : 'Failed to load documents.';
    return (
      <div>
        <h1 className="mb-6 text-2xl font-bold text-gray-900">Document History</h1>
        <div className="rounded-xl border border-red-200 bg-red-50 px-5 py-4">
          <p className="text-sm font-semibold text-red-800">Failed to load documents</p>
          <p className="mt-1 text-xs text-red-700">{message}</p>
        </div>
      </div>
    );
  }
}
