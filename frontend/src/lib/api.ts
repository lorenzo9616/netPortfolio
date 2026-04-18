import type {
  OcrProperty,
  CreateOcrPropertyDto,
  UpdateOcrPropertyDto,
  CropRegion,
  AnalyzeResponse,
  SavedFieldUpdate,
  ExtractedField,
  AnalysisResultDetail,
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
      // fallback to status text
      message = res.statusText || message;
    }
    throw new ApiError(message, res.status);
  }
  // 204 No Content
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

export async function analyzeDocument(
  file: File,
  crop?: CropRegion,
): Promise<AnalyzeResponse> {
  const formData = new FormData();
  formData.append('file', file);
  if (crop) {
    formData.append('cropX', String(crop.x));
    formData.append('cropY', String(crop.y));
    formData.append('cropWidth', String(crop.width));
    formData.append('cropHeight', String(crop.height));
  }
  const res = await fetch(`${API_BASE}/api/documents/analyze`, {
    method: 'POST',
    body: formData,
    // No Content-Type header — let browser set multipart boundary
  });
  if (!res.ok) throw new ApiError(`Analysis failed: ${res.statusText}`, res.status);
  return res.json() as Promise<AnalyzeResponse>;
}
