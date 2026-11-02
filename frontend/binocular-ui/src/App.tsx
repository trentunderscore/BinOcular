import { useState, useEffect, useCallback, useRef } from 'react';
import type { AnalysisResponse, AnalysisStats } from './api/types';
import {
  getAnalyses,
  getAnalysisStats,
  deleteAnalysis,
  deleteAllAnalyses,
} from './api/analysis';
import UploadPanel from './components/upload/UploadPanel';
import AnalysisList from './components/analysis/AnalysisList';
import AnalysisDetailModal from './components/analysis/AnalysisDetailModal';
import {
  FiEye,
  FiActivity,
  FiCheckCircle,
  FiClock,
  FiAlertTriangle,
  FiTrash2,
  FiRefreshCw,
  FiRadio,
  FiHelpCircle,
  FiGithub,
  FiCommand,
  FiX,
  FiLayers,
} from 'react-icons/fi';

export default function App() {
  const [analyses, setAnalyses] = useState<AnalysisResponse[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(8);
  const [statusFilter, setStatusFilter] = useState('All');
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [stats, setStats] = useState<AnalysisStats>({
    total: 0,
    completed: 0,
    running: 0,
    queued: 0,
    failed: 0,
  });

  const [selectedAnalysis, setSelectedAnalysis] = useState<AnalysisResponse | null>(null);
  const [isDeletingAll, setIsDeletingAll] = useState(false);
  const [showShortcuts, setShowShortcuts] = useState(false);

  // Debounce search query so rapid typing does not spam the API
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(search);
    }, 250);
    return () => clearTimeout(timer);
  }, [search]);

  // Global keyboard shortcuts (e.g. ? for shortcut cheat sheet)
  useEffect(() => {
    const handleGlobalKey = (e: KeyboardEvent) => {
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) {
        return;
      }
      if (e.key === '?') {
        e.preventDefault();
        setShowShortcuts((prev) => !prev);
      }
    };
    window.addEventListener('keydown', handleGlobalKey);
    return () => window.removeEventListener('keydown', handleGlobalKey);
  }, []);

  // Keep ref of active status for polling decision
  const hasActiveJobs = stats.running > 0 || stats.queued > 0;
  const pollTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const refreshData = useCallback(
    async (showSpinner = false) => {
      if (showSpinner) {
        setLoading(true);
      }

      try {
        const [analysesResult, statsResult] = await Promise.all([
          getAnalyses({
            page,
            pageSize,
            status: statusFilter,
            search: debouncedSearch,
          }),
          getAnalysisStats().catch(() => ({
            total: 0,
            completed: 0,
            running: 0,
            queued: 0,
            failed: 0,
          })),
        ]);

        setAnalyses(analysesResult.items);
        setTotalCount(analysesResult.totalCount);
        setTotalPages(analysesResult.totalPages || 1);
        setStats(statsResult);
      } catch (err) {
        console.error('Failed to refresh dashboard data:', err);
      } finally {
        if (showSpinner) {
          setLoading(false);
        }
      }
    },
    [page, pageSize, statusFilter, debouncedSearch]
  );

  // Initial load and filter synchronization
  useEffect(() => {
    let isCancelled = false;

    Promise.all([
      getAnalyses({
        page,
        pageSize,
        status: statusFilter,
        search: debouncedSearch,
      }),
      getAnalysisStats().catch(() => ({
        total: 0,
        completed: 0,
        running: 0,
        queued: 0,
        failed: 0,
      })),
    ])
      .then(([analysesResult, statsResult]) => {
        if (!isCancelled) {
          setAnalyses(analysesResult.items);
          setTotalCount(analysesResult.totalCount);
          setTotalPages(analysesResult.totalPages || 1);
          setStats(statsResult);
          setLoading(false);
        }
      })
      .catch((err) => {
        if (!isCancelled) {
          console.error('Failed to load dashboard data:', err);
          setLoading(false);
        }
      });

    return () => {
      isCancelled = true;
    };
  }, [page, pageSize, statusFilter, debouncedSearch]);

  // Smart polling: Poll every 3 seconds if jobs are running/queued
  useEffect(() => {
    if (pollTimerRef.current) {
      clearInterval(pollTimerRef.current);
      pollTimerRef.current = null;
    }

    if (hasActiveJobs) {
      pollTimerRef.current = setInterval(() => {
        refreshData(false);
      }, 3000);
    }

    return () => {
      if (pollTimerRef.current) {
        clearInterval(pollTimerRef.current);
      }
    };
  }, [hasActiveJobs, refreshData]);

  const handleUploadSuccess = (_newAnalysis: AnalysisResponse) => {
    setPage(1);
    refreshData(true);
  };

  const handleDeleteAnalysis = async (id: string) => {
    try {
      await deleteAnalysis(id);
      refreshData(false);
    } catch (err: unknown) {
      alert(`Failed to delete: ${err instanceof Error ? err.message : String(err)}`);
    }
  };

  const handleDeleteAll = async () => {
    if (
      !confirm(
        'Are you sure you want to delete ALL analyses and associated files? This cannot be undone.'
      )
    ) {
      return;
    }

    setIsDeletingAll(true);
    try {
      await deleteAllAnalyses();
      setPage(1);
      await refreshData(true);
    } catch (err: unknown) {
      alert(`Failed to clear database: ${err instanceof Error ? err.message : String(err)}`);
    } finally {
      setIsDeletingAll(false);
    }
  };

  const handleStatusFilterChange = (newStatus: string) => {
    setStatusFilter(newStatus);
    setPage(1);
  };

  const handleSearchChange = (newSearch: string) => {
    setSearch(newSearch);
    setPage(1);
    if (!newSearch) {
      setDebouncedSearch('');
    }
  };

  return (
    <div className="min-h-screen bg-background text-foreground flex flex-col font-sans selection:bg-primary/30 selection:text-white">
      {/* Top Navigation Bar */}
      <header className="sticky top-0 z-40 bg-card/90 backdrop-blur-md border-b border-border">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-xl bg-gradient-to-tr from-primary to-accent-foreground flex items-center justify-center text-white shadow-md shadow-primary/20">
              <FiEye className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="font-bold text-lg tracking-tight text-foreground">BinOcular</span>
                <span className="text-[10px] font-mono uppercase px-1.5 py-0.5 rounded bg-primary/20 text-primary border border-primary/30">
                  v1.0
                </span>
              </div>
              <p className="text-xs text-muted-foreground hidden sm:block">
                Automated Binary & Firmware Triage Workbench
              </p>
            </div>
          </div>

          {/* Right Header Actions */}
          <div className="flex items-center gap-2.5 sm:gap-3">
            {/* Live Polling Status Pill */}
            <div
              className={`flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-mono border transition-colors ${
                hasActiveJobs
                  ? 'bg-blue-950/60 text-blue-400 border-blue-800/60'
                  : 'bg-muted/40 text-muted-foreground border-border/60'
              }`}
            >
              <FiRadio className={`w-3.5 h-3.5 ${hasActiveJobs ? 'animate-pulse text-blue-400' : ''}`} />
              <span className="hidden sm:inline">
                {hasActiveJobs ? 'Pipeline Processing' : 'Pipeline Idle'}
              </span>
            </div>

            <button
              onClick={() => refreshData(true)}
              disabled={loading}
              title="Refresh Data"
              className="p-2 text-muted-foreground hover:text-foreground hover:bg-muted border border-border/80 rounded-lg transition-colors"
            >
              <FiRefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            </button>

            {/* Shortcuts Guide Button */}
            <button
              onClick={() => setShowShortcuts(true)}
              title="Keyboard Shortcuts (?)"
              className="p-2 text-muted-foreground hover:text-foreground hover:bg-muted border border-border/80 rounded-lg transition-colors"
            >
              <FiHelpCircle className="w-4 h-4" />
            </button>

            {/* GitHub Repo Link */}
            <a
              href="https://github.com"
              target="_blank"
              rel="noopener noreferrer"
              title="View on GitHub"
              className="p-2 text-muted-foreground hover:text-foreground hover:bg-muted border border-border/80 rounded-lg transition-colors hidden sm:flex items-center gap-1.5"
            >
              <FiGithub className="w-4 h-4" />
            </a>

            {totalCount > 0 && (
              <button
                onClick={handleDeleteAll}
                disabled={isDeletingAll}
                title="Clear All Analyses"
                className="p-2 text-muted-foreground hover:text-destructive hover:bg-destructive/10 border border-border/80 rounded-lg transition-colors"
              >
                <FiTrash2 className="w-4 h-4" />
              </button>
            )}
          </div>
        </div>
      </header>

      {/* Main Content Area */}
      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6">
        {/* KPI Summary Strip */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div className="bg-card border border-border rounded-xl p-4 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs text-muted-foreground font-medium uppercase tracking-wider">
                Total Submissions
              </p>
              <h4 className="text-2xl font-bold text-foreground mt-1 font-mono">{stats.total}</h4>
            </div>
            <div className="p-3 bg-muted text-muted-foreground rounded-xl">
              <FiActivity className="w-5 h-5 text-primary" />
            </div>
          </div>

          <div className="bg-card border border-border rounded-xl p-4 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs text-muted-foreground font-medium uppercase tracking-wider">
                Completed
              </p>
              <h4 className="text-2xl font-bold text-emerald-400 mt-1 font-mono">
                {stats.completed}
              </h4>
            </div>
            <div className="p-3 bg-emerald-950/50 text-emerald-400 rounded-xl border border-emerald-800/40">
              <FiCheckCircle className="w-5 h-5" />
            </div>
          </div>

          <div className="bg-card border border-border rounded-xl p-4 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs text-muted-foreground font-medium uppercase tracking-wider">
                In Queue / Active
              </p>
              <h4 className="text-2xl font-bold text-blue-400 mt-1 font-mono">
                {stats.running + stats.queued}
              </h4>
            </div>
            <div className="p-3 bg-blue-950/50 text-blue-400 rounded-xl border border-blue-800/40">
              <FiClock className={`w-5 h-5 ${hasActiveJobs ? 'animate-spin' : ''}`} />
            </div>
          </div>

          <div className="bg-card border border-border rounded-xl p-4 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs text-muted-foreground font-medium uppercase tracking-wider">
                Failed
              </p>
              <h4 className="text-2xl font-bold text-rose-400 mt-1 font-mono">
                {stats.failed}
              </h4>
            </div>
            <div className="p-3 bg-rose-950/50 text-rose-400 rounded-xl border border-rose-800/40">
              <FiAlertTriangle className="w-5 h-5" />
            </div>
          </div>
        </div>

        {/* Two-Column Responsive Split */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
          {/* Left Column: Upload Panel (5 cols) */}
          <div className="lg:col-span-5 lg:sticky lg:top-24">
            <UploadPanel onUploadSuccess={handleUploadSuccess} />
          </div>

          {/* Right Column: Analyses List & Management (7 cols) */}
          <div className="lg:col-span-7">
            <AnalysisList
              analyses={analyses}
              totalCount={totalCount}
              currentPage={page}
              totalPages={totalPages}
              pageSize={pageSize}
              loading={loading}
              search={search}
              statusFilter={statusFilter}
              onSearchChange={handleSearchChange}
              onStatusFilterChange={handleStatusFilterChange}
              onPageChange={setPage}
              onSelect={setSelectedAnalysis}
              onDelete={handleDeleteAnalysis}
              onRefresh={() => refreshData(true)}
            />
          </div>
        </div>
      </main>

      {/* Modern Cybersecurity Footer */}
      <footer className="mt-auto border-t border-border bg-card/60 backdrop-blur-sm py-4">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 flex flex-col sm:flex-row items-center justify-between gap-3 text-xs text-muted-foreground">
          <div className="flex items-center gap-2">
            <span className="font-semibold text-foreground">BinOcular</span>
            <span>•</span>
            <span>Automated Binary & Firmware Triage Workbench</span>
          </div>

          <div className="flex items-center gap-4">
            <span className="font-mono text-[11px] text-muted-foreground/80 flex items-center gap-1">
              <FiLayers className="w-3.5 h-3.5 text-primary" /> 15 Heuristic Engines Active
            </span>
            <span>•</span>
            <button
              onClick={() => setShowShortcuts(true)}
              className="text-primary hover:underline flex items-center gap-1 font-medium"
            >
              Shortcuts <kbd className="px-1 py-0.5 rounded bg-muted border border-border text-[10px] font-mono">?</kbd>
            </button>
          </div>
        </div>
      </footer>

      {/* Keyboard Shortcuts Cheat Sheet Modal */}
      {showShortcuts && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-in fade-in duration-150"
          onClick={() => setShowShortcuts(false)}
        >
          <div
            className="bg-card border border-border rounded-2xl shadow-2xl max-w-md w-full p-6 space-y-4"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between pb-3 border-b border-border">
              <div className="flex items-center gap-2.5">
                <div className="p-2 rounded-lg bg-primary/10 text-primary">
                  <FiCommand className="w-4 h-4" />
                </div>
                <h3 className="font-bold text-base text-foreground">Keyboard Navigation</h3>
              </div>
              <button
                onClick={() => setShowShortcuts(false)}
                className="p-1.5 text-muted-foreground hover:text-foreground rounded-lg"
              >
                <FiX className="w-4 h-4" />
              </button>
            </div>

            <div className="space-y-2.5 text-xs">
              <div className="flex items-center justify-between py-1.5 border-b border-border/40">
                <span className="text-muted-foreground">Grid View (All 15 Tools)</span>
                <kbd className="px-2 py-1 rounded bg-muted border border-border font-mono font-semibold text-foreground">1</kbd>
              </div>
              <div className="flex items-center justify-between py-1.5 border-b border-border/40">
                <span className="text-muted-foreground">Split View (Directory + Focused)</span>
                <kbd className="px-2 py-1 rounded bg-muted border border-border font-mono font-semibold text-foreground">2</kbd>
              </div>
              <div className="flex items-center justify-between py-1.5 border-b border-border/40">
                <span className="text-muted-foreground">Strings Explorer (Search & Export)</span>
                <kbd className="px-2 py-1 rounded bg-muted border border-border font-mono font-semibold text-foreground">3</kbd>
              </div>
              <div className="flex items-center justify-between py-1.5 border-b border-border/40">
                <span className="text-muted-foreground">Raw Output Logs</span>
                <kbd className="px-2 py-1 rounded bg-muted border border-border font-mono font-semibold text-foreground">4</kbd>
              </div>
              <div className="flex items-center justify-between py-1.5 border-b border-border/40">
                <span className="text-muted-foreground">Toggle Fullscreen Mode</span>
                <kbd className="px-2 py-1 rounded bg-muted border border-border font-mono font-semibold text-foreground">F</kbd>
              </div>
              <div className="flex items-center justify-between py-1.5 border-b border-border/40">
                <span className="text-muted-foreground">Close Inspector / Modals</span>
                <kbd className="px-2 py-1 rounded bg-muted border border-border font-mono font-semibold text-foreground">Esc</kbd>
              </div>
              <div className="flex items-center justify-between py-1.5">
                <span className="text-muted-foreground">Toggle This Shortcuts Guide</span>
                <kbd className="px-2 py-1 rounded bg-muted border border-border font-mono font-semibold text-foreground">?</kbd>
              </div>
            </div>

            <p className="text-[11px] text-muted-foreground text-center pt-2 border-t border-border/40">
              BinOcular Static Triage Workbench • Press <kbd className="font-mono">Esc</kbd> to close
            </p>
          </div>
        </div>
      )}

      {/* Detail Inspection Modal */}
      {selectedAnalysis && (
        <AnalysisDetailModal
          analysis={selectedAnalysis}
          onClose={() => setSelectedAnalysis(null)}
          onDeleted={(_id) => {
            setSelectedAnalysis(null);
            refreshData(false);
          }}
        />
      )}
    </div>
  );
}
