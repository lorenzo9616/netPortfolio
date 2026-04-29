import type { OcrTextBlock } from '@/types/ocr';

function ConfidenceBadge({ value }: { value: number }) {
  const pct = Math.round(value);
  const colorClass =
    pct > 80
      ? 'bg-green-100 text-green-800 ring-green-200'
      : pct > 50
      ? 'bg-yellow-100 text-yellow-800 ring-yellow-200'
      : 'bg-red-100 text-red-800 ring-red-200';
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${colorClass}`}>
      {pct}%
    </span>
  );
}

interface Props {
  blocks: OcrTextBlock[];
}

export default function OcrTokensTable({ blocks }: Props) {
  return (
    <section aria-label="Raw OCR tokens">
      <h2 className="mb-4 text-sm font-semibold uppercase tracking-wide text-gray-500">
        Raw OCR Tokens
      </h2>

      {blocks.length === 0 ? (
        <div className="rounded-xl border border-gray-200 bg-gray-50 px-5 py-6 text-center">
          <p className="text-sm text-gray-600">No OCR tokens found.</p>
        </div>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white">
          <div className="max-h-96 overflow-y-auto">
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <thead className="sticky top-0 bg-gray-50">
                <tr className="text-left text-xs font-semibold uppercase tracking-wide text-gray-500">
                  <th className="px-4 py-3">#</th>
                  <th className="px-4 py-3">Text</th>
                  <th className="px-4 py-3">Confidence</th>
                  <th className="px-4 py-3">Page</th>
                  <th className="px-4 py-3">Position</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {blocks.map((b, i) => (
                  <tr key={i} className="align-middle">
                    <td className="px-4 py-2 text-gray-400">{i + 1}</td>
                    <td className="px-4 py-2 font-medium text-gray-800">{b.text}</td>
                    <td className="px-4 py-2">
                      <ConfidenceBadge value={b.confidence} />
                    </td>
                    <td className="px-4 py-2 text-gray-600">{b.page}</td>
                    <td className="px-4 py-2 text-gray-500">
                      ({b.bboxX}, {b.bboxY})
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </section>
  );
}
