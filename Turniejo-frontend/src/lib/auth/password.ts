import { z } from "zod"

export const PASSWORD_MIN_LENGTH = 10

/** Podpowiedź pod polem hasła — te same wymagania, które sprawdza passwordSchema. */
export const PASSWORD_HINT = `Co najmniej ${PASSWORD_MIN_LENGTH} znaków, w tym mała i wielka litera oraz cyfra.`

// Limity 1:1 z PasswordPolicy w backendzie (i z passwordPolicy realmu w Keycloaku).
export const passwordSchema = z
  .string()
  .min(PASSWORD_MIN_LENGTH, `Hasło musi mieć co najmniej ${PASSWORD_MIN_LENGTH} znaków`)
  .max(100, "Hasło może mieć maksymalnie 100 znaków")
  .refine((value) => /\p{Ll}/u.test(value) && /\p{Lu}/u.test(value) && /\p{Nd}/u.test(value), {
    message: "Hasło musi zawierać małą i wielką literę oraz cyfrę",
  })
