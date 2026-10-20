export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  error: ApiError | null;
}

export interface ApiError {
  code: string;
  message: string;
}

export interface AnalysisResponse {
  analysisId: string;
  fileName: string;
  status: string;
  sha256: string;
  createdAtUtc: string;
}