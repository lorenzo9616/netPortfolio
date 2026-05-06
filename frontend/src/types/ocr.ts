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
  isCapturing: boolean;
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

export interface OcrTextBlock {
  text: string;
  confidence: number;
  page: number;
  bboxX: number;
  bboxY: number;
  bboxWidth: number;
  bboxHeight: number;
}

export interface TableCell {
  row: number;
  col: number;
  text: string;
}

export interface TableBlock {
  page: number;
  rows: number;
  cols: number;
  cells: TableCell[];
}

export interface SignatureDto {
  id: number;
  imageData: string; // base64
  label: string | null;
  capturedAt: string;
}

export interface AnalysisResultDetail {
  documentId: number;
  extractedFields: ExtractedField[];
  rawText: string;
  analyzedAt: string;
  fileName: string;
  documentType: string | null;
  textBlocks: OcrTextBlock[];
  signatures: SignatureDto[];
  pageCount: number;
  tableBlocks: TableBlock[];
}

export interface DocumentFieldSummary {
  propertyName: string;
  extractedValue: string | null;
  confidence: number;
}

export interface DocumentSummary {
  id: number;
  fileName: string;
  analyzedAt: string;
  documentType: string | null;
  fieldCount: number;
  pageCount: number;
  fields: DocumentFieldSummary[];
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
