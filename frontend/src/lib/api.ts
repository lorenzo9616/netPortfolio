import type {
  OcrProperty,
  CreateOcrPropertyDto,
  UpdateOcrPropertyDto,
  CropRegion,
  AnalyzeResponse,
  SavedFieldUpdate,
  ExtractedField,
  AnalysisResultDetail,
  DocumentSummary,
  SignatureDto,
} from '@/types/ocr';

// Server-side (RSC/SSR): use API_URL (internal Docker hostname, e.g. http://backend:5000).
// Client-side (browser): NEXT_PUBLIC_API_URL is baked into the bundle at build time.
export const API_BASE =
  typeof window === 'undefined'
    ? (process.env.API_URL ?? process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000')
    : (process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000');

export class ApiError extends Error {
  constructor(
    public readonly message: string,
    public readonly status: number,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    let message = `HTTP ${res.status}`;
    try {
      const body = await res.json();
      if (typeof body === 'object' && body !== null && 'message' in body) {
        message = String((body as { message: unknown }).message);
      } else if (typeof body === 'string') {
        message = body;
      }
    } catch {
      message = res.statusText || message;
    }
    throw new ApiError(message, res.status);
  }
  if (res.status === 204) {
    return undefined as unknown as T;
  }
  return res.json() as Promise<T>;
}

export async function getProperties(): Promise<OcrProperty[]> {
  const res = await fetch(`${API_BASE}/api/ocrproperties`, {
    cache: 'no-store',
  });
  return handleResponse<OcrProperty[]>(res);
}

export async function createProperty(
  dto: CreateOcrPropertyDto,
): Promise<OcrProperty> {
  const res = await fetch(`${API_BASE}/api/ocrproperties`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(dto),
  });
  return handleResponse<OcrProperty>(res);
}

export async function updateProperty(
  id: number,
  dto: UpdateOcrPropertyDto,
): Promise<OcrProperty> {
  const res = await fetch(`${API_BASE}/api/ocrproperties/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(dto),
  });
  return handleResponse<OcrProperty>(res);
}

export async function deleteProperty(id: number): Promise<void> {
  const res = await fetch(`${API_BASE}/api/ocrproperties/${id}`, {
    method: 'DELETE',
  });
  return handleResponse<void>(res);
}

export async function getDocuments(): Promise<DocumentSummary[]> {
  const res = await fetch(`${API_BASE}/api/documents`, {
    cache: 'no-store',
  });
  return handleResponse<DocumentSummary[]>(res);
}

export async function getAnalysisResult(id: number): Promise<AnalysisResultDetail> {
  const res = await fetch(`${API_BASE}/api/documents/${id}`, {
    cache: 'no-store',
  });
  return handleResponse<AnalysisResultDetail>(res);
}

export async function saveFieldOverrides(
  documentId: number,
  fields: SavedFieldUpdate[],
): Promise<ExtractedField[]> {
  const res = await fetch(`${API_BASE}/api/documents/${documentId}/fields`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(fields),
  });
  if (!res.ok) throw new ApiError(`Save failed: ${res.statusText}`, res.status);
  return res.json() as Promise<ExtractedField[]>;
}

export async function addSignature(
  documentId: number,
  imageData: string,
  label?: string | null,
): Promise<SignatureDto> {
  const res = await fetch(`${API_BASE}/api/documents/${documentId}/signatures`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ imageData, label: label ?? null }),
  });
  return handleResponse<SignatureDto>(res);
}

export async function deleteSignature(
  documentId: number,
  signatureId: number,
): Promise<void> {
  const res = await fetch(`${API_BASE}/api/documents/${documentId}/signatures/${signatureId}`, {
    method: 'DELETE',
  });
  return handleResponse<void>(res);
}

export async function analyzeDocument(
  file: File,
  crop?: CropRegion,
  lang: string = 'eng',
): Promise<AnalyzeResponse> {
  const formData = new FormData();
  formData.append('file', file);
  formData.append('lang', lang);
  if (crop) {
    formData.append('cropX', String(crop.x));
    formData.append('cropY', String(crop.y));
    formData.append('cropWidth', String(crop.width));
    formData.append('cropHeight', String(crop.height));
  }
  const res = await fetch(`${API_BASE}/api/documents/analyze`, {
    method: 'POST',
    body: formData,
  });
  if (!res.ok) throw new ApiError(`Analysis failed: ${res.statusText}`, res.status);
  return res.json() as Promise<AnalyzeResponse>;
}

export async function analyzeDocumentStream(
  file: File,
  crop: CropRegion | undefined,
  lang: string,
  onProgress: (message: string) => void,
): Promise<{ documentId: number }> {
  const formData = new FormData();
  formData.append('file', file);
  formData.append('lang', lang);
  if (crop) {
    formData.append('cropX', String(crop.x));
    formData.append('cropY', String(crop.y));
    formData.append('cropWidth', String(crop.width));
    formData.append('cropHeight', String(crop.height));
  }

  const res = await fetch(`${API_BASE}/api/documents/analyze/stream`, {
    method: 'POST',
    body: formData,
  });

  if (!res.ok || !res.body) {
    throw new ApiError(`Analysis failed: ${res.statusText}`, res.status);
  }

  const reader  = res.body.getReader();
  const decoder = new TextDecoder();
  let buffer    = '';

  while (true) {
    const { done, value } = await reader.read();
    if (done) break;

    buffer += decoder.decode(value, { stream: true });

    let sep = buffer.indexOf('\n\n');
    while (sep !== -1) {
      const chunk = buffer.substring(0, sep);
      buffer      = buffer.substring(sep + 2);
      sep         = buffer.indexOf('\n\n');

      const eventType = chunk.match(/^event: (.+)/m)?.[1]?.trim();
      const dataStr   = chunk.match(/^data: (.+)/m)?.[1]?.trim();
      if (!eventType || !dataStr) continue;

      if (eventType === 'status') {
        try {
          const d = JSON.parse(dataStr) as { message: string };
          onProgress(d.message);
        } catch { /* malformed — skip */ }
      } else if (eventType === 'done') {
        const d = JSON.parse(dataStr) as { documentId: number };
        return { documentId: d.documentId };
      } else if (eventType === 'error') {
        const d = JSON.parse(dataStr) as { message: string };
        throw new ApiError(d.message, 500);
      }
    }
  }

  throw new ApiError('Stream ended without a done event.', 500);
}
