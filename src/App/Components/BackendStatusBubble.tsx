import React, { useEffect, useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import { Loader2, CheckCircle2, AlertTriangle } from "lucide-react";
import type { BackendStatus } from "../Utilities/useBackendStatus";

interface BackendStatusBubbleProps {
  status: BackendStatus;
}

export const BackendStatusBubble: React.FC<BackendStatusBubbleProps> = ({
  status,
}) => {
  const [wasWaking, setWasWaking] = useState(false);
  const [showReady, setShowReady] = useState(false);

  // Briefly confirm readiness, but only if the user actually saw it waking up
  useEffect(() => {
    if (status === "waking") setWasWaking(true);
    if (status === "ready" && wasWaking) {
      setShowReady(true);
      const t = setTimeout(() => setShowReady(false), 2500);
      return () => clearTimeout(t);
    }
  }, [status, wasWaking]);

  const visible =
    status === "waking" || status === "offline" || showReady;

  return (
    <AnimatePresence>
      {visible && (
        <motion.div
          key={status}
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0, y: 20 }}
          className="fixed bottom-6 left-1/2 -translate-x-1/2 z-50 px-4 w-full max-w-md"
        >
          {status === "waking" && (
            <div className="flex items-start gap-3 bg-slate-900/90 border border-blue-500/30 rounded-2xl px-5 py-4 backdrop-blur-md shadow-lg shadow-blue-500/10 text-left">
              <Loader2 size={18} className="animate-spin text-blue-400 mt-0.5 shrink-0" />
              <div>
                <p className="text-sm font-semibold text-slate-100">
                  Waking up the backend...
                </p>
                <p className="text-xs text-slate-400">
                  The server sleeps when idle. This can take up to a minute.
                </p>
              </div>
            </div>
          )}
          {status === "ready" && (
            <div className="flex items-center gap-3 bg-slate-900/90 border border-emerald-500/30 rounded-2xl px-5 py-4 backdrop-blur-md shadow-lg shadow-emerald-500/10">
              <CheckCircle2 size={18} className="text-emerald-400 shrink-0" />
              <p className="text-sm font-semibold text-slate-100">
                Backend is ready!
              </p>
            </div>
          )}
          {status === "offline" && (
            <div className="flex items-start gap-3 bg-slate-900/90 border border-red-500/30 rounded-2xl px-5 py-4 backdrop-blur-md shadow-lg shadow-red-500/10 text-left">
              <AlertTriangle size={18} className="text-red-400 mt-0.5 shrink-0" />
              <div>
                <p className="text-sm font-semibold text-slate-100">
                  Couldn't reach the backend
                </p>
                <p className="text-xs text-slate-400">
                  Try reloading the page in a moment.
                </p>
              </div>
            </div>
          )}
        </motion.div>
      )}
    </AnimatePresence>
  );
};
