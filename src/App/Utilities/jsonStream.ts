// The backend streams a JSON array ("[{...},{...}]") piece by piece.
// Pulls out every complete top-level object from the buffer and returns what is left over.
// Braces inside strings (e.g. code snippets in the report) are ignored.
export const extractJsonObjects = (
  buffer: string,
): { objects: string[]; rest: string } => {
  const objects: string[] = [];
  let depth = 0;
  let start = -1;
  let inString = false;
  let escaped = false;
  let consumed = 0;

  for (let i = 0; i < buffer.length; i++) {
    const ch = buffer[i];

    if (inString) {
      if (escaped) escaped = false;
      else if (ch === "\\") escaped = true;
      else if (ch === '"') inString = false;
      continue;
    }

    if (ch === '"') inString = true;
    else if (ch === "{") {
      if (depth === 0) start = i;
      depth++;
    } else if (ch === "}" && depth > 0) {
      depth--;
      if (depth === 0) {
        objects.push(buffer.substring(start, i + 1));
        consumed = i + 1;
      }
    }
  }

  return { objects, rest: buffer.substring(consumed) };
};
