import { useState, useCallback } from "react";
import { BACKEND_BASE_URL } from "./backend";
import { extractJsonObjects } from "./jsonStream";

interface ReviewChunk {
  metadata?: {
    status: string;
    scannedFiles: string[];
    errorMessage?: string;
  };
  reportChunk?: string;
  section?: string;
}

interface UseReviewResult {
  url: string;
  setUrl: (url: string) => void;
  loading: boolean;
  report: string;
  status: string;
  scannedFiles: string[];
  suggestions: string;
  isModalOpen: boolean;
  setIsModalOpen: (isOpen: boolean) => void;
  handleSubmit: (e: React.FormEvent) => Promise<void>;
}

export const useReview = (): UseReviewResult => {
  const [url, setUrl] = useState("");
  const [loading, setLoading] = useState(false);
  const [report, setReport] = useState("");
  const [status, setStatus] = useState<string>("");
  const [scannedFiles, setScannedFiles] = useState<string[]>([]);
  const [suggestions, setSuggestions] = useState("");
  const [isModalOpen, setIsModalOpen] = useState(false);

  const handleSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      if (!url) return;

      setLoading(true);
      setReport("");
      setScannedFiles([]);
      setSuggestions("");
      setIsModalOpen(false);
      setStatus("Initializing review...");

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

            const { objects, rest } = extractJsonObjects(buffer);
            buffer = rest;

            for (const jsonStr of objects) {
              try {
                const data: ReviewChunk = JSON.parse(jsonStr);
                if (data.reportChunk) setReport((p) => p + data.reportChunk);
                if (data.section === "Suggestions" && data.reportChunk) {
                  setSuggestions((p) => p + data.reportChunk);
                }
                if (data.metadata?.status) setStatus(data.metadata.status);
                if (data.metadata?.errorMessage)
                  setReport(
                    (p) => p + `\n\nError: ${data.metadata!.errorMessage}`,
                  );
                if (data.metadata?.scannedFiles)
                  setScannedFiles(data.metadata.scannedFiles);
              } catch (e) {
                console.error("Chunk parse error", e);
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
    },
    [url],
  ); // Dependency array for useCallback

  return {
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
  };
};
