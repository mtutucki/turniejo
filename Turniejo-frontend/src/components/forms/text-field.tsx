import type { ComponentProps } from "react"
import type { FieldError as RhfFieldError } from "react-hook-form"

import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"

type TextFieldProps = ComponentProps<typeof Input> & {
  id: string
  label: string
  error?: RhfFieldError
  description?: string
}

/** Etykieta + input + opcjonalna podpowiedź + komunikat błędu (z Zod albo z API przez applyFieldErrors). */
export function TextField({ id, label, error, description, ...inputProps }: TextFieldProps) {
  return (
    <Field data-invalid={!!error}>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <Input
        id={id}
        aria-invalid={!!error}
        aria-describedby={description ? `${id}-description` : undefined}
        {...inputProps}
      />
      {description && <FieldDescription id={`${id}-description`}>{description}</FieldDescription>}
      <FieldError errors={[error]} />
    </Field>
  )
}
