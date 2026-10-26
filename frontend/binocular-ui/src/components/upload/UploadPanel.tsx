import { useState, useRef } from 'react';
import { uploadFile } from '../../api/analysis';
import type { AnalysisResponse } from '../../api/types';
import {
  FiUploadCloud,
  FiCheckCircle,
  FiAlertTriangle,
  FiShield,
  FiActivity,
  FiTerminal,
  FiLayers,
  FiShare2,
  FiLock,
  FiKey,
  FiBox,
  FiClock,
  FiGlobe,
  FiTarget,
} from 'react-icons/fi';

interface UploadPanelProps {
  onUploadSuccess?: (response: AnalysisResponse) => void;
  className?: string;
}

const SUPPORTED_EXTS = ['.exe', '.dll', '.bin', '.elf', '.so', '.dylib', '.hex', '.img', '.iso'];

export default function UploadPanel({ onUploadSuccess, className }: UploadPanelProps) {
  const [isDragging, setIsDragging] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState<string | null>(null);
  const [lastUploaded, setLastUploaded] = useState<AnalysisResponse | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleProcessFile = async (file: File) => {
    if (!file) return;

    if (file.size === 0) {
      setErrorMessage('The selected file is empty (0 bytes). Please select a valid binary.');
      if (fileInputRef.current) fileInputRef.current.value = '';
      return;
    }

    const MAX_FILE_SIZE = 100 * 1024 * 1024; // 100 MB
    if (file.size > MAX_FILE_SIZE) {
      setErrorMessage(
        `File exceeds the 100 MB limit (selected: ${(file.size / (1024 * 1024)).toFixed(1)} MB). Please select a file under 100 MB.`
      );
      if (fileInputRef.current) fileInputRef.current.value = '';
      return;
    }

    setIsUploading(true);
    setErrorMessage(null);
    setLastUploaded(null);
    setUploadProgress(`Uploading ${file.name} (${(file.size / (1024 * 1024)).toFixed(2)} MB)...`);

    try {
      const response = await uploadFile(file);
      setLastUploaded(response);
      setUploadProgress(null);
      setIsUploading(false);
      onUploadSuccess?.(response);
    } catch (err: unknown) {
      setErrorMessage(err instanceof Error ? err.message : 'Upload failed');
      setIsUploading(false);
      setUploadProgress(null);
    } finally {
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    }
  };

  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      handleProcessFile(file);
    }
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    const file = e.dataTransfer.files?.[0];
    if (file) {
      handleProcessFile(file);
    }
  };

  return (
    <div className={`flex flex-col gap-4 ${className || ''}`}>
      {/* Upload Zone Card */}
      <div className="bg-card text-card-foreground border border-border rounded-xl p-5 shadow-sm flex flex-col gap-4">
        <div>
          <h2 className="text-xl font-bold tracking-tight text-foreground flex items-center gap-2">
            <FiUploadCloud className="w-5 h-5 text-primary" />
            Upload Binary
          </h2>
          <p className="text-xs text-muted-foreground mt-1">
            Submit a binary or executable image for static inspection and packer detection.
          </p>
        </div>

        {/* Drag & Drop Area */}
        <div
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          onClick={() => !isUploading && fileInputRef.current?.click()}
          className={`relative border-2 border-dashed rounded-xl p-8 flex flex-col items-center justify-center text-center cursor-pointer transition-all ${
            isDragging
              ? 'border-primary bg-primary/10 scale-[1.01]'
              : isUploading
              ? 'border-border/60 bg-muted/20 cursor-wait'
              : 'border-border hover:border-primary/60 hover:bg-muted/40'
          }`}
        >
          <input
            ref={fileInputRef}
            id="file-upload"
            type="file"
            accept={SUPPORTED_EXTS.join(',')}
            className="hidden"
            onChange={handleFileChange}
            disabled={isUploading}
          />

          {isUploading ? (
            <div className="flex flex-col items-center gap-3">
              <div className="w-10 h-10 border-3 border-primary border-t-transparent rounded-full animate-spin" />
              <div className="text-sm font-medium text-foreground">{uploadProgress}</div>
              <p className="text-xs text-muted-foreground animate-pulse">
                Hashing SHA-256 and queuing automated analysis...
              </p>
            </div>
          ) : (
            <>
              <div className="p-3.5 bg-primary/10 text-primary rounded-full mb-3">
                <FiUploadCloud className="w-7 h-7" />
              </div>
              <p className="text-sm font-semibold text-foreground">
                Click to browse or drag & drop binary
              </p>
              <p className="text-xs text-muted-foreground mt-1">
                Max file size 100 MB
              </p>

              {/* Supported Extensions tags */}
              <div className="flex flex-wrap justify-center gap-1.5 mt-4 max-w-xs">
                {SUPPORTED_EXTS.map((ext) => (
                  <span
                    key={ext}
                    className="text-[11px] font-mono px-2 py-0.5 rounded bg-muted text-muted-foreground border border-border/60"
                  >
                    {ext}
                  </span>
                ))}
              </div>
            </>
          )}
        </div>

        {/* Status messages: Success */}
        {lastUploaded && (
          <div className="p-3.5 bg-emerald-950/40 border border-emerald-800/60 rounded-lg text-xs text-emerald-300 flex items-start gap-2.5 animate-in fade-in">
            <FiCheckCircle className="w-4 h-4 text-emerald-400 flex-shrink-0 mt-0.5" />
            <div className="min-w-0">
              <p className="font-semibold text-emerald-400">File Enqueued Successfully</p>
              <p className="mt-0.5 truncate text-foreground/90">
                <span className="font-medium">{lastUploaded.fileName}</span> is now{' '}
                <span className="font-mono text-emerald-400">{lastUploaded.status}</span>.
              </p>
            </div>
          </div>
        )}

        {/* Status messages: Error */}
        {errorMessage && (
          <div className="p-3.5 bg-destructive/10 border border-destructive/30 rounded-lg text-xs text-rose-300 flex items-start gap-2.5 animate-in fade-in">
            <FiAlertTriangle className="w-4 h-4 text-destructive flex-shrink-0 mt-0.5" />
            <div>
              <p className="font-semibold text-destructive">Upload Failed</p>
              <p className="mt-0.5">{errorMessage}</p>
            </div>
          </div>
        )}
      </div>

      {/* Capabilities & Information Card */}
      <div className="bg-card text-card-foreground border border-border rounded-xl p-5 shadow-sm flex flex-col gap-3">
        <h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground flex items-center gap-2">
          <FiShield className="w-4 h-4 text-primary" />
          Automated Pipeline Tools
        </h3>

        <div className="space-y-2 text-xs text-muted-foreground max-h-[380px] overflow-y-auto pr-1">
          <div className="flex items-start gap-2.5 bg-muted/30 p-2.5 rounded-lg border border-border/50">
            <FiShield className="w-4 h-4 text-primary flex-shrink-0 mt-0.5" />
            <div>
              <span className="font-medium text-foreground">File Format & Architecture</span>
              <p className="text-[11px] text-muted-foreground mt-0.5">
                Inspects PE, ELF, Mach-O headers, architectures (x86_64, ARM64), and security mitigations (ASLR, DEP, PIE, CFG).
              </p>
            </div>
          </div>

          <div className="flex items-start gap-2.5 bg-muted/30 p-2.5 rounded-lg border border-border/50">
            <FiActivity className="w-4 h-4 text-emerald-400 flex-shrink-0 mt-0.5" />
            <div>
              <span className="font-medium text-foreground">Cryptographic Hashes & Entropy</span>
              <p className="text-[11px] text-muted-foreground mt-0.5">
                Calculates MD5, SHA-1, SHA-256, SHA-512 digests and whole-file Shannon entropy to flag packing/encryption.
              </p>
            </div>
          </div>

          <div className="flex items-start gap-2.5 bg-muted/30 p-2.5 rounded-lg border border-border/50">
            <FiAlertTriangle className="w-4 h-4 text-amber-400 flex-shrink-0 mt-0.5" />
            <div>
              <span className="font-medium text-foreground">Heuristic Threat & Anomaly Scorer</span>
              <p className="text-[11px] text-muted-foreground mt-0.5">
                Computes composite threat score (0-100) weighing high entropy, evasion APIs, injection stubs, and droppers.
              </p>
            </div>
          </div>

          <div className="flex items-start gap-2.5 bg-muted/30 p-2.5 rounded-lg border border-border/50">
            <FiTarget className="w-4 h-4 text-rose-400 flex-shrink-0 mt-0.5" />
            <div>
              <span className="font-medium text-foreground">MITRE ATT&CK Matrix Mapper</span>
              <p className="text-[11px] text-muted-foreground mt-0.5">
                Maps static signatures to MITRE ATT&CK tactics & techniques (Defense Evasion, Persistence, Credential Access).
              </p>
            </div>
          </div>

          <div className="flex items-start gap-2.5 bg-muted/30 p-2.5 rounded-lg border border-border/50">
            <FiGlobe className="w-4 h-4 text-blue-400 flex-shrink-0 mt-0.5" />
            <div>
              <span className="font-medium text-foreground">Network Endpoints & C2 Extractor</span>
              <p className="text-[11px] text-muted-foreground mt-0.5">
                Discovers URLs, public IPv4/v6 endpoints, User-Agent strings, and known dynamic DNS / C2 beacon profiles.
              </p>
            </div>
          </div>

          <div className="flex items-start gap-2.5 bg-muted/30 p-2.5 rounded-lg border border-border/50">
            <FiTerminal className="w-4 h-4 text-purple-400 flex-shrink-0 mt-0.5" />
            <div>
              <span className="font-medium text-foreground">Assembly & Shellcode Pattern Detector</span>
              <p className="text-[11px] text-muted-foreground mt-0.5">
                Scans for NOP sleds, direct syscall stubs bypassing EDR hooks, PEB walking, and RDTSC anti-debug timing checks.
              </p>
            </div>
          </div>

          <div className="flex items-start gap-2.5 bg-muted/30 p-2.5 rounded-lg border border-border/50">
            <FiLock className="w-4 h-4 text-indigo-400 flex-shrink-0 mt-0.5" />
            <div>
              <span className="font-medium text-foreground">Cryptographic Algorithm Detector</span>
              <p className="text-[11px] text-muted-foreground mt-0.5">
                Pinpoints AES S-Boxes, MD5 IVs, SHA-256 K constants, ChaCha20 magic words, and Base64 tables.
              </p>
            </div>
          </div>

          <div className="flex items-start gap-2.5 bg-muted/30 p-2.5 rounded-lg border border-border/50">
            <FiKey className="w-4 h-4 text-yellow-400 flex-shrink-0 mt-0.5" />
            <div>
              <span className="font-medium text-foreground">Certificates, Signatures & Hardcoded Secrets</span>
              <p className="text-[11px] text-muted-foreground mt-0.5">
                Validates Authenticode certificates and extracts leaked API keys (AWS, GitHub, Slack tokens, JWTs, PEM blocks).
              </p>
            </div>
          </div>

          <div className="flex items-start gap-2.5 bg-muted/30 p-2.5 rounded-lg border border-border/50">
            <FiBox className="w-4 h-4 text-orange-400 flex-shrink-0 mt-0.5" />
            <div>
              <span className="font-medium text-foreground">Embedded Assets & Secondary Droppers</span>
              <p className="text-[11px] text-muted-foreground mt-0.5">
                Detects nested secondary PE executables (droppers), ZIP/GZIP archives, and embedded SQLite databases.
              </p>
            </div>
          </div>

          <div className="flex items-start gap-2.5 bg-muted/30 p-2.5 rounded-lg border border-border/50">
            <FiClock className="w-4 h-4 text-teal-400 flex-shrink-0 mt-0.5" />
            <div>
              <span className="font-medium text-foreground">Compiler & Timestamp Anomalies</span>
              <p className="text-[11px] text-muted-foreground mt-0.5">
                Inspects COFF TimeDateStamp for time-stomping anomalies, MSVC Rich headers, and language runtimes (.NET, Go, Rust).
              </p>
            </div>
          </div>

          <div className="flex items-start gap-2.5 bg-muted/30 p-2.5 rounded-lg border border-border/50">
            <FiLayers className="w-4 h-4 text-pink-400 flex-shrink-0 mt-0.5" />
            <div>
              <span className="font-medium text-foreground">Binary Sections & RWX Permissions</span>
              <p className="text-[11px] text-muted-foreground mt-0.5">
                Inspects section headers, raw/virtual sizes, memory permissions (flags dangerous RWX), and per-section entropy.
              </p>
            </div>
          </div>

          <div className="flex items-start gap-2.5 bg-muted/30 p-2.5 rounded-lg border border-border/50">
            <FiShare2 className="w-4 h-4 text-cyan-400 flex-shrink-0 mt-0.5" />
            <div>
              <span className="font-medium text-foreground">Imports & Library Dependencies</span>
              <p className="text-[11px] text-muted-foreground mt-0.5">
                Maps linked DLLs / shared objects and infers capabilities (Networking, Registry, Crypto, Shell, Injection).
              </p>
            </div>
          </div>

          <div className="flex items-start gap-2.5 bg-muted/30 p-2.5 rounded-lg border border-border/50">
            <FiTerminal className="w-4 h-4 text-accent-foreground flex-shrink-0 mt-0.5" />
            <div>
              <span className="font-medium text-foreground">String Analysis & Threat IOCs</span>
              <p className="text-[11px] text-muted-foreground mt-0.5">
                Extracts ASCII/Unicode strings and classifies URLs, IPs, commands, and process injection APIs.
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
