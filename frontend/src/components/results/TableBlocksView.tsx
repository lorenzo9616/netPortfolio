import type { TableBlock } from '@/types/ocr';

interface Props {
  tables: TableBlock[];
}

export default function TableBlocksView({ tables }: Props) {
  if (tables.length === 0) return null;

  return (
    <section aria-label="Detected tables">
      <h2 className="mb-4 text-sm font-semibold uppercase tracking-wide text-gray-500">
        Detected Tables ({tables.length})
      </h2>
      <div className="flex flex-col gap-6">
        {tables.map((table, tableIdx) => (
          <div key={tableIdx} className="overflow-x-auto rounded-xl border border-gray-200 bg-white">
            <div className="border-b border-gray-100 bg-gray-50 px-4 py-2">
              <span className="text-xs font-semibold uppercase tracking-wide text-gray-500">
                Page {table.page} — {table.rows} rows × {table.cols} columns
              </span>
            </div>
            <table className="min-w-full divide-y divide-gray-100 text-sm">
              <tbody className="divide-y divide-gray-100">
                {Array.from({ length: table.rows }, (_, rowIdx) => (
                  <tr
                    key={rowIdx}
                    className={rowIdx === 0 ? 'bg-gray-50 font-semibold text-gray-700' : 'hover:bg-gray-50'}
                  >
                    {Array.from({ length: table.cols }, (_, colIdx) => {
                      const cell = table.cells.find(
                        (c) => c.row === rowIdx && c.col === colIdx,
                      );
                      return (
                        <td
                          key={colIdx}
                          className="px-4 py-2 text-gray-800 align-top"
                        >
                          {cell?.text ?? ''}
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))}
      </div>
    </section>
  );
}
