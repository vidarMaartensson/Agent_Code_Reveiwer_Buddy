import React, { useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import {
  Search,
  Loader2,
  FileCode,
  AlertCircle,
  CheckCircle2,
  Terminal,
} from "lucide-react";

interface ReviewChunk {
  metadata?: {
    status: string;
    scannedFiles: string[];
    errorMessage?: string;
  };
  reportChunk?: string;
  section?: string;
}

function App() {
  const [url, setUrl] = useState("");
  const [loading, setLoading] = useState(false);
  const [report, setReport] = useState("");
  const [status, setStatus] = useState<string>("");
  const [scannedFiles, setScannedFiles] = useState<string[]>([]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!url) return;

    setLoading(true);
    setReport("");
    setScannedFiles([]);
    setStatus("Initializing review...");

    // Updated to match your local backend port
    const BACKEND_BASE_URL = "http://localhost:5199";

    try {
      console.log(`Attempting fetch to: ${BACKEND_BASE_URL}/review`);
      const response = await fetch(
        `${BACKEND_BASE_URL}/review?repoUrl=${encodeURIComponent(url)}`,
        {
          method: "POST",
        },
      );

      if (!response.ok) throw new Error("Failed to start review");

      const reader = response.body?.getReader();
      const decoder = new TextDecoder();

      let buffer = "";
      if (reader) {
        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          buffer += decoder.decode(value, { stream: true });

          let updated = true;
          while (updated) {
            updated = false;
            buffer = buffer.trim();

            // Handle leading array brackets or commas from the JSON stream
            if (buffer.startsWith("[") || buffer.startsWith(",")) {
              buffer = buffer.substring(1).trim();
              updated = true;
              continue;
            }

            if (buffer.startsWith("{")) {
              let depth = 0;
              let end = -1;
              for (let i = 0; i < buffer.length; i++) {
                if (buffer[i] === "{") depth++;
                else if (buffer[i] === "}") {
                  depth--;
                  if (depth === 0) {
                    end = i;
                    break;
                  }
                }
              }

              if (end !== -1) {
                const jsonStr = buffer.substring(0, end + 1);
                try {
                  const data: ReviewChunk = JSON.parse(jsonStr);
                  if (data.reportChunk) setReport((p) => p + data.reportChunk);
                  if (data.metadata?.status) setStatus(data.metadata.status);
                  if (data.metadata?.scannedFiles)
                    setScannedFiles(data.metadata.scannedFiles);
                } catch (e) {
                  console.error("Chunk parse error", e);
                }
                buffer = buffer.substring(end + 1).trim();
                updated = true;
              }
            }
          }
        }
      }
    } catch (err) {
      setStatus("Error");
      console.error("Fetch Error:", err);
      setReport(
        (prev) =>
          prev +
          `\n\nError: ${err instanceof Error ? err.message : "Unknown error"}`,
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex flex-col items-center justify-center p-6 bg-[radial-gradient(ellipse_at_top,_var(--tw-gradient-stops))] from-slate-900 via-slate-950 to-black">
      <motion.div
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
        className="w-full max-w-4xl text-center"
      >
        <h1 className="text-5xl font-extrabold tracking-tight mb-2 bg-clip-text text-transparent bg-gradient-to-r from-blue-400 to-emerald-400">
          Agent Code Reviewer Buddy
        </h1>
        <p className="text-slate-400 mb-12 text-lg">
          Your automated AI companion for high-quality code reviews.
        </p>

        <form
          onSubmit={handleSubmit}
          className="relative group max-w-2xl mx-auto mb-12"
        >
          <div className="absolute -inset-0.5 bg-gradient-to-r from-blue-500 to-emerald-500 rounded-xl blur opacity-30 group-hover:opacity-100 transition duration-1000 group-hover:duration-200"></div>
          <div className="relative flex items-center bg-slate-900 rounded-xl overflow-hidden border border-slate-800">
            <div className="pl-4 text-slate-500">
              <Search size={20} />
            </div>
            <input
              type="text"
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              placeholder="Enter GitHub Repository URL (e.g., https://github.com/user/repo)"
              className="w-full p-4 bg-transparent outline-none text-slate-100 placeholder:text-slate-600"
            />
            <button
              disabled={loading}
              className="px-8 bg-blue-600 hover:bg-blue-500 disabled:bg-slate-800 transition-colors font-semibold"
            >
              {loading ? <Loader2 className="animate-spin" /> : "Review"}
            </button>
          </div>
        </form>

        {/* Results Area */}
        <AnimatePresence>
          {(report || loading) && (
            <motion.div
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              className="w-full max-w-4xl space-y-6 text-left"
            >
              {/* Status & Metadata Bar */}
              <div className="flex flex-wrap items-center gap-4 bg-slate-900/80 border border-slate-800 p-4 rounded-xl backdrop-blur-md">
                <div className="flex items-center gap-2 px-3 py-1 bg-blue-500/10 border border-blue-500/20 rounded-full text-blue-400 text-sm font-medium">
                  {loading ? (
                    <Loader2 size={14} className="animate-spin" />
                  ) : (
                    <CheckCircle2 size={14} />
                  )}
                  {status || "Agent Active"}
                </div>

                {scannedFiles.length > 0 && (
                  <div className="flex items-center gap-2 text-slate-400 text-sm">
                    <FileCode size={16} />
                    <span>{scannedFiles.length} files scanned</span>
                  </div>
                )}
              </div>

              {/* Scanned Files List (Chips) */}
              {scannedFiles.length > 0 && (
                <div className="flex flex-wrap gap-2">
                  {scannedFiles.map((file, idx) => (
                    <span
                      key={idx}
                      className="text-[10px] font-mono px-2 py-0.5 bg-slate-800 text-slate-400 rounded border border-slate-700"
                    >
                      {file.split("/").pop()}
                    </span>
                  ))}
                </div>
              )}

              {/* The Report Body */}
              <div className="relative bg-slate-900/50 border border-slate-800 rounded-2xl overflow-hidden backdrop-blur-sm">
                <div className="flex items-center gap-2 px-4 py-2 bg-slate-800/50 border-b border-slate-800 text-xs font-mono text-slate-500">
                  <Terminal size={14} />
                  REVIE_LOG_STREAM
                </div>
                <div className="p-8 max-h-[600px] overflow-y-auto custom-scrollbar">
                  <pre className="whitespace-pre-wrap font-mono text-sm leading-relaxed text-slate-300">
                    {report || "Agent is starting the engine..."}
                  </pre>
                  {loading && (
                    <motion.span
                      animate={{ opacity: [0, 1, 0] }}
                      transition={{ repeat: Infinity, duration: 1 }}
                      className="inline-block w-2 h-4 ml-1 bg-emerald-500 align-middle"
                    />
                  )}
                </div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </motion.div>
    </div>
  );
}

export default App;
