import { getProperties } from '@/lib/api';
import PropertiesList from '@/components/properties/PropertiesList';
import type { OcrProperty } from '@/types/ocr';

export const metadata = {
  title: 'Properties — OpenOCR',
};

export default async function PropertiesPage() {
  let properties: OcrProperty[] = [];
  let fetchError: string | null = null;

  try {
    properties = await getProperties();
  } catch (err) {
    if (err instanceof Error) {
      fetchError = err.message;
    } else {
      fetchError = 'Failed to load properties.';
    }
  }

  if (fetchError) {
    return (
      <div
        role="alert"
        className="rounded-xl border border-red-200 bg-red-50 p-6 text-red-700"
      >
        <p className="font-semibold">Failed to load properties</p>
        <p className="mt-1 text-sm">{fetchError}</p>
        <p className="mt-3 text-sm text-gray-600">
          Make sure the API is running at{' '}
          <code className="rounded bg-gray-100 px-1">
            {process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000'}
          </code>
          .
        </p>
      </div>
    );
  }

  return <PropertiesList initialProperties={properties} />;
}
