import React from "react";

interface FileChipsProps {
  files: string[];
}

export const FileChips: React.FC<FileChipsProps> = ({ files }) => {
  if (files.length === 0) return null;
  return (
    <div className="flex flex-wrap gap-2">
      {files.map((file, idx) => (
        <span
          key={idx}
          className="text-[10px] font-mono px-2 py-0.5 bg-slate-800 text-slate-400 rounded border border-slate-700"
        >
          {file.split("/").pop()}
        </span>
      ))}
    </div>
  );
};
