import { useEffect, useState, useMemo } from 'react';
import type { AnalysisResponse, FormattedOutput, OutputSection, ToolResultResponse } from '../../api/types';
import { getToolResults, deleteAnalysis } from '../../api/analysis';
import OutputSectionView from '../results/OutputSectionView';
import StringsExplorer from '../strings/StringsExplorer';
import {
  FiX,
  FiClock,
  FiHash,
  FiFileText,
  FiTrash2,
  FiCheck,
  FiCopy,
  FiAlertCircle,
  FiCheckCircle,
  FiTerminal,
  FiCpu,
  FiLayers,
  FiShield,
  FiActivity,
  FiShare2,
  FiKey,
  FiLock,
  FiBox,
  FiAlertTriangle,
  FiGlobe,
  FiTarget,
  FiMaximize2,
  FiMinimize2,
  FiGrid,
  FiColumns,
  FiSearch,
  FiChevronRight,
  FiChevronLeft,
  FiZoomIn,
} from 'react-icons/fi';

interface AnalysisDetailModalProps {
  analysis: AnalysisResponse;
  onClose: () => void;
  onDeleted?: (id: string) => void;
}

interface ToolSummary {
  badge: string;
  variant: 'risk' | 'warning' | 'info' | 'success' | 'neutral';
  hasFindings: boolean;
  kpi?: string;
}

/**
 * Intelligent frontend formatter that transforms any tool result (structured JSON,
 * legacy DIE outputs, key-value logs, or plain text) into formatted sections.
 */
function autoFormatToolResult(result: ToolResultResponse): FormattedOutput {
  // 1. Try pre-formatted JSON from backend
  if (result.formattedOutputJson) {
    try {
      const parsed: FormattedOutput = JSON.parse(result.formattedOutputJson);
      const secs = parsed.sections || parsed.Sections;
      if (secs && secs.length > 0) {
        return {
          title: parsed.title || parsed.Title || result.toolName,
          sections: secs,
        };
      }
    } catch {
      // Fall through to fallback parser
    }
  }

  const output: FormattedOutput = {
    title: result.toolName || 'Tool Result',
    sections: [],
  };

  const rawOut = (result.standardOutput || '').trim();
  const rawErr = (result.standardError || '').trim();

  if (!rawOut && !rawErr) {
    output.sections.push({
      title: 'Execution Status',
      type: 'text',
      text: 'No output was produced by this tool.',
      fields: [],
      columns: [],
      rows: [],
    });
    return output;
  }

  // 2. Try parsing raw stdout as JSON
  if (rawOut.startsWith('{') || rawOut.startsWith('[')) {
    try {
      const json = JSON.parse(rawOut);

      // DIE specific shape fallback
      if (json.status !== undefined || json.total !== undefined || json.records) {
        output.sections.push({
          title: 'File Summary',
          type: 'keyValue',
          fields: [
            { label: 'Packing Status', value: String(json.status || 'Unknown') },
            { label: 'Total Entropy', value: json.total ? Number(json.total).toFixed(4) : 'Unknown' },
          ],
          columns: [],
          rows: [],
        });

        if (Array.isArray(json.records) && json.records.length > 0) {
          const rows = json.records.map((r: Record<string, unknown>) => [
            String(r.name || 'Unknown'),
            String(r.offset || '0'),
            String(r.size || '0'),
            r.entropy !== undefined ? Number(r.entropy).toFixed(4) : '0.0000',
            String(r.status || 'Unknown'),
          ]);
          output.sections.push({
            title: 'Entropy Records',
            type: 'table',
            columns: ['Name', 'Offset', 'Size', 'Entropy', 'Status'],
            rows: rows,
            fields: [],
          });
        }
        return output;
      }

      // Generic Object: extract key-value fields and table arrays
      if (typeof json === 'object' && !Array.isArray(json)) {
        const kvFields: { label: string; value: string }[] = [];

        for (const [key, val] of Object.entries(json)) {
          if (val === null || val === undefined) continue;
          if (typeof val === 'string' || typeof val === 'number' || typeof val === 'boolean') {
            kvFields.push({ label: formatLabel(key), value: String(val) });
          } else if (Array.isArray(val) && val.length > 0 && typeof val[0] === 'object') {
            const cols = Object.keys(val[0]);
            const rows = val.map((item: Record<string, unknown>) => cols.map((c) => String(item[c] ?? '')));
            output.sections.push({
              title: formatLabel(key),
              type: 'table',
              columns: cols.map(formatLabel),
              rows: rows,
              fields: [],
            });
          }
        }

        if (kvFields.length > 0) {
          output.sections.unshift({
            title: 'General Properties',
            type: 'keyValue',
            fields: kvFields,
            columns: [],
            rows: [],
          });
        }

        if (output.sections.length > 0) return output;
      }
    } catch {
      // Continue to text parsing
    }
  }

  // 3. Check for 64-character SHA-256 hash
  if (/^[a-fA-F0-9]{64}$/.test(rawOut)) {
    output.sections.push({
      title: 'Cryptographic Hash',
      type: 'keyValue',
      fields: [
        { label: 'SHA-256 Digest', value: rawOut.toLowerCase() },
        { label: 'Algorithm', value: 'SHA-256 (256-bit)' },
      ],
      columns: [],
      rows: [],
    });
    return output;
  }

  // 4. Check for line-based key-value pairs (e.g. "Key: Value")
  const lines = rawOut.split('\n').filter((l) => l.trim().length > 0);
  const kvLines = lines.filter((l) => l.includes(': ') && !l.startsWith('http'));

  if (kvLines.length > 0 && kvLines.length >= lines.length / 2) {
    const fields = kvLines.map((l) => {
      const idx = l.indexOf(': ');
      return {
        label: l.substring(0, idx).trim(),
        value: l.substring(idx + 2).trim(),
      };
    });

    output.sections.push({
      title: 'Extracted Properties',
      type: 'keyValue',
      fields: fields,
      columns: [],
      rows: [],
    });
    return output;
  }

  // 5. Default text output formatted into a clean terminal section
  output.sections.push({
    title: `${result.toolName} Output`,
    type: 'text',
    text: rawOut || rawErr,
    fields: [],
    columns: [],
    rows: [],
  });

  return output;
}

function formatLabel(str: string): string {
  return str
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, (s) => s.toUpperCase())
    .trim();
}

