import { useState } from 'react';
import type { OutputSection } from '../../api/types';
import { FiCopy, FiCheck, FiAlertTriangle, FiCheckCircle, FiInfo } from 'react-icons/fi';

interface Props {
  section: OutputSection;
}

export default function OutputSectionView({ section }: Props) {
  const [copiedKey, setCopiedKey] = useState<string | null>(null);

  // Normalize casing for both camelCase and PascalCase backend inputs
  const rawType = section.type || section.Type || '';
  const type = rawType.toLowerCase();
  const title = section.title || section.Title || '';
  const rawFields = section.fields || section.Fields || [];
  const fields = rawFields.map((f) => ({
    label: f.label || f.Label || '',
    value: f.value || f.Value || '',
    valueType: f.valueType || f.ValueType,
  }));
  const columns = section.columns || section.Columns || [];
  const rows = section.rows || section.Rows || [];
  const text = section.text || section.Text || '';

  const handleCopy = (key: string, value: string) => {
    navigator.clipboard.writeText(value);
    setCopiedKey(key);
    setTimeout(() => setCopiedKey(null), 1800);
  };

  const renderEntropyBadge = (entropyVal: number | string) => {
    const num = typeof entropyVal === 'number' ? entropyVal : parseFloat(String(entropyVal));
    if (isNaN(num)) return <span>{entropyVal}</span>;

    const percent = Math.min(100, Math.max(0, (num / 8.0) * 100));
    let colorClass = 'bg-emerald-500';
    let textClass = 'text-emerald-400';
    let label = 'Low';

    if (num >= 7.2) {
      colorClass = 'bg-rose-500';
      textClass = 'text-rose-400';
      label = 'High / Packed';
    } else if (num >= 6.0) {
      colorClass = 'bg-amber-500';
      textClass = 'text-amber-400';
      label = 'Moderate';
    }

    return (
      <div className="flex flex-col gap-1 min-w-[130px]">
        <div className="flex items-center justify-between text-xs font-mono">
          <span className={`font-semibold ${textClass}`}>{num.toFixed(4)}</span>
          <span className="text-[10px] text-muted-foreground">{label}</span>
        </div>
        <div className="w-full h-1.5 bg-muted rounded-full overflow-hidden border border-border/40">
          <div
            className={`h-full ${colorClass} transition-all duration-300 rounded-full`}
            style={{ width: `${percent}%` }}
          />
        </div>
      </div>
    );
  };

  const renderTableCell = (column: string, value: string) => {
    const colLower = column.toLowerCase();
    const valLower = value.toLowerCase();

    // Entropy Column
    if (colLower.includes('entropy')) {
      const num = parseFloat(value);
      if (!isNaN(num)) {
        return renderEntropyBadge(num);
      }
    }

    // Permission Column (RWX)
    if (colLower.includes('perm')) {
      const isRWX = value.includes('X') && value.includes('W');
      return (
        <span
          className={`inline-flex px-2 py-0.5 rounded text-[11px] font-mono font-bold tracking-wider ${
            isRWX
              ? 'bg-rose-950 text-rose-400 border border-rose-800 animate-pulse'
              : 'bg-accent text-accent-foreground border border-border/80'
          }`}
        >
          {value}
        </span>
      );
    }

    // Status / Severity Column
    if (colLower.includes('status') || colLower.includes('severity')) {
      if (valLower.includes('suspicious') || valLower.includes('high') || valLower.includes('packed')) {
        return (
          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-rose-950/80 text-rose-400 border border-rose-800">
            <FiAlertTriangle className="w-3 h-3" /> {value}
          </span>
        );
      }
      if (valLower.includes('medium') || valLower.includes('moderate')) {
        return (
          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-amber-950/80 text-amber-400 border border-amber-800">
            <FiInfo className="w-3 h-3" /> {value}
          </span>
        );
      }
      if (valLower.includes('normal') || valLower.includes('low') || valLower.includes('not packed')) {
        return (
          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-950/80 text-emerald-400 border border-emerald-800">
            <FiCheckCircle className="w-3 h-3" /> {value}
          </span>
        );
      }
    }

    return <span className="text-foreground/90">{value}</span>;
  };

  switch (type) {
    case 'keyvalue':
      return (
        <section className="bg-muted/30 border border-border/90 rounded-xl p-4 shadow-sm">
          <h4 className="text-xs font-bold uppercase tracking-wider text-muted-foreground mb-3 flex items-center justify-between">
            <span>{title}</span>
            <span className="text-[10px] text-muted-foreground/60 font-mono">
              {fields.length} {fields.length === 1 ? 'Property' : 'Properties'}
            </span>
          </h4>
          <dl className="grid grid-cols-1 md:grid-cols-2 gap-3">
            {fields.map((field) => {
              const isEntropy = field.label.toLowerCase().includes('entropy') && !isNaN(parseFloat(field.value));
              const isCopyable = field.value.length > 10;
              const isCopied = copiedKey === field.label;

              return (
                <div
                  key={field.label}
                  className="bg-card/80 border border-border/70 hover:border-border transition-colors rounded-lg p-3 flex flex-col justify-between gap-1.5"
                >
                  <dt className="text-xs text-muted-foreground font-medium flex items-center justify-between">
                    <span>{field.label}</span>
                    {isCopyable && (
                      <button
                        onClick={() => handleCopy(field.label, field.value)}
                        className="text-muted-foreground/60 hover:text-foreground text-[11px] flex items-center gap-1 transition-colors"
                        title="Copy value"
                      >
                        {isCopied ? (
                          <span className="text-emerald-400 flex items-center gap-0.5">
                            <FiCheck className="w-3 h-3" /> Copied
                          </span>
                        ) : (
                          <FiCopy className="w-3 h-3" />
                        )}
                      </button>
                    )}
                  </dt>
                  <dd className="text-sm font-semibold text-foreground mt-0.5 truncate">
                    {isEntropy ? renderEntropyBadge(field.value) : field.value}
                  </dd>
                </div>
              );
            })}
          </dl>
        </section>
      );

    case 'table':
      return (
        <section className="bg-muted/30 border border-border/90 rounded-xl p-4 shadow-sm">
          <h4 className="text-xs font-bold uppercase tracking-wider text-muted-foreground mb-3 flex items-center justify-between">
            <span>{title}</span>
            <span className="text-[10px] text-muted-foreground/60 font-mono">
              {rows.length} {rows.length === 1 ? 'Entry' : 'Entries'}
            </span>
          </h4>
          <div className={`overflow-x-auto rounded-lg border border-border/80 ${rows.length > 6 ? 'max-h-72 overflow-y-auto' : ''}`}>
            <table className="w-full text-left text-xs">
              <thead className="bg-card text-muted-foreground uppercase tracking-wider border-b border-border font-medium text-[11px]">
                <tr>
                  {columns.map((column) => (
                    <th key={column} className="px-3.5 py-3 whitespace-nowrap">
                      {column}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-border/50 font-mono">
                {rows.length === 0 ? (
                  <tr>
                    <td
                      colSpan={columns.length || 1}
                      className="px-4 py-6 text-center text-muted-foreground italic text-xs"
                    >
                      No records available.
                    </td>
                  </tr>
                ) : (
                  rows.map((row, index) => (
                    <tr
                      key={index}
                      className="hover:bg-card/70 transition-colors even:bg-muted/15"
                    >
                      {row.map((value, cellIndex) => (
                        <td
                          key={cellIndex}
                          className="px-3.5 py-2.5 whitespace-nowrap"
                        >
                          {renderTableCell(columns[cellIndex] || '', value)}
                        </td>
                      ))}
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </section>
      );

    case 'tags':
      return (
        <section className="bg-muted/30 border border-border/90 rounded-xl p-4 shadow-sm">
          <h4 className="text-xs font-bold uppercase tracking-wider text-muted-foreground mb-3">
            {title}
          </h4>
          <div className="flex flex-wrap gap-2">
            {fields.map((field) => {
              const isAlert = field.value.toLowerCase().includes('risk') ||
                              field.value.toLowerCase().includes('suspicious') ||
                              field.value.toLowerCase().includes('rwx');
              return (
                <span
                  key={field.label + field.value}
                  className={`text-xs px-3 py-1 rounded-lg border font-mono font-medium flex items-center gap-1.5 ${
                    isAlert
                      ? 'bg-rose-950/60 text-rose-300 border-rose-800'
                      : 'bg-card text-accent-foreground border-border/90'
                  }`}
                >
                  <span className="w-1.5 h-1.5 rounded-full bg-primary" />
                  {field.value}
                </span>
              );
            })}
          </div>
        </section>
      );

    case 'text':
    case 'json':
      return (
        <section className="bg-muted/30 border border-border/90 rounded-xl p-4 shadow-sm">
          <div className="flex items-center justify-between mb-2">
            <h4 className="text-xs font-bold uppercase tracking-wider text-muted-foreground">
              {title}
            </h4>
            {text && (
              <button
                onClick={() => handleCopy('text-content', text || '')}
                className="text-xs text-muted-foreground hover:text-foreground flex items-center gap-1"
              >
                {copiedKey === 'text-content' ? (
                  <span className="text-emerald-400 flex items-center gap-0.5">
                    <FiCheck className="w-3.5 h-3.5" /> Copied
                  </span>
                ) : (
                  <>
                    <FiCopy className="w-3.5 h-3.5" /> Copy Content
                  </>
                )}
              </button>
            )}
          </div>
          <pre className="bg-[#050608] text-foreground/90 p-3.5 rounded-lg border border-border text-xs font-mono whitespace-pre-wrap break-all max-h-72 overflow-y-auto leading-relaxed">
            {text || 'No text recorded.'}
          </pre>
        </section>
      );

    default:
      return (
        <section className="bg-muted/30 border border-border/90 rounded-xl p-4 shadow-sm">
          <h4 className="text-xs font-bold uppercase tracking-wider text-muted-foreground mb-2">
            {title || 'Output'}
          </h4>
          <pre className="bg-[#050608] text-foreground/90 p-3.5 rounded-lg border border-border text-xs font-mono whitespace-pre-wrap break-all max-h-72 overflow-y-auto leading-relaxed">
            {text || JSON.stringify(section, null, 2)}
          </pre>
        </section>
      );
  }
}
