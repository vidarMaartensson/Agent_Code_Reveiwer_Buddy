// Set VITE_BACKEND_URL in Vercel; falls back to the local backend port
export const BACKEND_BASE_URL: string =
  import.meta.env.VITE_BACKEND_URL ?? "http://localhost:5199";
