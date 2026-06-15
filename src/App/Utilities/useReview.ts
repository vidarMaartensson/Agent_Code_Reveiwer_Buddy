import { useState, useCallback } from "react";

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
                    if (data.reportChunk)
                      setReport((p) => p + data.reportChunk);
                    if (data.section === "Suggestions" && data.reportChunk) {
                      setSuggestions((p) => p + data.reportChunk);
                    }
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
