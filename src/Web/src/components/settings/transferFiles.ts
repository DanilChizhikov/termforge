export type SaveFile = (fileName: string, content: BlobPart, type: string) => void;

export const downloadFile: SaveFile = (fileName, content, type) => {
  const blob = new Blob([content], { type });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.rel = 'noopener';
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 0);
};

function readFile<T>(read: (reader: FileReader) => void): Promise<T> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as T);
    reader.onerror = () => reject(reader.error ?? new Error('The file could not be read.'));
    read(reader);
  });
}

export function readFileText(file: Blob): Promise<string> {
  return readFile<string>((reader) => reader.readAsText(file));
}

export async function readFileBytes(file: Blob): Promise<Uint8Array> {
  const buffer = await readFile<ArrayBuffer>((reader) => reader.readAsArrayBuffer(file));
  return new Uint8Array(buffer);
}

export function exportDateStamp(now: Date): string {
  return now.toISOString().slice(0, 10);
}
