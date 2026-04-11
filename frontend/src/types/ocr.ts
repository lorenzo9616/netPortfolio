export interface CropRegion {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface UploadState {
  file: File | null;
  previewUrl: string | null;
  imageDimensions: { width: number; height: number } | null;
  cropRegion: CropRegion | null;
  isCapturing: boolean; // camera mode active
}

export interface ExtractedField {
  propertyName: string;
  extractedValue: string | null;
  confidence: number;
}

export interface AnalyzeResponse {
  documentId: number;
  extractedFields: ExtractedField[];
  rawText: string;
}

export interface SavedFieldUpdate {
  propertyName: string;
  manualOverride: string | null;
}

export interface AnalysisResultDetail {
  documentId: number;
  extractedFields: ExtractedField[];
  rawText: string;
  analyzedAt: string;
  fileName: string;
}

export interface OcrProperty {
  id: number;
  name: string;
  dataType: string;
  searchHeuristic: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateOcrPropertyDto {
  name: string;
  dataType: string;
  searchHeuristic?: string | null;
  isActive: boolean;
}

export interface UpdateOcrPropertyDto {
  name?: string;
  dataType?: string;
  searchHeuristic?: string | null;
  isActive?: boolean;
}

export interface DocumentHistoryItem {
  documentId: number;
  createdAt: string;
  pageCount: number;
  fieldCount: number;
  preview: string;
}

export interface DocumentHistoryPage {
  items: DocumentHistoryItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}
