export type FieldError = { field: string; message: string }

/**
 * Body błędu z backendu — jeden z trzech kształtów:
 * - 422 z ExceptionHandlingMiddleware (FluentValidation): ProblemDetails + errors: [{ field, message }]
 * - 400 z [ApiController] (błąd bindowania modelu): ProblemDetails + errors: { Pole: ["komunikat"] }
 * - 400/404/409 z Result.ToActionResult(): { code: "Validation.Email", message, type }
 */
type ProblemDetails = {
  status?: number
  title?: string
  detail?: string
  errors?: FieldError[] | Record<string, string[]>
  code?: string
  message?: string
}

// Result.Error niesie pole w kodzie ("Validation.Email") — traktujemy go jak błąd walidacji pola.
function fromResultError(problem: ProblemDetails): ProblemDetails {
  if (!problem.code || !problem.message) return problem
  const [kind, field] = problem.code.split(".", 2)
  return {
    ...problem,
    detail: problem.message,
    errors: kind === "Validation" && field ? [{ field, message: problem.message }] : undefined,
  }
}

function normalizeFieldErrors(errors: ProblemDetails["errors"]): FieldError[] {
  if (!errors) return []
  if (Array.isArray(errors)) return errors
  return Object.entries(errors).flatMap(([field, messages]) =>
    messages.map((message) => ({ field, message })),
  )
}

export class ApiError extends Error {
  readonly status: number
  readonly title?: string
  readonly detail?: string
  /** Błędy walidacji (400/422) per pole — puste dla błędów domenowych. */
  readonly fieldErrors: FieldError[]
  /** Sekundy z nagłówka Retry-After (429). */
  readonly retryAfter?: number

  constructor(status: number, problem: ProblemDetails, retryAfter?: number) {
    super(problem.detail ?? problem.title ?? `HTTP ${status}`)
    this.name = "ApiError"
    this.status = status
    this.title = problem.title
    this.detail = problem.detail
    this.fieldErrors = normalizeFieldErrors(problem.errors)
    this.retryAfter = retryAfter
  }

  static async fromResponse(response: Response) {
    let problem: ProblemDetails = {}
    try {
      problem = await response.json()
    } catch {
      // 401 z JwtBearer i 429 z rate limitera przychodzą bez body.
    }
    const retryAfter = Number(response.headers.get("Retry-After")) || undefined
    return new ApiError(response.status, fromResultError(problem), retryAfter)
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError
}
