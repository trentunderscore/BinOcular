import type { AnalysisResponse } from '../../api/types';
import AnalysisCard from './AnalysisCard';
import {
  FiSearch,
  FiX,
  FiChevronLeft,
  FiChevronRight,
  FiInbox,
  FiRefreshCw,
} from 'react-icons/fi';

interface AnalysisListProps {
  analyses: AnalysisResponse[];
  totalCount: number;
  currentPage: number;
  totalPages: number;
  pageSize: number;
  loading: boolean;
  search: string;
  statusFilter: string;
  onSearchChange: (search: string) => void;
  onStatusFilterChange: (status: string) => void;
  onPageChange: (page: number) => void;
  onSelect: (analysis: AnalysisResponse) => void;
  onDelete?: (id: string) => void;
  onRefresh?: () => void;
  className?: string;
}

const STATUS_OPTIONS = ['All', 'Completed', 'Running', 'Queued', 'Failed'] as const;

export default function AnalysisList({
  analyses,
  totalCount,
  currentPage,
  totalPages,
  pageSize,
  loading,
  search,
  statusFilter,
  onSearchChange,
  onStatusFilterChange,
  onPageChange,
  onSelect,
  onDelete,
  onRefresh,
  className,
}: AnalysisListProps) {
  const startIndex = totalCount === 0 ? 0 : (currentPage - 1) * pageSize + 1;
  const endIndex = Math.min(currentPage * pageSize, totalCount);

  return (
    <div className={`flex flex-col gap-4 ${className || ''}`}>
      {/* Header & Controls Bar */}
      <div className="bg-card border border-border rounded-xl p-4 shadow-sm flex flex-col gap-3">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <h2 className="text-xl font-bold tracking-tight text-foreground">
              Analyses
            </h2>
            <span className="text-xs bg-muted text-muted-foreground px-2 py-0.5 rounded-full font-mono">
              {totalCount} total
            </span>
          </div>

          <div className="flex items-center gap-2">
            {onRefresh && (
              <button
                onClick={onRefresh}
                disabled={loading}
                title="Refresh list"
                className="p-2 text-muted-foreground hover:text-foreground hover:bg-muted border border-border/80 rounded-lg transition-colors flex items-center gap-1.5 text-xs font-medium"
              >
                <FiRefreshCw className={`w-3.5 h-3.5 ${loading ? 'animate-spin' : ''}`} />
                <span className="hidden sm:inline">Refresh</span>
              </button>
            )}
          </div>
        </div>

        {/* Search Bar & Filters */}
        <div className="flex flex-col sm:flex-row gap-2.5">
          {/* Search Box */}
          <div className="relative flex-1">
            <FiSearch className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground w-4 h-4" />
            <input
              type="text"
              value={search}
              onChange={(e) => onSearchChange(e.target.value)}
              placeholder="Search by file name or SHA-256..."
              className="w-full pl-9 pr-8 py-2 bg-input text-foreground border border-border rounded-lg text-sm placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all"
            />
            {search && (
              <button
                onClick={() => onSearchChange('')}
                className="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground p-0.5"
                title="Clear search"
              >
                <FiX className="w-3.5 h-3.5" />
              </button>
            )}
          </div>

          {/* Status Tabs */}
          <div className="flex items-center gap-1 overflow-x-auto pb-1 sm:pb-0 no-scrollbar">
            {STATUS_OPTIONS.map((status) => {
              const active = statusFilter === status;
              return (
                <button
                  key={status}
                  onClick={() => onStatusFilterChange(status)}
                  className={`px-3 py-1.5 text-xs font-medium rounded-lg whitespace-nowrap transition-colors ${
                    active
                      ? 'bg-primary text-primary-foreground shadow-sm'
                      : 'bg-muted/60 text-muted-foreground hover:bg-muted hover:text-foreground border border-border/60'
                  }`}
                >
                  {status}
                </button>
              );
            })}
          </div>
        </div>
      </div>

      {/* List Content */}
      <div className="space-y-3 min-h-[300px]">
        {loading && analyses.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-20 bg-card border border-border rounded-xl text-muted-foreground">
            <div className="w-8 h-8 border-2 border-primary border-t-transparent rounded-full animate-spin mb-3" />
            <p className="text-sm font-medium">Loading analyses...</p>
          </div>
        ) : analyses.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 bg-card border border-border rounded-xl text-muted-foreground text-center p-6">
            <FiInbox className="w-12 h-12 mb-3 text-muted-foreground/40" />
            <h3 className="text-base font-semibold text-foreground">No analyses found</h3>
            <p className="text-xs text-muted-foreground mt-1 max-w-sm">
              {search || statusFilter !== 'All'
                ? 'Try adjusting your search query or status filter.'
                : 'Upload a binary file (.exe, .dll, .elf, .bin) to begin analysis.'}
            </p>
          </div>
        ) : (
          analyses.map((analysis) => (
            <AnalysisCard
              key={analysis.analysisId}
              analysis={analysis}
              onSelect={onSelect}
              onDelete={onDelete}
            />
          ))
        )}
      </div>

      {/* Pagination Controls */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between bg-card border border-border rounded-xl px-4 py-3 text-xs text-muted-foreground">
          <div>
            Showing <span className="font-semibold text-foreground">{startIndex}</span> to{' '}
            <span className="font-semibold text-foreground">{endIndex}</span> of{' '}
            <span className="font-semibold text-foreground">{totalCount}</span> results
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={() => onPageChange(currentPage - 1)}
              disabled={currentPage <= 1 || loading}
              className="px-3 py-1.5 rounded-lg border border-border bg-muted/60 hover:bg-muted text-foreground disabled:opacity-40 disabled:cursor-not-allowed transition-colors flex items-center gap-1 font-medium"
            >
              <FiChevronLeft className="w-3.5 h-3.5" /> Previous
            </button>

            <span className="px-2 font-mono">
              Page {currentPage} of {totalPages}
            </span>

            <button
              onClick={() => onPageChange(currentPage + 1)}
              disabled={currentPage >= totalPages || loading}
              className="px-3 py-1.5 rounded-lg border border-border bg-muted/60 hover:bg-muted text-foreground disabled:opacity-40 disabled:cursor-not-allowed transition-colors flex items-center gap-1 font-medium"
            >
              Next <FiChevronRight className="w-3.5 h-3.5" />
            </button>
          </div>
        </div>
      )}
    </div>
  );
}