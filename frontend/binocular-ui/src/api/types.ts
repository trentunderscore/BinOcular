export interface AnalysisResponse {
  analysisId: string;
  fileName: string;
  status: string;
  sha256?: string;
  createdAtUtc: string;
  startedAtUtc?: string;
  completedAtUtc?: string;
  error?: string;
}

export interface OutputField {
  label: string;
  value: string;
  valueType?: string;
  Label?: string;
  Value?: string;
  ValueType?: string;
}

export interface OutputSection {
  title: string;
  type: 'keyValue' | 'table' | 'text' | 'tags' | 'json';
  fields: OutputField[];
  columns: string[];
  rows: string[][];
  text?: string;
  Title?: string;
  Type?: 'keyValue' | 'table' | 'text' | 'tags' | 'json';
  Fields?: OutputField[];
  Columns?: string[];
  Rows?: string[][];
  Text?: string;
}

export interface FormattedOutput {
  title: string;
  sections: OutputSection[];
  Title?: string;
  Sections?: OutputSection[];
}

export interface ToolResultResponse {
  id: string;
  analysisRecordId: string;
  toolName: string;
  exitCode: number;
  standardOutput?: string;
  standardError?: string;
  formattedOutputJson?: string;
  completedAtUtc?: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface AnalysisStats {
  total: number;
  completed: number;
  running: number;
  queued: number;
  failed: number;
}

export interface GetAnalysesParams {
  page?: number;
  pageSize?: number;
  status?: string;
  search?: string;
}

export interface ExtractedStringItem {
  offset: number;
  offsetHex: string;
  length: number;
  encoding: string;
  value: string;
  category?: string | null;
  severity?: string | null;
}

export interface GetStringsParams {
  page?: number;
  pageSize?: number;
  minLength?: number;
  search?: string;
  encoding?: string;
  category?: string;
}
