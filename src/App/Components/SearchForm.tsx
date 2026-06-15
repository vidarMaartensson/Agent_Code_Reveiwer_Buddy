import React from "react";
import { Search, Loader2 } from "lucide-react";

interface SearchFormProps {
  url: string;
  setUrl: (url: string) => void;
  onSubmit: (e: React.FormEvent) => void;
  loading: boolean;
}

export const SearchForm: React.FC<SearchFormProps> = ({
  url,
  setUrl,
  onSubmit,
  loading,
}) => {
  return (
    <form
      onSubmit={onSubmit}
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
          className="px-8 bg-blue-600 hover:bg-blue-500 disabled:bg-slate-800 transition-colors font-semibold h-[56px] min-w-[120px] flex items-center justify-center"
        >
          {loading ? <Loader2 className="animate-spin" /> : "Review"}
        </button>
      </div>
    </form>
  );
};
