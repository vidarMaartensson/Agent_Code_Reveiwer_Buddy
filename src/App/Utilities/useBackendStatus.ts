import { useEffect, useState } from "react";
import { BACKEND_BASE_URL } from "./backend";

export type BackendStatus = "checking" | "waking" | "ready" | "offline";

// Only show the wake-up bubble if the backend is slower than this
const SHOW_WAKING_AFTER_MS = 1500;
const ATTEMPT_TIMEOUT_MS = 10000;
const RETRY_DELAY_MS = 3000;
// Free-tier hosts can take up to a minute to cold start
const GIVE_UP_AFTER_MS = 120000;

export const useBackendStatus = (): BackendStatus => {
  const [status, setStatus] = useState<BackendStatus>("checking");

  useEffect(() => {
    let cancelled = false;
    const startedAt = Date.now();

    const wakingTimer = setTimeout(() => {
      if (!cancelled) setStatus((s) => (s === "checking" ? "waking" : s));
    }, SHOW_WAKING_AFTER_MS);

    const ping = async () => {
      while (!cancelled) {
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), ATTEMPT_TIMEOUT_MS);
        try {
          const res = await fetch(`${BACKEND_BASE_URL}/health`, {
            signal: controller.signal,
          });
          if (res.ok) {
            if (!cancelled) setStatus("ready");
            return;
          }
        } catch {
          // Backend still starting (or unreachable), retry below
        } finally {
          clearTimeout(timeout);
        }

        if (Date.now() - startedAt > GIVE_UP_AFTER_MS) {
          if (!cancelled) setStatus("offline");
          return;
        }
        await new Promise((r) => setTimeout(r, RETRY_DELAY_MS));
      }
    };

    ping();

    return () => {
      cancelled = true;
      clearTimeout(wakingTimer);
    };
  }, []);

  return status;
};
