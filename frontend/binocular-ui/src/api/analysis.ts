import type {
  AnalysisResponse,
  AnalysisStats,
  GetAnalysesParams,
  PagedResult,
  ToolResultResponse,
  ExtractedStringItem,
  GetStringsParams,
} from './types';

export async function getAnalyses(
  params: GetAnalysesParams = {}
): Promise<PagedResult<AnalysisResponse>> {
  const query = new URLSearchParams();
  if (params.page !== undefined) query.set('page', params.page.toString());
  if (params.pageSize !== undefined) query.set('pageSize', params.pageSize.toString());
  if (params.status && params.status !== 'All') query.set('status', params.status);
  if (params.search && params.search.trim()) query.set('search', params.search.trim());

  const url = `/api/analyses${query.toString() ? `?${query.toString()}` : ''}`;
  const response = await fetch(url);

  if (!response.ok) {
    throw new Error(`Failed to fetch analyses: ${response.status} ${response.statusText}`);
  }

  return response.json();
}

export async function getAnalysis(id: string): Promise<AnalysisResponse> {
  const response = await fetch(`/api/analyses/${id}`);
  if (!response.ok) {
    throw new Error(`Failed to fetch analysis: ${response.status}`);
  }
  return response.json();
}

export async function getAnalysisStats(): Promise<AnalysisStats> {
  const response = await fetch('/api/analyses/stats');
  if (!response.ok) {
    throw new Error(`Failed to fetch analysis stats: ${response.status}`);
  }
  return response.json();
}

export async function uploadFile(file: File): Promise<AnalysisResponse> {
  const formData = new FormData();
  formData.append('file', file);

  const response = await fetch('/api/analyses', {
    method: 'POST',
    body: formData,
  });

  if (!response.ok) {
    let errorDetail = '';
    try {
      const errJson = await response.json();
      errorDetail = errJson.error || errJson.title || '';
    } catch {
      // ignore
    }
    throw new Error(errorDetail || `Upload failed with status ${response.status}`);
  }

  return response.json();
}

export async function getToolResults(
  analysisId: string
): Promise<ToolResultResponse[]> {
  const response = await fetch(`/api/analyses/${analysisId}/tool-results`);
  if (!response.ok) {
    throw new Error(`Fetching results failed: ${response.status}`);
  }
  return response.json();
}

export async function deleteAnalysis(id: string): Promise<void> {
  const response = await fetch(`/api/analyses/${id}`, {
    method: 'DELETE',
  });
  if (!response.ok) {
    throw new Error(`Failed to delete analysis: ${response.status}`);
  }
}

export async function deleteAllAnalyses(): Promise<void> {
  const response = await fetch('/api/analyses', {
    method: 'DELETE',
  });
  if (!response.ok) {
    throw new Error(`Failed to delete all analyses: ${response.status}`);
  }
}

export async function getStrings(
  analysisId: string,
  params: GetStringsParams = {}
): Promise<PagedResult<ExtractedStringItem>> {
  const query = new URLSearchParams();
  if (params.page !== undefined) query.set('page', params.page.toString());
  if (params.pageSize !== undefined) query.set('pageSize', params.pageSize.toString());
  if (params.minLength !== undefined) query.set('minLength', params.minLength.toString());
  if (params.search && params.search.trim()) query.set('search', params.search.trim());
  if (params.encoding && params.encoding !== 'all') query.set('encoding', params.encoding);
  if (params.category && params.category !== 'all') query.set('category', params.category);

  const url = `/api/analyses/${analysisId}/strings${query.toString() ? `?${query.toString()}` : ''}`;
  const response = await fetch(url);

  if (!response.ok) {
    throw new Error(`Failed to fetch strings: ${response.status}`);
  }

  return response.json();
}

export function getStringsDownloadUrl(analysisId: string, minLength = 4, encoding = 'all'): string {
  const query = new URLSearchParams();
  query.set('minLength', minLength.toString());
  if (encoding && encoding !== 'all') query.set('encoding', encoding);
  return `/api/analyses/${analysisId}/strings/download?${query.toString()}`;
}
