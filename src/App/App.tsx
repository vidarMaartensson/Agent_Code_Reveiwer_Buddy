import React from "react";
import { motion, AnimatePresence } from "framer-motion";
import { Sparkles } from "lucide-react";

import { SearchForm } from "./components/SearchForm";
import { StatusBanner } from "./components/StatusBanner";
import { ReviewReport } from "./components/ReviewReport";
import { BackgroundStars } from "./components/BackgroundStars";
import { SuggestionsModal } from "./components/SuggestionsModal";
import { FileChips } from "./components/FileChips"; // Keep this import
import { useReview } from "./utilities/useReview";

function App() {
  const {
    url,
    setUrl,
    loading,
    report,
    status,
    scannedFiles,
    suggestions,
    isModalOpen,
    setIsModalOpen,
    handleSubmit,
  } = useReview();

  return (
    <div className="min-h-screen flex flex-col items-center justify-center p-6 bg-[radial-gradient(ellipse_at_top,_var(--tw-gradient-stops))] from-slate-900 via-slate-950 to-black">
      <BackgroundStars />

      <motion.div
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
        className="w-full max-w-4xl text-center relative z-10"
      >
        <h1 className="text-5xl font-extrabold tracking-tight mb-2 pb-2 bg-clip-text text-transparent bg-gradient-to-r from-blue-400 to-emerald-400">
          Agent Code Reviewer Buddy
        </h1>
        <p className="text-slate-400 mb-12 text-lg">
          Your automated AI companion for high-quality code reviews.
        </p>

        <SearchForm
          url={url}
          setUrl={setUrl}
          onSubmit={handleSubmit}
          loading={loading}
        />

        {/* Results Area */}
        <AnimatePresence>
          {(report || loading) && (
            <motion.div
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              className="w-full max-w-4xl space-y-6 text-left"
            >
              {/* Status & Metadata Bar */}
              <div className="flex flex-col sm:flex-row sm:items-stretch gap-2">
                <div className="flex-grow flex">
                  <StatusBanner
                    loading={loading}
                    status={status}
                    scannedFilesCount={scannedFiles.length}
                  />
                </div>
                {status === "Success" && !loading && (
                  <button
                    onClick={() => setIsModalOpen(true)}
                    className="flex items-center justify-center gap-2 px-6 py-3 bg-emerald-500 hover:bg-emerald-400 text-slate-900 rounded-2xl text-sm font-bold transition-all hover:scale-105 active:scale-95 shadow-lg shadow-emerald-500/20 whitespace-nowrap"
                  >
                    <Sparkles size={18} />
                    View Suggestions
                  </button>
                )}
              </div>

              {/* Scanned Files List (Chips) */}
              <FileChips files={scannedFiles} />

              {/* The Report Body */}
              <ReviewReport report={report} loading={loading} />
            </motion.div>
          )}
        </AnimatePresence>
      </motion.div>

      <SuggestionsModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        suggestions={suggestions}
      />
    </div>
  );
}

export default App;
