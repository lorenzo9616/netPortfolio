import { useState } from 'react';
import type { ExtractedFieldDto, SignatureDto } from '@/types/ocr';

interface Props {
  fields: ExtractedFieldDto[];
  overrides: Record<string, string>;
  isSaving: boolean;
  onOverrideChange: (propertyName: string, value: string) => void;
  signatures: SignatureDto[];
}

function ConfidenceBadge({ value }: { value: number }) {
  const pct = Math.round(value * 100);
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

export default function ExtractedFieldsTable({
  fields,
  overrides,
  isSaving,
  onOverrideChange,
  signatures,
}: Props) {
  const [editingField, setEditingField] = useState<string | null>(null);

  const filledFields = fields.filter(
    (f) => f.extractedValue !== null && f.extractedValue !== '',
  );
  const emptyFields = fields.filter(
    (f) => f.extractedValue === null || f.extractedValue === '',
  );

  function renderRow(field: ExtractedFieldDto) {
    const isSignature  = field.propertyName === 'Signature';
    const displayValue =
      overrides[field.propertyName] !== undefined
        ? overrides[field.propertyName]
        : field.extractedValue;
    const isEditing    = editingField === field.propertyName;

    return (
      <tr
        key={field.propertyName}
        className="group align-middle hover:bg-gray-50"
      >
        {/* Field name */}
        <td className="whitespace-nowrap px-4 py-3 text-xs font-semibold uppercase tracking-wide text-gray-500">
          {field.propertyName}
        </td>

        {/* Value — click to edit (except Signature) */}
        <td
          className={`px-4 py-3 font-medium text-gray-800 ${!isSignature ? 'cursor-text' : ''}`}
          tabIndex={isSignature ? undefined : 0}
          role={isSignature ? undefined : 'button'}
          onClick={() => {
            if (!isSignature && !isSaving) setEditingField(field.propertyName);
          }}
          onKeyDown={(e) => {
            if (!isSignature && !isSaving && (e.key === 'Enter' || e.key === ' '))
              setEditingField(field.propertyName);
          }}
        >
          {isSignature ? (
            signatures.length > 0 ? (
              <span className="inline-flex items-center rounded-full bg-emerald-100 px-2 py-0.5 text-xs font-semibold text-emerald-700 ring-1 ring-inset ring-emerald-200">
                {signatures.length} captured
              </span>
            ) : (
              <span className="italic text-gray-400">—</span>
            )
          ) : isEditing ? (
            <input
              autoFocus
              type="text"
              value={overrides[field.propertyName] ?? ''}
              onChange={(e) => onOverrideChange(field.propertyName, e.target.value)}
              onBlur={() => setEditingField(null)}
              onKeyDown={(e) => { if (e.key === 'Enter' || e.key === 'Escape') setEditingField(null); }}
              disabled={isSaving}
              aria-label={`Edit ${field.propertyName}`}
              className="w-full rounded-md border border-blue-300 bg-white px-2 py-0.5 text-sm text-gray-800 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          ) : (
            <span className={displayValue ? '' : 'italic text-gray-400'}>
              {displayValue || '—'}
            </span>
          )}
        </td>

        {/* Confidence */}
        <td className="px-4 py-3 text-right">
          {isSignature ? (
            <span className="text-xs italic text-gray-400">See preview</span>
          ) : (
            <ConfidenceBadge value={field.confidence} />
          )}
        </td>
      </tr>
    );
  }

  const tableShell = (rows: ExtractedFieldDto[]) => (
    <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white">
      <table className="min-w-full divide-y divide-gray-100 text-sm">
        <thead>
          <tr className="bg-gray-50 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">
            <th className="px-4 py-3">Field</th>
            <th className="px-4 py-3">Value <span className="normal-case font-normal text-gray-400">(click to edit)</span></th>
            <th className="px-4 py-3 text-right">Confidence</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {rows.map(renderRow)}
        </tbody>
      </table>
    </div>
  );

  return (
    <div className="flex flex-col gap-3">
      {filledFields.length > 0 && tableShell(filledFields)}

      {emptyFields.length > 0 && (
        <details className="group">
          <summary className="cursor-pointer select-none list-none text-xs font-medium text-gray-500 hover:text-gray-800 focus:outline-none">
            <span className="group-open:hidden">▶ {emptyFields.length} empty {emptyFields.length === 1 ? 'field' : 'fields'}</span>
            <span className="hidden group-open:inline">▼ {emptyFields.length} empty {emptyFields.length === 1 ? 'field' : 'fields'}</span>
          </summary>
          <div className="mt-2">{tableShell(emptyFields)}</div>
        </details>
      )}
    </div>
  );
}
