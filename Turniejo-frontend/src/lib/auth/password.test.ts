import { describe, expect, it } from "vitest"

import { passwordSchema } from "./password"

// Te same przypadki co PasswordPolicyTests w backendzie — obie strony muszą się zgadzać.
describe("passwordSchema", () => {
  it.each(["NoweHaslo1", "Żółtakartka7", "dluga fraza z Cyfra 4"])("przyjmuje %s", (password) => {
    expect(passwordSchema.safeParse(password).success).toBe(true)
  })

  it.each([
    ["", "Hasło musi mieć co najmniej 10 znaków"],
    ["Haslo123!", "Hasło musi mieć co najmniej 10 znaków"],
    ["nowehaslo123", "Hasło musi zawierać małą i wielką literę oraz cyfrę"],
    ["NOWEHASLO123", "Hasło musi zawierać małą i wielką literę oraz cyfrę"],
    ["NoweHasloBezCyfr", "Hasło musi zawierać małą i wielką literę oraz cyfrę"],
    ["Aa1" + "x".repeat(98), "Hasło może mieć maksymalnie 100 znaków"],
  ])("odrzuca %s", (password, message) => {
    const result = passwordSchema.safeParse(password)
    expect(result.success).toBe(false)
    expect(result.error?.issues[0]?.message).toBe(message)
  })
})
