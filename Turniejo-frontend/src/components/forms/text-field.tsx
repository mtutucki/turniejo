import type { ComponentProps } from "react"
import type { FieldError as RhfFieldError } from "react-hook-form"

import { Field, FieldError, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"

type TextFieldProps = ComponentProps<typeof Input> & {
  id: string
  label: string
  error?: RhfFieldError
}

/** Etykieta + input + komunikat błędu (z Zod albo z API przez applyFieldErrors). */
export function TextField({ id, label, error, ...inputProps }: TextFieldProps) {
  return (
    <Field data-invalid={!!error}>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <Input id={id} aria-invalid={!!error} {...inputProps} />
      <FieldError errors={[error]} />
    </Field>
  )
}
