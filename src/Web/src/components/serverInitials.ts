const WORD_SEPARATOR = /[^\p{L}\p{N}]+/u;
const DIGITS_ONLY = /^\p{N}+$/u;

function significantDigit(digits: string): string {
  const trimmed = digits.replace(/^0+(?=\d)/, '');
  return trimmed.slice(0, 1);
}

export function serverInitials(name: string): string {
  const words = name.split(WORD_SEPARATOR).filter((word) => word !== '');
  const first = words[0];
  if (first === undefined) {
    return '?';
  }
  const last = words[words.length - 1] ?? first;
  if (words.length === 1) {
    return first.slice(0, 2).toLowerCase();
  }
  const second = DIGITS_ONLY.test(last) ? significantDigit(last) : last.slice(0, 1);
  return `${first.slice(0, 1)}${second}`.toLowerCase();
}
