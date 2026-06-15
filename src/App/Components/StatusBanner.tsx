import React from "react";
import { Loader2, CheckCircle2, FileCode } from "lucide-react";

interface StatusBannerProps {
  loading: boolean;
  status: string;
  scannedFilesCount: number;
}

export const StatusBanner: React.FC<StatusBannerProps> = ({
  loading,
  status,
  scannedFilesCount,
}) => {
  return (
    <div className="flex flex-wrap items-center gap-4 bg-slate-900/80 border border-slate-800 py-3 px-4 rounded-xl backdrop-blur-md w-full h-full">
      <div className="flex items-center gap-2 px-3 py-1 bg-blue-500/10 border border-blue-500/20 rounded-full text-blue-400 text-sm font-medium">
        {loading ? (
          <Loader2 size={14} className="animate-spin" />
        ) : (
          <CheckCircle2 size={14} />
        )}
        {status || "Agent Active"}
      </div>

      {scannedFilesCount > 0 && (
        <div className="flex items-center gap-2 text-slate-400 text-sm">
          <FileCode size={16} />
          <span>{scannedFilesCount} files scanned</span>
        </div>
      )}
    </div>
  );
};
