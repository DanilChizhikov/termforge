export type PasswordStrengthScore = 0 | 1 | 2 | 3 | 4;

export interface PasswordStrength {
  score: PasswordStrengthScore;
  label: string;
}

const LABELS: Record<PasswordStrengthScore, string> = {
  0: 'Too short',
  1: 'Weak',
  2: 'Fair',
  3: 'Strong',
  4: 'Very strong',
};

const CHARACTER_CLASSES = [/[a-z]/, /[A-Z]/, /\d/, /[^A-Za-z0-9]/];

export function measurePassword(password: string, minLength: number): PasswordStrength {
  if (password.length < minLength) {
    return { score: 0, label: LABELS[0] };
  }
  const classes = CHARACTER_CLASSES.filter((pattern) => pattern.test(password)).length;
  let score = 1;
  if (password.length >= 16) {
    score += 1;
  }
  if (classes >= 2) {
    score += 1;
  }
  if (classes >= 3 && password.length >= 16) {
    score += 1;
  }
  const bounded = Math.min(score, 4) as PasswordStrengthScore;
  return { score: bounded, label: LABELS[bounded] };
}
