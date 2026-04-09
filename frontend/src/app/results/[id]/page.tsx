import Link from 'next/link';
import { getAnalysisResult } from '@/lib/api';
import { ApiError } from '@/lib/api';
import ResultsClient from '@/components/results/ResultsClient';

interface PageProps {
  params: Promise<{ id: string }>;
}

export default async function ResultsPage({ params }: PageProps) {
  const { id } = await params;
  const numericId = Number(id);

  if (!Number.isInteger(numericId) || numericId <= 0) {
    return <NotFoundView id={id} />;
  }

  try {
    const result = await getAnalysisResult(numericId);
    return <ResultsClient result={result} />;
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) {
      return <NotFoundView id={id} />;
    }

    // Unexpected fetch/network failure
    const message =
      err instanceof ApiError
        ? err.message
        : err instanceof Error
        ? err.message
        : 'An unexpected error occurred while loading the results.';

    return (
      <div className="flex min-h-[40vh] flex-col items-center justify-center gap-4 px-6 py-12 text-center">
        <div className="rounded-xl border border-red-200 bg-red-50 px-6 py-5 max-w-md w-full">
          <p className="text-sm font-semibold text-red-800">Failed to load results</p>
          <p className="mt-1 text-xs text-red-700">{message}</p>
        </div>
        <Link
          href="/upload"
          className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
        >
          Back to Upload
        </Link>
      </div>
    );
  }
}

function NotFoundView({ id }: { id: string }) {
  return (
    <div className="flex min-h-[40vh] flex-col items-center justify-center gap-4 px-6 py-12 text-center">
      <div className="rounded-xl border border-gray-200 bg-gray-50 px-6 py-5 max-w-md w-full">
        <p className="text-sm font-semibold text-gray-800">Document not found</p>
        <p className="mt-1 text-xs text-gray-500">
          No analysis result exists for ID &ldquo;{id}&rdquo;. It may have been deleted or never
          created.
        </p>
      </div>
      <Link
        href="/upload"
        className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
      >
        Back to Upload
      </Link>
    </div>
  );
}
