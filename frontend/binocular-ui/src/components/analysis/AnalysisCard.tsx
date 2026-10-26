import { useState } from 'react';
import type { AnalysisResponse } from '../../api/types';
import {
  FiClock,
  FiCheckCircle,
  FiAlertCircle,
  FiCopy,
  FiCheck,
  FiTrash2,
  FiFileText,
  FiChevronRight,
  FiTerminal,
} from 'react-icons/fi';

interface AnalysisCardProps {
  analysis: AnalysisResponse;
  onSelect: (analysis: AnalysisResponse) => void;
  onDelete?: (id: string) => void;
}

export default function AnalysisCard({
  analysis,
  onSelect,
  onDelete,
}: AnalysisCardProps) {
  const [copied, setCopied] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);

  const copyHash = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (analysis.sha256) {
      navigator.clipboard.writeText(analysis.sha256);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  const handleDelete = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (onDelete && confirm(`Delete analysis for "${analysis.fileName}"?`)) {
      setIsDeleting(true);
      onDelete(analysis.analysisId);
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Completed':
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-950/70 text-emerald-400 border border-emerald-800/60">
            <FiCheckCircle className="w-3 h-3" /> Completed
          </span>
        );
      case 'Running':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-blue-950/70 text-blue-400 border border-blue-800/60 animate-pulse">
            <span className="w-1.5 h-1.5 rounded-full bg-blue-400 animate-ping" /> Running
          </span>
        );
      case 'Queued':
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-950/70 text-amber-400 border border-amber-800/60">
            <FiClock className="w-3 h-3" /> Queued
          </span>
        );
      case 'Failed':
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-rose-950/70 text-rose-400 border border-rose-800/60">
            <FiAlertCircle className="w-3 h-3" /> Failed
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold bg-muted text-muted-foreground border border-border">
            {status}
          </span>
        );
    }
  };

  const getDuration = () => {
    if (!analysis.startedAtUtc || !analysis.completedAtUtc) return null;
    const start = new Date(analysis.startedAtUtc).getTime();
    const end = new Date(analysis.completedAtUtc).getTime();
    const diff = Math.max(0, end - start);
    if (diff < 1000) return `${diff}ms`;
    return `${(diff / 1000).toFixed(1)}s`;
  };

  return (
    <div
      onClick={() => onSelect(analysis)}
      className="group relative bg-card hover:bg-card/90 border border-border hover:border-primary/50 transition-all duration-150 rounded-xl p-4 shadow-sm hover:shadow-md cursor-pointer flex flex-col justify-between gap-3"
    >
      {/* Top row: File name & Status */}
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-3 min-w-0">
          <div className="p-2.5 rounded-lg bg-accent text-accent-foreground border border-border/80 group-hover:border-primary/40 transition-colors flex-shrink-0">
            <FiFileText className="w-5 h-5" />
          </div>
          <div className="min-w-0">
            <h3 className="font-semibold text-base text-foreground tracking-tight truncate group-hover:text-primary transition-colors">
              {analysis.fileName}
            </h3>
            <p className="text-xs text-muted-foreground mt-0.5 flex items-center gap-2">
              <span>{new Date(analysis.createdAtUtc).toLocaleString()}</span>
              {getDuration() && (
                <>
                  <span>•</span>
                  <span className="text-accent-foreground font-mono">{getDuration()}</span>
                </>
              )}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2 flex-shrink-0">
          {getStatusBadge(analysis.status)}
          {onDelete && (
            <button
              onClick={handleDelete}
              disabled={isDeleting}
              title="Delete analysis"
              className="opacity-0 group-hover:opacity-100 p-1.5 text-muted-foreground hover:text-destructive hover:bg-destructive/10 rounded-md transition-all"
            >
              <FiTrash2 className="w-3.5 h-3.5" />
            </button>
          )}
        </div>
      </div>

      {/* SHA-256 snippet */}
      {analysis.sha256 && (
        <div className="flex items-center justify-between text-xs bg-muted/40 border border-border/60 rounded-md px-2.5 py-1.5 font-mono">
          <span className="text-muted-foreground truncate mr-2">
            <span className="text-muted-foreground/60 select-none">SHA256: </span>
            <span className="text-accent-foreground">{analysis.sha256}</span>
          </span>
          <button
            onClick={copyHash}
            title="Copy SHA-256"
            className="text-muted-foreground hover:text-foreground flex items-center gap-1 flex-shrink-0"
          >
            {copied ? (
              <span className="text-emerald-400 flex items-center gap-0.5 text-[10px]">
                <FiCheck className="w-3 h-3" /> Copied
              </span>
            ) : (
              <FiCopy className="w-3 h-3" />
            )}
          </button>
        </div>
      )}

      {/* Error state if failed */}
      {analysis.error && (
        <div className="text-xs text-rose-400 bg-rose-950/30 border border-rose-900/40 rounded px-2.5 py-1 truncate">
          Error: {analysis.error}
        </div>
      )}

      {/* Bottom row: Action link */}
      <div className="flex items-center justify-between pt-1 text-xs text-muted-foreground border-t border-border/40">
        <span className="flex items-center gap-1.5 font-mono text-[11px] text-muted-foreground/80">
          <FiTerminal className="w-3 h-3" />
          ID: {analysis.analysisId.substring(0, 8)}...
        </span>

        <span className="inline-flex items-center gap-1 text-primary group-hover:translate-x-0.5 transition-transform font-medium">
          Inspect Details <FiChevronRight className="w-3.5 h-3.5" />
        </span>
      </div>
    </div>
  );
}