import React from "react";
import { Terminal } from "lucide-react";
import { motion } from "framer-motion";

interface ReviewReportProps {
  report: string;
  loading: boolean;
}

export const ReviewReport: React.FC<ReviewReportProps> = ({
  report,
  loading,
}) => {
  return (
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
  );
};
