import { useState, useEffect } from 'react';
import type { ExtractedStringItem } from '../../api/types';
import { getStrings, getStringsDownloadUrl } from '../../api/analysis';
import {
  FiSearch,
  FiDownload,
  FiCopy,
  FiCheck,
  FiChevronLeft,
  FiChevronRight,
  FiRefreshCw,
  FiTerminal,
  FiAlertTriangle,
  FiFilter,
} from 'react-icons/fi';

interface StringsExplorerProps {
  analysisId: string;
  fileName: string;
}

export default function StringsExplorer({ analysisId, fileName }: StringsExplorerProps) {
  const [items, setItems] = useState<ExtractedStringItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(100);
  const [minLength, setMinLength] = useState(4);
  const [encoding, setEncoding] = useState('all');
  const [category, setCategory] = useState('all');
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [copiedKey, setCopiedKey] = useState<string | null>(null);
  const [copiedPage, setCopiedPage] = useState(false);

  const [refreshKey, setRefreshKey] = useState(0);

  // Debounce search input
  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1); // Reset to page 1 on new search
    }, 250);
    return () => clearTimeout(handler);
  }, [search]);

  useEffect(() => {
    let isMounted = true;

    getStrings(analysisId, {
      page,
      pageSize,
      minLength,
      search: debouncedSearch,
      encoding,
      category,
    })
      .then((result) => {
        if (isMounted) {
          setItems(result.items || []);
          setTotalCount(result.totalCount || 0);
          setTotalPages(result.totalPages || 1);
          setLoading(false);
        }
      })
      .catch((err: unknown) => {
        if (isMounted) {
          setError(err instanceof Error ? err.message : 'Failed to fetch strings');
          setLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, [analysisId, page, pageSize, minLength, debouncedSearch, encoding, category, refreshKey]);

  const handleCopy = (key: string, text: string) => {
    navigator.clipboard.writeText(text);
    setCopiedKey(key);
    setTimeout(() => setCopiedKey(null), 1800);
  };

  const handleCopyPage = () => {
    if (items.length === 0) return;
    const allText = items.map((i) => i.value).join('\n');
    navigator.clipboard.writeText(allText);
    setCopiedPage(true);
    setTimeout(() => setCopiedPage(false), 2000);
  };

  const categoryFilters = [
    { id: 'all', label: 'All Strings' },
    { id: 'ioc', label: 'Threat IOCs' },
    { id: 'url', label: 'URLs / Web' },
    { id: 'ip', label: 'IP Addresses' },
    { id: 'command', label: 'Commands / Shell' },
    { id: 'injection', label: 'Injection APIs' },
    { id: 'evasion', label: 'Defense Evasion' },
    { id: 'registry', label: 'Registry / Run' },
  ];

  return (
    <div className="flex flex-col h-full bg-background overflow-hidden">
      {/* Top Controls & Filter Bar */}
      <div className="p-4 bg-card/90 border-b border-border space-y-3 flex-shrink-0">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-2.5">
            <span className="p-2 rounded-lg bg-primary/10 text-primary">
              <FiTerminal className="w-5 h-5" />
            </span>
            <div>
              <h2 className="text-base font-bold text-foreground flex items-center gap-2">
                Binary Strings Explorer
                <span className="text-xs px-2 py-0.5 rounded-full font-mono font-semibold bg-primary/10 text-primary border border-primary/20">
                  {totalCount.toLocaleString()} {totalCount === 1 ? 'String' : 'Strings'}
                </span>
              </h2>
              <p className="text-xs text-muted-foreground mt-0.5">
                Extracted printable ASCII and UTF-16 strings with byte offsets and automated IOC classification
              </p>
            </div>
          </div>

          {/* Quick Actions: Copy Page & Download Full .txt */}
          <div className="flex items-center gap-2">
            <button
              onClick={handleCopyPage}
              disabled={items.length === 0 || loading}
              className="px-3 py-1.5 rounded-lg bg-muted hover:bg-muted/80 text-foreground border border-border text-xs font-medium flex items-center gap-1.5 transition-colors disabled:opacity-50"
              title="Copy all strings visible on current page to clipboard"
            >
              {copiedPage ? (
                <>
                  <FiCheck className="w-3.5 h-3.5 text-emerald-400" />
                  <span className="text-emerald-400">Page Copied</span>
                </>
              ) : (
                <>
                  <FiCopy className="w-3.5 h-3.5" />
                  <span>Copy Page</span>
                </>
              )}
            </button>

            <a
              href={getStringsDownloadUrl(analysisId, minLength, encoding)}
              download={`${fileName.replace(/\.[^/.]+$/, '')}_strings.txt`}
              className="px-3.5 py-1.5 rounded-lg bg-primary hover:bg-primary/90 text-primary-foreground text-xs font-semibold flex items-center gap-1.5 transition-colors shadow-sm"
              title="Download all extracted strings as a plain text file"
            >
              <FiDownload className="w-3.5 h-3.5" />
              <span>Export .txt</span>
            </a>
          </div>
        </div>

        {/* Search, Encodings & Min-Length Row */}
        <div className="flex flex-wrap items-center justify-between gap-3 pt-1">
          {/* Search Input */}
          <div className="relative flex-1 min-w-[240px] max-w-md">
            <FiSearch className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search strings or byte offset (e.g. 0x00001000)..."
              className="w-full pl-9 pr-8 py-1.5 bg-muted/50 border border-border rounded-lg text-xs text-foreground placeholder:text-muted-foreground/70 focus:outline-none focus:border-primary transition-colors"
            />
            {search && (
              <button
                onClick={() => setSearch('')}
                className="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground text-xs"
              >
                ✕
              </button>
            )}
          </div>

          {/* Filters: Encoding, Min Length, Refresh */}
          <div className="flex flex-wrap items-center gap-2 text-xs">
            {/* Encoding Selector */}
            <div className="flex items-center gap-1.5 bg-muted/40 border border-border rounded-lg px-2 py-1">
              <span className="text-muted-foreground text-[11px] font-medium">Encoding:</span>
              <select
                value={encoding}
                onChange={(e) => {
                  setEncoding(e.target.value);
                  setPage(1);
                }}
                className="bg-transparent text-foreground focus:outline-none text-xs cursor-pointer"
              >
                <option value="all" className="bg-card">All Encodings</option>
                <option value="ascii" className="bg-card">ASCII</option>
                <option value="utf-16" className="bg-card">UTF-16 (Unicode)</option>
              </select>
            </div>

            {/* Min Length Selector */}
            <div className="flex items-center gap-1.5 bg-muted/40 border border-border rounded-lg px-2 py-1">
              <span className="text-muted-foreground text-[11px] font-medium">Min Length:</span>
              <select
                value={minLength}
                onChange={(e) => {
                  setMinLength(parseInt(e.target.value, 10));
                  setPage(1);
                }}
                className="bg-transparent text-foreground focus:outline-none text-xs cursor-pointer font-mono"
              >
                <option value="4" className="bg-card">4+ chars</option>
                <option value="6" className="bg-card">6+ chars</option>
                <option value="8" className="bg-card">8+ chars</option>
                <option value="12" className="bg-card">12+ chars</option>
                <option value="16" className="bg-card">16+ chars</option>
              </select>
            </div>

            {/* Refresh */}
            <button
              onClick={() => setRefreshKey((k) => k + 1)}
              disabled={loading}
              className="p-1.5 text-muted-foreground hover:text-foreground hover:bg-muted rounded-lg border border-border transition-colors"
              title="Refresh strings"
            >
              <FiRefreshCw className={`w-3.5 h-3.5 ${loading ? 'animate-spin' : ''}`} />
            </button>
          </div>
        </div>

        {/* Category Pills Strip */}
        <div className="flex items-center gap-1.5 overflow-x-auto pt-1 pb-0.5 no-scrollbar">
          <FiFilter className="w-3.5 h-3.5 text-muted-foreground/80 flex-shrink-0 mr-1" />
          {categoryFilters.map((tab) => (
            <button
              key={tab.id}
              onClick={() => {
                setCategory(tab.id);
                setPage(1);
              }}
              className={`px-2.5 py-1 rounded-md text-xs font-medium whitespace-nowrap transition-all border ${
                category === tab.id
                  ? 'bg-primary text-primary-foreground border-primary shadow-sm font-semibold'
                  : 'bg-card text-muted-foreground hover:text-foreground border-border/80 hover:bg-muted/50'
              }`}
            >
              {tab.label}
            </button>
          ))}
        </div>
      </div>

      {/* Strings Table Content Area */}
      <div className="flex-1 overflow-y-auto bg-background p-4">
        {loading && items.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-28 text-muted-foreground">
            <div className="w-8 h-8 border-3 border-primary border-t-transparent rounded-full animate-spin mb-3" />
            <p className="text-sm font-medium">Extracting and classifying binary strings...</p>
          </div>
        ) : error ? (
          <div className="p-4 bg-destructive/10 border border-destructive/30 rounded-xl text-sm text-destructive flex items-center gap-3">
            <FiAlertTriangle className="w-5 h-5 flex-shrink-0" />
            <span>{error}</span>
          </div>
        ) : items.length === 0 ? (
          <div className="text-center py-28 text-muted-foreground">
            <FiSearch className="w-10 h-10 mx-auto mb-2 opacity-30" />
            <p className="text-sm font-semibold text-foreground">No strings matched your current filters.</p>
            <p className="text-xs text-muted-foreground mt-1">
              Try adjusting the minimum length, clearing your search query, or switching category filter.
            </p>
            {(search || category !== 'all' || encoding !== 'all' || minLength > 4) && (
              <button
                onClick={() => {
                  setSearch('');
                  setCategory('all');
                  setEncoding('all');
                  setMinLength(4);
                  setPage(1);
                }}
                className="mt-3 px-3 py-1.5 rounded-lg bg-primary text-primary-foreground text-xs font-medium shadow-sm hover:bg-primary/90 transition-colors"
              >
                Reset All Filters
              </button>
            )}
          </div>
        ) : (
          <div className="rounded-xl border border-border bg-card/60 shadow-sm overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="bg-card text-muted-foreground uppercase tracking-wider border-b border-border font-semibold text-[11px] sticky top-0 z-10 shadow-sm">
                  <tr>
                    <th className="px-3.5 py-3 w-16 text-center">#</th>
                    <th className="px-3.5 py-3 w-32 font-mono">Offset</th>
                    <th className="px-3.5 py-3 w-20 text-center">Encoding</th>
                    <th className="px-3.5 py-3 w-36">Category</th>
                    <th className="px-3.5 py-3">String Value</th>
                    <th className="px-3.5 py-3 w-16 text-center">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border/40 font-mono text-xs">
                  {items.map((item, idx) => {
                    const rowNum = (page - 1) * pageSize + idx + 1;
                    const isCopied = copiedKey === `str-${item.offset}-${idx}`;
                    const isIoc = Boolean(item.category && item.category.trim().length > 0);

                    return (
                      <tr
                        key={`${item.offset}-${idx}`}
                        className={`hover:bg-primary/5 transition-colors group ${
                          isIoc ? 'bg-rose-950/15' : 'even:bg-muted/15'
                        }`}
                      >
                        {/* Row Index */}
                        <td className="px-3.5 py-2.5 text-center text-muted-foreground/60 text-[11px]">
                          {rowNum}
                        </td>

                        {/* Byte Offset */}
                        <td className="px-3.5 py-2.5 font-semibold text-accent-foreground whitespace-nowrap">
                          {item.offsetHex}
                        </td>

                        {/* Encoding Pill */}
                        <td className="px-3.5 py-2.5 text-center whitespace-nowrap">
                          <span
                            className={`px-1.5 py-0.5 rounded text-[10px] font-semibold border ${
                              item.encoding === 'UTF-16'
                                ? 'bg-purple-950/80 text-purple-300 border-purple-800'
                                : 'bg-cyan-950/80 text-cyan-300 border-cyan-800'
                            }`}
                          >
                            {item.encoding}
                          </span>
                        </td>

                        {/* Category / Threat Badge */}
                        <td className="px-3.5 py-2.5 whitespace-nowrap">
                          {item.category ? (
                            <span
                              className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-[10px] font-semibold border ${
                                item.severity === 'High'
                                  ? 'bg-rose-950/80 text-rose-300 border-rose-800'
                                  : 'bg-amber-950/80 text-amber-300 border-amber-800'
                              }`}
                            >
                              <FiAlertTriangle className="w-3 h-3 flex-shrink-0" />
                              {item.category}
                            </span>
                          ) : (
                            <span className="text-muted-foreground/60 text-[11px]">Plain</span>
                          )}
                        </td>

                        {/* String Value */}
                        <td className="px-3.5 py-2.5 text-foreground/95 break-all max-w-xl font-mono text-[12px] leading-relaxed">
                          {item.value}
                        </td>

                        {/* Copy Single Action */}
                        <td className="px-3.5 py-2.5 text-center whitespace-nowrap">
                          <button
                            onClick={() => handleCopy(`str-${item.offset}-${idx}`, item.value)}
                            className="p-1.5 text-muted-foreground hover:text-foreground hover:bg-muted rounded-md transition-colors opacity-70 group-hover:opacity-100"
                            title="Copy string to clipboard"
                          >
                            {isCopied ? (
                              <FiCheck className="w-3.5 h-3.5 text-emerald-400" />
                            ) : (
                              <FiCopy className="w-3.5 h-3.5" />
                            )}
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>

      {/* Pagination Footer */}
      <div className="px-5 py-3 border-t border-border bg-card/90 flex flex-wrap items-center justify-between gap-3 text-xs flex-shrink-0">
        <div className="text-muted-foreground">
          Showing <span className="font-semibold text-foreground font-mono">{totalCount > 0 ? (page - 1) * pageSize + 1 : 0}</span> to{' '}
          <span className="font-semibold text-foreground font-mono">{Math.min(page * pageSize, totalCount)}</span> of{' '}
          <span className="font-semibold text-foreground font-mono">{totalCount.toLocaleString()}</span> strings
        </div>

        <div className="flex items-center gap-3">
          {/* Rows per page selector */}
          <div className="flex items-center gap-1.5 text-muted-foreground">
            <span>Rows:</span>
            <select
              value={pageSize}
              onChange={(e) => {
                setPageSize(parseInt(e.target.value, 10));
                setPage(1);
              }}
              className="bg-muted border border-border text-foreground rounded px-1.5 py-0.5 text-xs focus:outline-none cursor-pointer"
            >
              <option value="50">50</option>
              <option value="100">100</option>
              <option value="250">250</option>
              <option value="500">500</option>
            </select>
          </div>

          {/* Navigation Controls */}
          <div className="flex items-center gap-1">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1 || loading}
              className="px-2.5 py-1 rounded bg-muted hover:bg-muted/80 text-foreground border border-border disabled:opacity-40 disabled:cursor-not-allowed flex items-center gap-1 transition-colors"
            >
              <FiChevronLeft className="w-3.5 h-3.5" /> Prev
            </button>

            <span className="px-2 font-mono text-muted-foreground">
              {page} / {totalPages || 1}
            </span>

            <button
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages || loading}
              className="px-2.5 py-1 rounded bg-muted hover:bg-muted/80 text-foreground border border-border disabled:opacity-40 disabled:cursor-not-allowed flex items-center gap-1 transition-colors"
            >
              Next <FiChevronRight className="w-3.5 h-3.5" />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