/**
 * Extracts a concise summary, severity variant, and finding indicator from a tool's output.
 */
function extractToolSummary(result: ToolResultResponse, formatted: FormattedOutput): ToolSummary {
  if (result.exitCode !== 0) {
    return {
      badge: `Exit: ${result.exitCode}`,
      variant: 'risk',
      hasFindings: true,
      kpi: 'Error',
    };
  }

  const name = result.toolName.toLowerCase();
  const rawOut = result.standardOutput || '';

  // 1. Threat Scorer
  if (name.includes('threat') || name.includes('scorer') || name.includes('heuristic')) {
    for (const sec of formatted.sections) {
      for (const f of sec.fields || []) {
        if (f.label.toLowerCase().includes('score')) {
          const num = parseInt(f.value, 10);
          if (!isNaN(num)) {
            return {
              badge: `Score: ${f.value}`,
              variant: num >= 70 ? 'risk' : num >= 45 ? 'warning' : 'success',
              hasFindings: num >= 20,
              kpi: `${num}/100`,
            };
          }
        }
      }
    }
  }

  // 2. Sections - check for RWX
  if (name.includes('section')) {
    let hasRwx = false;
    let sectionCount = 0;
    for (const sec of formatted.sections) {
      if (sec.type?.toLowerCase() === 'table') {
        sectionCount = sec.rows?.length || 0;
        for (const row of sec.rows || []) {
          for (const cell of row) {
            if (cell.includes('RWX') || (cell.includes('W') && cell.includes('X') && cell.length <= 5)) {
              hasRwx = true;
              break;
            }
          }
        }
      }
    }
    if (hasRwx) {
      return { badge: 'RWX Flagged!', variant: 'risk', hasFindings: true, kpi: 'RWX' };
    }
    return {
      badge: `${sectionCount || 'All'} Sections Clean`,
      variant: 'neutral',
      hasFindings: false,
    };
  }

  // 3. MITRE ATT&CK
  if (name.includes('mitre') || name.includes('attack')) {
    let techCount = 0;
    for (const sec of formatted.sections) {
      if (sec.type?.toLowerCase() === 'table') {
        techCount = sec.rows?.length || 0;
      }
    }
    if (techCount > 0) {
      return {
        badge: `${techCount} ATT&CK Techs`,
        variant: 'risk',
        hasFindings: true,
        kpi: `${techCount} Tech`,
      };
    }
    return { badge: 'No Techniques', variant: 'neutral', hasFindings: false };
  }

  // 4. Network IOCs
  if (name.includes('network') || name.includes('endpoint')) {
    let epCount = 0;
    for (const sec of formatted.sections) {
      if (sec.type?.toLowerCase() === 'table') {
        epCount = sec.rows?.length || 0;
      }
    }
    if (epCount > 0) {
      return {
        badge: `${epCount} Endpoints / IOCs`,
        variant: 'warning',
        hasFindings: true,
        kpi: `${epCount} IOCs`,
      };
    }
    return { badge: 'No Endpoints', variant: 'neutral', hasFindings: false };
  }

  // 5. Hardcoded Secrets & Certificates
  if (name.includes('secret') || name.includes('certificate')) {
    let secCount = 0;
    for (const sec of formatted.sections) {
      if (sec.type?.toLowerCase() === 'table') {
        secCount = sec.rows?.length || 0;
      }
    }
    if (secCount > 0) {
      return {
        badge: `${secCount} Secrets Leaked`,
        variant: 'risk',
        hasFindings: true,
        kpi: `${secCount} Secrets`,
      };
    }
    return { badge: 'No Leaked Secrets', variant: 'neutral', hasFindings: false };
  }

  // 6. Embedded Assets & Droppers
  if (name.includes('embedded') || name.includes('asset')) {
    let hasDropper = false;
    let assetCount = 0;
    for (const sec of formatted.sections) {
      if (sec.type?.toLowerCase() === 'table') {
        assetCount = sec.rows?.length || 0;
        for (const row of sec.rows || []) {
          for (const cell of row) {
            if (cell.toLowerCase().includes('dropper') || cell.toLowerCase().includes('secondary pe')) {
              hasDropper = true;
            }
          }
        }
      }
    }
    if (hasDropper) {
      return { badge: 'Secondary PE Dropper!', variant: 'risk', hasFindings: true, kpi: 'Dropper' };
    }
    if (assetCount > 0) {
      return { badge: `${assetCount} Embedded Assets`, variant: 'warning', hasFindings: true };
    }
    return { badge: 'No Embedded Files', variant: 'neutral', hasFindings: false };
  }

  // 7. Assembly / Shellcode / Opcode
  if (name.includes('opcode') || name.includes('assembly') || name.includes('shellcode')) {
    let findingCount = 0;
    for (const sec of formatted.sections) {
      if (sec.type?.toLowerCase() === 'table') {
        findingCount = sec.rows?.length || 0;
      }
    }
    if (findingCount > 0) {
      return {
        badge: `${findingCount} Shellcode Stubs`,
        variant: 'risk',
        hasFindings: true,
        kpi: `${findingCount} Stubs`,
      };
    }
    return { badge: 'Clean Assembly', variant: 'neutral', hasFindings: false };
  }

  // 8. Crypto Algorithms & Constants
  if (name.includes('crypto') && name.includes('constant')) {
    let count = 0;
    for (const sec of formatted.sections) {
      if (sec.type?.toLowerCase() === 'table') {
        count = sec.rows?.length || 0;
      }
    }
    if (count > 0) {
      return { badge: `${count} Crypto Constants`, variant: 'info', hasFindings: true };
    }
    return { badge: 'No Constants', variant: 'neutral', hasFindings: false };
  }

  // 9. Strings & IOCs
  if (name.includes('string')) {
    let count = 0;
    for (const sec of formatted.sections) {
      if (sec.type?.toLowerCase() === 'table') {
        count = sec.rows?.length || 0;
      }
      for (const f of sec.fields || []) {
        if (f.label.toLowerCase().includes('suspicious')) {
          const num = parseInt(f.value, 10);
          if (!isNaN(num) && num > 0) count = num;
        }
      }
    }
    if (count > 0) {
      return { badge: `${count} Suspicious Strings`, variant: 'warning', hasFindings: true };
    }
    return { badge: 'No Threat Strings', variant: 'neutral', hasFindings: false };
  }

  // 10. Hashes & Entropy
  if (name.includes('hash') || name.includes('entropy')) {
    for (const sec of formatted.sections) {
      for (const f of sec.fields || []) {
        if (f.label.toLowerCase().includes('entropy')) {
          const num = parseFloat(f.value);
          if (!isNaN(num)) {
            const isHigh = num >= 7.2;
            return {
              badge: `Entropy: ${num.toFixed(2)}`,
              variant: isHigh ? 'warning' : 'info',
              hasFindings: isHigh,
              kpi: num.toFixed(2),
            };
          }
        }
      }
    }
  }

  // 11. Format & Architecture
  if (name.includes('format') || name.includes('architecture')) {
    let arch = '';
    let fmt = '';
    for (const sec of formatted.sections) {
      for (const f of sec.fields || []) {
        if (f.label.toLowerCase().includes('machine') || f.label.toLowerCase().includes('arch')) {
          arch = f.value;
        }
        if (f.label.toLowerCase().includes('format')) {
          fmt = f.value;
        }
      }
    }
    return {
      badge: arch || fmt || 'Binary Format',
      variant: 'neutral',
      hasFindings: false,
    };
  }

  // 12. Compiler & Timestamp
  if (name.includes('compiler') || name.includes('timestamp')) {
    let comp = '';
    for (const sec of formatted.sections) {
      for (const f of sec.fields || []) {
        if (f.label.toLowerCase().includes('compiler') || f.label.toLowerCase().includes('runtime')) {
          comp = f.value;
        }
      }
    }
    return {
      badge: comp || 'Compiled Binary',
      variant: 'neutral',
      hasFindings: false,
    };
  }

  // 13. DIE
  if (name.includes('die') || name.includes('detect')) {
    if (rawOut.toLowerCase().includes('packed')) {
      return { badge: 'Packed Binary', variant: 'warning', hasFindings: true };
    }
    return { badge: 'DIE Unpacked', variant: 'neutral', hasFindings: false };
  }

  // 14. SHA-256
  if (name.includes('sha')) {
    return { badge: 'SHA-256 Verified', variant: 'neutral', hasFindings: false };
  }

  return { badge: 'Inspection Complete', variant: 'neutral', hasFindings: false };
}

