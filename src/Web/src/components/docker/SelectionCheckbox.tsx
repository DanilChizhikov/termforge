export interface SelectionCheckboxProps {
  label: string;
  checked: boolean;
  indeterminate?: boolean;
  disabled: boolean;
  onChange: () => void;
}

export function SelectionCheckbox({ label, checked, indeterminate = false, disabled, onChange }: SelectionCheckboxProps) {
  const applyIndeterminate = (input: HTMLInputElement | null) => {
    if (input !== null) {
      input.indeterminate = indeterminate && !checked;
    }
  };

  return (
    <input
      ref={applyIndeterminate}
      type="checkbox"
      className="checkbox docker-select"
      aria-label={label}
      checked={checked}
      disabled={disabled}
      onChange={onChange}
    />
  );
}