function getBadgeClasses(variant: ToolSummary['variant']) {
  switch (variant) {
    case 'risk':
      return 'bg-rose-950/80 text-rose-300 border-rose-800/80 font-semibold';
    case 'warning':
      return 'bg-amber-950/80 text-amber-300 border-amber-800/80 font-semibold';
    case 'info':
      return 'bg-blue-950/80 text-blue-300 border-blue-800/80 font-semibold';
    case 'success':
      return 'bg-emerald-950/80 text-emerald-300 border-emerald-800/80';
    default:
      return 'bg-muted text-muted-foreground border-border/80';
  }
}

export default function AnalysisDetailModal({
  analysis,
  onClose,
  onDeleted,
}: AnalysisDetailModalProps) {
  const [dataState, setDataState] = useState<{
    analysisId: string | null;
    toolResults: ToolResultResponse[];
    loading: boolean;
    error: string | null;
  }>({
    analysisId: null,
    toolResults: [],
    loading: true,
    error: null,
  });

  const [copiedHash, setCopiedHash] = useState(false);
  const [copiedReport, setCopiedReport] = useState(false);
  const [viewMode, setViewMode] = useState<'grid' | 'split' | 'strings' | 'raw'>('grid');
  const [selectedToolName, setSelectedToolName] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [onlyFindings, setOnlyFindings] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(true);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    let isMounted = true;

    getToolResults(analysis.analysisId)
      .then((results) => {
        if (isMounted) {
          setDataState({
            analysisId: analysis.analysisId,
            toolResults: results,
            loading: false,
            error: null,
          });
          if (results.length > 0 && !selectedToolName) {
            setSelectedToolName(results[0].toolName);
          }
        }
      })
      .catch((err: unknown) => {
        if (isMounted) {
          setDataState({
            analysisId: analysis.analysisId,
            toolResults: [],
            loading: false,
            error: err instanceof Error ? err.message : 'Failed to load analysis tool results',
          });
        }
      });

    return () => {
      isMounted = false;
    };
  }, [analysis.analysisId, selectedToolName]);

  // Keyboard navigation & shortcuts
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) {
        if (e.key === 'Escape') {
          (e.target as HTMLElement).blur();
        }
        return;
      }

      if (e.key === 'Escape') {
        onClose();
      } else if (e.key === 'f' || e.key === 'F') {
        setIsFullscreen((prev) => !prev);
      } else if (e.key === '1') {
        setViewMode('grid');
      } else if (e.key === '2') {
        setViewMode('split');
      } else if (e.key === '3') {
        setViewMode('strings');
      } else if (e.key === '4') {
        setViewMode('raw');
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  // Lock background page scroll while modal is open to eliminate background ghost scrollbar
  useEffect(() => {
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = prevOverflow;
    };
  }, []);

  const isLoading = dataState.loading || dataState.analysisId !== analysis.analysisId;
  const toolResults = dataState.toolResults;
  const error = dataState.error;

  // Memoize formatted outputs and summary info for all tools
  const processedTools = useMemo(() => {
    return toolResults.map((result) => {
      const formatted = autoFormatToolResult(result);
      const summary = extractToolSummary(result, formatted);
      return {
        result,
        formatted,
        summary,
      };
    });
  }, [toolResults]);

  // High-level executive findings across the entire file
  const executiveSummary = useMemo(() => {
    let threatScore: string | null = null;
    let threatRisk: string | null = null;
    let hasRwx = false;
    let mitreCount = 0;
    let iocCount = 0;
    let dropperDetected = false;

    for (const item of processedTools) {
      const name = item.result.toolName.toLowerCase();
      if (name.includes('threat') || name.includes('scorer')) {
        for (const sec of item.formatted.sections) {
          for (const f of sec.fields || []) {
            if (f.label.toLowerCase().includes('score')) threatScore = f.value;
            if (f.label.toLowerCase().includes('classification') || f.label.toLowerCase().includes('verdict')) {
              threatRisk = f.value;
            }
          }
        }
      }
      if (name.includes('section') && item.summary.badge.includes('RWX')) hasRwx = true;
      if (name.includes('mitre') && item.summary.kpi) {
        mitreCount = parseInt(item.summary.kpi, 10) || 0;
      }
      if (name.includes('network') && item.summary.kpi) {
        iocCount = parseInt(item.summary.kpi, 10) || 0;
      }
      if (name.includes('embedded') && item.summary.badge.includes('Dropper')) {
        dropperDetected = true;
      }
    }

    return {
      threatScore,
      threatRisk,
      hasRwx,
      mitreCount,
      iocCount,
      dropperDetected,
    };
  }, [processedTools]);

  // Filtered tools based on search and findings filter
  const filteredTools = useMemo(() => {
    return processedTools.filter((item) => {
      if (onlyFindings && !item.summary.hasFindings) {
        return false;
      }
      if (!searchQuery.trim()) {
        return true;
      }
      const q = searchQuery.toLowerCase();
      const matchName = item.result.toolName.toLowerCase().includes(q);
      const matchBadge = item.summary.badge.toLowerCase().includes(q);
      const matchRaw = (item.result.standardOutput || '').toLowerCase().includes(q);
      return matchName || matchBadge || matchRaw;
    });
  }, [processedTools, searchQuery, onlyFindings]);

  const focusedTool = useMemo(() => {
    return processedTools.find((t) => t.result.toolName === selectedToolName) || processedTools[0];
  }, [processedTools, selectedToolName]);

  const copyHash = () => {
    if (analysis.sha256) {
      navigator.clipboard.writeText(analysis.sha256);
      setCopiedHash(true);
      setTimeout(() => setCopiedHash(false), 2000);
    }
  };

  const handleCopyReport = () => {
    let md = `# BinOcular Analysis Briefing\n\n`;
    md += `**Target File**: \`${analysis.fileName}\`\n`;
    md += `**Analysis ID**: \`${analysis.analysisId}\`\n`;
    md += `**Status**: ${analysis.status}\n`;
    if (analysis.sha256) md += `**SHA-256**: \`${analysis.sha256}\`\n`;
    md += `**Created**: ${analysis.createdAtUtc}\n`;
    const dur = calculateDuration();
    if (dur) md += `**Execution Time**: ${dur}\n`;
    md += `\n---\n\n`;

    md += `## Executive Triage Summary\n\n`;
    if (executiveSummary.threatScore) {
      md += `- **Threat Score**: ${executiveSummary.threatScore} (${executiveSummary.threatRisk || 'Unknown'})\n`;
    }
    if (executiveSummary.hasRwx) {
      md += `- **RWX Memory Sections**: Detected (High-Risk In-Memory Injection Primitive)\n`;
    }
    if (executiveSummary.dropperDetected) {
      md += `- **Dropper Detection**: Secondary Executable / Dropper Payload Embedded!\n`;
    }
    if (executiveSummary.mitreCount > 0) {
      md += `- **MITRE ATT&CK**: ${executiveSummary.mitreCount} Attack Techniques Identified\n`;
    }
    if (executiveSummary.iocCount > 0) {
      md += `- **Network IOCs**: ${executiveSummary.iocCount} External Endpoints Identified\n`;
    }
    md += `\n---\n\n`;

    md += `## Tool Findings (${processedTools.length} Inspection Tools)\n\n`;
    for (const tool of processedTools) {
      md += `### ${tool.result.toolName} [${tool.summary.badge}]\n\n`;
      for (const section of tool.formatted.sections) {
        if (section.title) md += `#### ${section.title}\n\n`;
        if (section.text) md += `${section.text}\n\n`;
        if (section.fields && section.fields.length > 0) {
          for (const f of section.fields) {
            md += `- **${f.label}**: ${f.value}\n`;
          }
          md += `\n`;
        }
        if (section.columns && section.columns.length > 0 && section.rows && section.rows.length > 0) {
          md += `| ${section.columns.join(' | ')} |\n`;
          md += `| ${section.columns.map(() => '---').join(' | ')} |\n`;
          for (const row of section.rows.slice(0, 30)) {
            md += `| ${row.map((cell) => String(cell).replace(/\|/g, '\\|')).join(' | ')} |\n`;
          }
          if (section.rows.length > 30) {
            md += `\n*(... ${section.rows.length - 30} additional rows omitted for briefing summary)*\n`;
          }
          md += `\n`;
        }
      }
      md += `\n`;
    }

    navigator.clipboard.writeText(md);
    setCopiedReport(true);
    setTimeout(() => setCopiedReport(false), 2000);
  };

  const handleDelete = async () => {
    if (!confirm(`Are you sure you want to delete analysis for "${analysis.fileName}"?`)) {
      return;
    }
    setDeleting(true);
    try {
      await deleteAnalysis(analysis.analysisId);
      onDeleted?.(analysis.analysisId);
      onClose();
    } catch (err: unknown) {
      alert(`Delete failed: ${err instanceof Error ? err.message : String(err)}`);
      setDeleting(false);
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Completed':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-950/80 text-emerald-400 border border-emerald-800/60">
            <FiCheckCircle className="w-3.5 h-3.5" /> Completed
          </span>
        );
      case 'Running':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-blue-950/80 text-blue-400 border border-blue-800/60 animate-pulse">
            <span className="w-2 h-2 rounded-full bg-blue-400 animate-ping" /> Running
          </span>
        );
      case 'Queued':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-amber-950/80 text-amber-400 border border-amber-800/60">
            <FiClock className="w-3.5 h-3.5" /> Queued
          </span>
        );
      case 'Failed':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-rose-950/80 text-rose-400 border border-rose-800/60">
            <FiAlertCircle className="w-3.5 h-3.5" /> Failed
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-muted text-muted-foreground border border-border">
            {status}
          </span>
        );
    }
  };

  const calculateDuration = () => {
    if (!analysis.startedAtUtc || !analysis.completedAtUtc) return null;
    const start = new Date(analysis.startedAtUtc).getTime();
    const end = new Date(analysis.completedAtUtc).getTime();
    const diffMs = Math.max(0, end - start);
    if (diffMs < 1000) return `${diffMs}ms`;
    return `${(diffMs / 1000).toFixed(2)}s`;
  };

  const getToolIcon = (name: string, className = 'w-4 h-4') => {
    const lower = name.toLowerCase();
    if (lower.includes('format') || lower.includes('architecture')) {
      return <FiShield className={className} />;
    }
    if (lower.includes('hash') || lower.includes('sha')) {
      return <FiHash className={className} />;
    }
    if (lower.includes('section')) {
      return <FiLayers className={className} />;
    }
    if (lower.includes('import') || lower.includes('depend')) {
      return <FiShare2 className={className} />;
    }
    if (lower.includes('string') || lower.includes('ioc')) {
      return <FiFileText className={className} />;
    }
    if (lower.includes('crypto') || lower.includes('constant')) {
      return <FiLock className={className} />;
    }
    if (lower.includes('secret') || lower.includes('certificate')) {
      return <FiKey className={className} />;
    }
    if (lower.includes('embedded') || lower.includes('asset')) {
      return <FiBox className={className} />;
    }
    if (lower.includes('compiler') || lower.includes('timestamp')) {
      return <FiClock className={className} />;
    }
    if (lower.includes('threat') || lower.includes('scorer') || lower.includes('heuristic')) {
      return <FiAlertTriangle className={`${className} text-amber-400`} />;
    }
    if (lower.includes('opcode') || lower.includes('assembly') || lower.includes('shellcode')) {
      return <FiTerminal className={className} />;
    }
    if (lower.includes('network') || lower.includes('endpoint')) {
      return <FiGlobe className={className} />;
    }
    if (lower.includes('mitre') || lower.includes('attack')) {
      return <FiTarget className={`${className} text-rose-400`} />;
    }
    if (lower.includes('die') || lower.includes('detect it easy')) {
      return <FiActivity className={className} />;
    }
    return <FiCpu className={className} />;
  };

  const navigateTool = (direction: 'next' | 'prev') => {
    if (!processedTools.length) return;
    const currentIndex = processedTools.findIndex((t) => t.result.toolName === focusedTool?.result.toolName);
    if (currentIndex === -1) return;
    const nextIndex = direction === 'next'
      ? (currentIndex + 1) % processedTools.length
      : (currentIndex - 1 + processedTools.length) % processedTools.length;
    setSelectedToolName(processedTools[nextIndex].result.toolName);
  };

  return (
    <div
      className={
        isFullscreen
          ? 'fixed inset-0 z-50 flex flex-col bg-background text-foreground animate-in fade-in duration-150 overflow-hidden'
          : 'fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-5 md:p-6 bg-black/85 backdrop-blur-md animate-in fade-in duration-200 overflow-hidden'
      }
      onClick={isFullscreen ? undefined : onClose}
    >
      <div
        className={
          isFullscreen
            ? 'flex flex-col w-full h-full bg-background overflow-hidden'
            : 'relative flex flex-col w-full h-[96vh] max-w-[96vw] bg-card text-card-foreground border border-border rounded-2xl shadow-2xl overflow-hidden'
        }
        onClick={(e) => e.stopPropagation()}
      >
        {/* ==================== Top Header Bar ==================== */}
        <header className="flex flex-wrap items-center justify-between px-5 py-3.5 border-b border-border bg-card/95 gap-3 flex-shrink-0">
          <div className="flex items-center gap-3.5 min-w-0">
            <div className="p-2.5 rounded-xl bg-primary/10 text-primary border border-primary/20 flex-shrink-0">
              <FiCpu className="w-5 h-5" />
            </div>
            <div className="min-w-0">
              <div className="flex items-center gap-2.5">
                <h1 className="text-lg font-bold tracking-tight text-foreground truncate max-w-sm sm:max-w-md md:max-w-xl">
                  {analysis.fileName}
                </h1>
                {getStatusBadge(analysis.status)}
              </div>
              <div className="flex items-center gap-3 text-xs text-muted-foreground mt-0.5">
                <span>
                  ID: <span className="font-mono">{analysis.analysisId.substring(0, 8)}...</span>
                </span>
                <span>•</span>
                <span>{new Date(analysis.createdAtUtc).toLocaleTimeString()}</span>
                {calculateDuration() && (
                  <>
                    <span>•</span>
                    <span className="font-mono text-emerald-400">{calculateDuration()}</span>
                  </>
                )}
              </div>
            </div>
          </div>

          {/* Quick SHA-256 Copy Chip */}
          {analysis.sha256 && (
            <div className="hidden lg:flex items-center gap-2 px-3 py-1.5 rounded-lg bg-muted/40 border border-border/80 text-xs font-mono">
              <FiHash className="text-primary flex-shrink-0" />
              <span className="text-muted-foreground truncate max-w-[140px] xl:max-w-[200px]">
                {analysis.sha256}
              </span>
              <button
                onClick={copyHash}
                className="p-1 hover:text-foreground text-muted-foreground transition-colors"
                title="Copy full SHA-256 digest"
              >
                {copiedHash ? (
                  <FiCheck className="w-3.5 h-3.5 text-emerald-400" />
                ) : (
                  <FiCopy className="w-3.5 h-3.5" />
                )}
              </button>
            </div>
          )}

          {/* Top Actions: View Switcher & Controls */}
          <div className="flex items-center gap-2">
            {/* View Mode Segmented Control */}
            <div className="flex items-center p-0.5 bg-muted/70 rounded-lg border border-border/80 text-xs font-medium">
              <button
                onClick={() => setViewMode('grid')}
                className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md transition-all ${
                  viewMode === 'grid'
                    ? 'bg-primary text-primary-foreground shadow-sm font-semibold'
                    : 'text-muted-foreground hover:text-foreground'
                }`}
                title="Grid View [1]: See all tool inspection cards side-by-side at once"
              >
                <FiGrid className="w-3.5 h-3.5" />
                <span>Grid</span>
                <span className="hidden sm:inline px-1 py-0.2 rounded text-[10px] bg-black/20 text-muted-foreground font-mono">1</span>
              </button>

              <button
                onClick={() => setViewMode('split')}
                className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md transition-all ${
                  viewMode === 'split'
                    ? 'bg-primary text-primary-foreground shadow-sm font-semibold'
                    : 'text-muted-foreground hover:text-foreground'
                }`}
                title="Split Inspector [2]: Sidebar directory listing all tools + focused inspection"
              >
                <FiColumns className="w-3.5 h-3.5" />
                <span>Split</span>
                <span className="hidden sm:inline px-1 py-0.2 rounded text-[10px] bg-black/20 text-muted-foreground font-mono">2</span>
              </button>

              <button
                onClick={() => setViewMode('strings')}
                className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md transition-all ${
                  viewMode === 'strings'
                    ? 'bg-primary text-primary-foreground shadow-sm font-semibold'
                    : 'text-muted-foreground hover:text-foreground'
                }`}
                title="Strings Explorer [3]: Search, filter, paginate, and export all extracted strings"
              >
                <FiFileText className="w-3.5 h-3.5" />
                <span>Strings</span>
                <span className="hidden sm:inline px-1 py-0.2 rounded text-[10px] bg-black/20 text-muted-foreground font-mono">3</span>
              </button>

              <button
                onClick={() => setViewMode('raw')}
                className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md transition-all ${
                  viewMode === 'raw'
                    ? 'bg-primary text-primary-foreground shadow-sm font-semibold'
                    : 'text-muted-foreground hover:text-foreground'
                }`}
                title="Raw logs [4]: Direct command line output streams"
              >
                <FiTerminal className="w-3.5 h-3.5" />
                <span>Raw</span>
                <span className="hidden sm:inline px-1 py-0.2 rounded text-[10px] bg-black/20 text-muted-foreground font-mono">4</span>
              </button>
            </div>

            {/* Export Markdown Report */}
            <button
              onClick={handleCopyReport}
              disabled={isLoading || processedTools.length === 0}
              className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg border text-xs font-medium transition-all ${
                copiedReport
                  ? 'bg-emerald-950/80 text-emerald-300 border-emerald-800'
                  : 'bg-muted/50 hover:bg-muted text-muted-foreground hover:text-foreground border-border/80'
              }`}
              title="Copy complete analysis briefing in Markdown format to clipboard"
            >
              {copiedReport ? (
                <>
                  <FiCheck className="w-3.5 h-3.5 text-emerald-400" />
                  <span>Report Copied!</span>
                </>
              ) : (
                <>
                  <FiShare2 className="w-3.5 h-3.5" />
                  <span className="hidden sm:inline">Export Report</span>
                </>
              )}
            </button>

            {/* Fullscreen Toggle */}
            <button
              onClick={() => setIsFullscreen(!isFullscreen)}
              className="p-2 text-muted-foreground hover:text-foreground hover:bg-muted rounded-lg transition-colors border border-transparent hover:border-border"
              title={isFullscreen ? 'Restore window (F)' : 'Expand to full screen (F)'}
            >
              {isFullscreen ? <FiMinimize2 className="w-4 h-4" /> : <FiMaximize2 className="w-4 h-4" />}
            </button>

            {/* Delete Analysis */}
            <button
              onClick={handleDelete}
              disabled={deleting}
              className="p-2 text-muted-foreground hover:text-destructive hover:bg-destructive/10 rounded-lg transition-colors border border-transparent hover:border-destructive/20"
              title="Delete this analysis"
            >
              <FiTrash2 className="w-4 h-4" />
            </button>

            {/* Close Button */}
            <button
              onClick={onClose}
              className="p-2 text-muted-foreground hover:text-foreground hover:bg-muted rounded-lg transition-colors border border-transparent hover:border-border"
              title="Close (Esc)"
            >
              <FiX className="w-5 h-5" />
            </button>
          </div>
        </header>

        {/* ==================== Executive Triage Strip ==================== */}
        <section className="px-5 py-2.5 bg-muted/25 border-b border-border text-xs flex flex-wrap items-center justify-between gap-3 flex-shrink-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-semibold text-muted-foreground uppercase tracking-wider text-[11px] mr-1">
              File Triage:
            </span>

            {/* Threat Score Flag */}
            {executiveSummary.threatScore && (
              <span
                className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md border font-mono font-semibold ${
                  parseInt(executiveSummary.threatScore, 10) >= 70
                    ? 'bg-rose-950/80 text-rose-300 border-rose-800'
                    : parseInt(executiveSummary.threatScore, 10) >= 45
                    ? 'bg-amber-950/80 text-amber-300 border-amber-800'
                    : 'bg-emerald-950/80 text-emerald-300 border-emerald-800'
                }`}
              >
                <FiAlertTriangle className="w-3.5 h-3.5" />
                Score: {executiveSummary.threatScore}
              </span>
            )}

            {/* RWX Flag */}
            {executiveSummary.hasRwx && (
              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-rose-950/80 text-rose-300 border border-rose-800 font-semibold animate-pulse">
                <FiLayers className="w-3.5 h-3.5" /> RWX Section Flagged
              </span>
            )}

            {/* ATT&CK Flag */}
            {executiveSummary.mitreCount > 0 && (
              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-purple-950/80 text-purple-300 border border-purple-800 font-semibold">
                <FiTarget className="w-3.5 h-3.5" /> {executiveSummary.mitreCount} ATT&CK Techs
              </span>
            )}

            {/* Network IOCs Flag */}
            {executiveSummary.iocCount > 0 && (
              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-blue-950/80 text-blue-300 border border-blue-800 font-semibold">
                <FiGlobe className="w-3.5 h-3.5" /> {executiveSummary.iocCount} Network IOCs
              </span>
            )}

            {/* Dropper Flag */}
            {executiveSummary.dropperDetected && (
              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-rose-950/80 text-rose-300 border border-rose-800 font-semibold">
                <FiBox className="w-3.5 h-3.5" /> Secondary PE Dropper
              </span>
            )}

            <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-card text-muted-foreground border border-border/80 font-mono">
              {processedTools.length} Tools Executed
            </span>
          </div>

          {/* Quick Search & Filter Inputs */}
          <div className="flex items-center gap-2">
            <div className="relative">
              <FiSearch className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-muted-foreground" />
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Search tools or findings..."
                className="pl-8 pr-3 py-1 bg-card text-xs text-foreground placeholder:text-muted-foreground/70 rounded-md border border-border/80 focus:outline-none focus:border-primary w-44 sm:w-60"
              />
              {searchQuery && (
                <button
                  onClick={() => setSearchQuery('')}
                  className="absolute right-2 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground text-xs"
                >
                  ✕
                </button>
              )}
            </div>

            <button
              onClick={() => setOnlyFindings(!onlyFindings)}
              className={`px-2.5 py-1 rounded-md text-xs font-medium border transition-colors flex items-center gap-1.5 ${
                onlyFindings
                  ? 'bg-amber-950/80 text-amber-300 border-amber-800'
                  : 'bg-card text-muted-foreground border-border/80 hover:text-foreground'
              }`}
              title="Show only tools that identified threats or anomalies"
            >
              <FiAlertTriangle className="w-3 h-3" />
              <span>Findings Only</span>
            </button>
          </div>
        </section>

        {/* ==================== Main Content Workspace ==================== */}
        <main className="flex-1 overflow-hidden flex flex-col relative bg-background">
          {isLoading ? (
            <div className="flex flex-col items-center justify-center py-32 text-muted-foreground">
              <div className="w-9 h-9 border-3 border-primary border-t-transparent rounded-full animate-spin mb-3" />
              <p className="text-sm font-medium">Extracting binary inspection results...</p>
            </div>
          ) : error ? (
            <div className="m-6 p-4 bg-destructive/10 border border-destructive/30 rounded-xl text-sm text-destructive flex items-center gap-3">
              <FiAlertCircle className="w-5 h-5 flex-shrink-0" />
              <span>{error}</span>
            </div>
          ) : processedTools.length === 0 ? (
            <div className="text-center py-32 text-muted-foreground">
              <FiFileText className="w-12 h-12 mx-auto mb-3 opacity-40" />
              <p className="text-base font-semibold">No tool outputs recorded yet.</p>
              <p className="text-xs text-muted-foreground mt-1">
                The automated static inspection worker is currently analyzing the file.
              </p>
            </div>
          ) : viewMode === 'strings' ? (
            /* ==================== Strings Explorer View ==================== */
            <StringsExplorer analysisId={analysis.analysisId} fileName={analysis.fileName} />
          ) : viewMode === 'raw' ? (
            /* ==================== 1. Raw Output View ==================== */
            <div className="flex-1 overflow-y-auto p-6 space-y-6">
              {filteredTools.map(({ result }) => (
                <div key={result.id} className="bg-card/80 border border-border/80 rounded-xl p-5 shadow-sm space-y-3">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <span className="p-1.5 rounded-lg bg-primary/10 text-primary">
                        {getToolIcon(result.toolName)}
                      </span>
                      <span className="font-semibold text-sm text-foreground">{result.toolName}</span>
                    </div>
                    <span
                      className={`text-xs px-2.5 py-0.5 rounded font-mono ${
                        result.exitCode === 0
                          ? 'bg-emerald-950 text-emerald-400 border border-emerald-800'
                          : 'bg-rose-950 text-rose-400 border border-rose-800'
                      }`}
                    >
                      exit: {result.exitCode}
                    </span>
                  </div>

                  {result.standardOutput && (
                    <div>
                      <div className="text-xs text-muted-foreground mb-1 font-semibold">Standard Output:</div>
                      <pre className="bg-[#050608] text-emerald-400/95 p-3.5 rounded-lg border border-border text-xs font-mono whitespace-pre-wrap break-all max-h-80 overflow-y-auto">
                        {result.standardOutput}
                      </pre>
                    </div>
                  )}

                  {result.standardError && (
                    <div>
                      <div className="text-xs text-rose-400 mb-1 font-semibold">Standard Error:</div>
                      <pre className="bg-[#050608] text-rose-300 p-3.5 rounded-lg border border-rose-950 text-xs font-mono whitespace-pre-wrap break-all max-h-56 overflow-y-auto">
                        {result.standardError}
                      </pre>
                    </div>
                  )}
                </div>
              ))}
            </div>
          ) : viewMode === 'split' ? (
            /* ==================== 2. Split Workspace (Sidebar Directory + Deep Inspector) ==================== */
            <div className="flex-1 overflow-hidden flex flex-row">
              {/* Left Master Sidebar: All 15 tools visible at once */}
              <aside className="w-80 xl:w-96 flex-shrink-0 border-r border-border bg-card/50 flex flex-col h-full overflow-hidden">
                <div className="p-3 border-b border-border/80 bg-card/80 flex items-center justify-between text-xs text-muted-foreground">
                  <span className="font-semibold uppercase tracking-wider text-[11px]">
                    Inspection Tools ({filteredTools.length})
                  </span>
                  <span className="text-[11px] font-mono">Select to inspect</span>
                </div>

                <div className="flex-1 overflow-y-auto divide-y divide-border/40 p-2 space-y-1">
                  {filteredTools.map(({ result, summary }) => {
                    const isSelected = focusedTool?.result.id === result.id;
                    return (
                      <button
                        key={result.id}
                        onClick={() => setSelectedToolName(result.toolName)}
                        className={`w-full text-left p-3 rounded-xl transition-all flex items-start justify-between gap-2.5 ${
                          isSelected
                            ? 'bg-primary/15 border border-primary/50 text-foreground shadow-sm'
                            : 'hover:bg-muted/60 text-muted-foreground hover:text-foreground border border-transparent'
                        }`}
                      >
                        <div className="flex items-start gap-2.5 min-w-0">
                          <span
                            className={`p-1.5 rounded-lg mt-0.5 flex-shrink-0 ${
                              isSelected ? 'bg-primary text-primary-foreground' : 'bg-muted text-muted-foreground'
                            }`}
                          >
                            {getToolIcon(result.toolName, 'w-3.5 h-3.5')}
                          </span>
                          <div className="min-w-0">
                            <p className="text-xs font-semibold truncate text-foreground">
                              {result.toolName}
                            </p>
                            <div className="flex items-center gap-1.5 mt-1">
                              <span
                                className={`text-[10px] px-1.5 py-0.5 rounded border font-mono ${getBadgeClasses(
                                  summary.variant
                                )}`}
                              >
                                {summary.badge}
                              </span>
                            </div>
                          </div>
                        </div>

                        <FiChevronRight
                          className={`w-4 h-4 flex-shrink-0 mt-1 transition-transform ${
                            isSelected ? 'text-primary translate-x-0.5' : 'text-muted-foreground/40'
                          }`}
                        />
                      </button>
                    );
                  })}
                </div>
              </aside>

              {/* Right Detail Pane */}
              <section className="flex-1 overflow-y-auto p-6 space-y-6 bg-background">
                {focusedTool ? (
                  <div className="space-y-6 max-w-5xl mx-auto animate-in fade-in duration-150">
                    {/* Tool Focused Header Strip */}
                    <div className="flex items-center justify-between pb-4 border-b border-border">
                      <div className="flex items-center gap-3">
                        <span className="p-2.5 rounded-xl bg-primary/10 text-primary border border-primary/20">
                          {getToolIcon(focusedTool.result.toolName, 'w-5 h-5')}
                        </span>
                        <div>
                          <h2 className="text-lg font-bold text-foreground">
                            {focusedTool.formatted.title || focusedTool.result.toolName}
                          </h2>
                          <div className="flex items-center gap-2 mt-1">
                            <span
                              className={`text-xs px-2.5 py-0.5 rounded-full border font-mono ${getBadgeClasses(
                                focusedTool.summary.variant
                              )}`}
                            >
                              {focusedTool.summary.badge}
                            </span>
                            <span
                              className={`text-xs px-2 py-0.5 rounded font-mono ${
                                focusedTool.result.exitCode === 0
                                  ? 'bg-emerald-950/80 text-emerald-400 border border-emerald-800/60'
                                  : 'bg-rose-950/80 text-rose-400 border border-rose-800/60'
                              }`}
                            >
                              Exit Code: {focusedTool.result.exitCode}
                            </span>
                          </div>
                        </div>
                      </div>

                      {/* Tool Carousel Navigation */}
                      <div className="flex items-center gap-1.5">
                        <button
                          onClick={() => navigateTool('prev')}
                          className="px-2.5 py-1.5 rounded-lg bg-card hover:bg-muted border border-border text-xs text-muted-foreground hover:text-foreground flex items-center gap-1 transition-colors"
                          title="Previous Tool"
                        >
                          <FiChevronLeft className="w-3.5 h-3.5" /> Previous
                        </button>
                        <button
                          onClick={() => navigateTool('next')}
                          className="px-2.5 py-1.5 rounded-lg bg-card hover:bg-muted border border-border text-xs text-muted-foreground hover:text-foreground flex items-center gap-1 transition-colors"
                          title="Next Tool"
                        >
                          Next <FiChevronRight className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </div>

                    {/* Output Sections */}
                    <div className="space-y-5">
                      {focusedTool.result.toolName.toLowerCase().includes('string') && (
                        <div className="p-4 bg-primary/10 border border-primary/30 rounded-xl flex flex-wrap items-center justify-between gap-3 shadow-sm">
                          <div className="flex items-center gap-3">
                            <span className="p-2 rounded-lg bg-primary/20 text-primary">
                              <FiFileText className="w-5 h-5" />
                            </span>
                            <div>
                              <p className="text-sm font-bold text-foreground">Full Strings Explorer Available</p>
                              <p className="text-xs text-muted-foreground">Search through all strings, filter by IOC category / encoding, and download as .txt</p>
                            </div>
                          </div>
                          <button
                            onClick={() => setViewMode('strings')}
                            className="px-4 py-2 bg-primary text-primary-foreground text-xs font-semibold rounded-lg hover:bg-primary/90 transition-colors shadow-sm flex items-center gap-1.5"
                          >
                            <FiFileText className="w-3.5 h-3.5" />
                            <span>Open Strings Explorer →</span>
                          </button>
                        </div>
                      )}
                      {(focusedTool.formatted.sections || []).map((section: OutputSection, sIdx: number) => (
                        <OutputSectionView key={sIdx} section={section} />
                      ))}
                    </div>
                  </div>
                ) : (
                  <div className="text-center py-24 text-muted-foreground">
                    <p>Select a tool from the left directory to inspect.</p>
                  </div>
                )}
              </section>
            </div>
          ) : (
            /* ==================== 3. Dashboard Grid View ("All at Once") ==================== */
            <div className="flex-1 overflow-y-auto p-5 sm:p-6 bg-background">
              <div className="grid grid-cols-1 md:grid-cols-2 2xl:grid-cols-3 gap-5">
                {filteredTools.map(({ result, formatted, summary }) => (
                  <article
                    key={result.id}
                    className="flex flex-col bg-card/85 border border-border hover:border-primary/50 transition-all rounded-xl shadow-sm overflow-hidden"
                  >
                    {/* Card Header */}
                    <header className="flex items-start justify-between p-4 pb-3 border-b border-border/80 bg-card">
                      <div className="flex items-start gap-2.5 min-w-0">
                        <span className="p-2 rounded-lg bg-primary/10 text-primary border border-primary/20 flex-shrink-0 mt-0.5">
                          {getToolIcon(result.toolName, 'w-4 h-4')}
                        </span>
                        <div className="min-w-0">
                          <h3 className="text-sm font-bold text-foreground truncate" title={result.toolName}>
                            {formatted.title || result.toolName}
                          </h3>
                          <div className="flex items-center gap-1.5 mt-1 flex-wrap">
                            <span
                              className={`text-[11px] px-2 py-0.5 rounded-md border font-mono ${getBadgeClasses(
                                summary.variant
                              )}`}
                            >
                              {summary.badge}
                            </span>
                          </div>
                        </div>
                      </div>

                      {/* Card Action: Zoom into Split View */}
                      <button
                        onClick={() => {
                          setSelectedToolName(result.toolName);
                          setViewMode('split');
                        }}
                        className="p-1.5 text-muted-foreground hover:text-foreground hover:bg-muted/80 rounded-lg transition-colors border border-transparent hover:border-border"
                        title="Focus and inspect this tool individually"
                      >
                        <FiZoomIn className="w-4 h-4" />
                      </button>
                    </header>

                    {/* Card Body with formatted sections */}
                    <div className="p-4 space-y-4 flex-1 overflow-y-auto max-h-[460px]">
                      {result.toolName.toLowerCase().includes('string') && (
                        <button
                          onClick={() => setViewMode('strings')}
                          className="w-full py-2 px-3 rounded-lg bg-primary/10 hover:bg-primary/20 text-primary border border-primary/30 text-xs font-semibold flex items-center justify-center gap-2 transition-colors shadow-sm"
                        >
                          <FiFileText className="w-3.5 h-3.5" />
                          <span>Open Full Strings Explorer (Search & Export) →</span>
                        </button>
                      )}
                      {(formatted.sections || []).map((section: OutputSection, sIdx: number) => (
                        <OutputSectionView key={sIdx} section={section} />
                      ))}
                    </div>
                  </article>
                ))}
              </div>
            </div>
          )}
        </main>
      </div>
    </div>
  );
}
