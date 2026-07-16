import type { ProblemDetails } from '../types/manifest'

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080'
const labId = import.meta.env.VITE_LAB_ID ?? '1'

/**
 * An error carrying what the server actually said.
 *
 * Keeps the server's reason code, so callers can tell "still pending" from "already
 * closed" without matching on a message that a later edit would break.
 */
export class ApiError extends Error {
  readonly status: number
  readonly code?: string

  constructor(message: string, status: number, code?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

/**
 * Calls the API as the current lab.
 *
 * The lab travels in a header on every request, and the server decides what that lab may
 * see — nothing here is a permission check. Changing VITE_LAB_ID only changes who you are,
 * not what you are allowed to reach.
 */
async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response

  try {
    response = await fetch(`${baseUrl}${path}`, {
      ...init,
      headers: {
        'Content-Type': 'application/json',
        'X-Lab-Id': labId,
        ...init?.headers,
      },
    })
  } catch {
    // fetch only rejects when the request never landed — the API is down, or CORS blocked
    // it. Worth saying plainly rather than surfacing "Failed to fetch" to a technician.
    throw new ApiError('Cannot reach the API. Is the backend running?', 0)
  }

  if (!response.ok) {
    const problem = await readProblem(response)

    throw new ApiError(
      problem?.detail ?? problem?.title ?? `Request failed (${response.status}).`,
      response.status,
      problem?.code,
    )
  }

  return response.json() as Promise<T>
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    // A refusal without a JSON body — nothing to add beyond the status code.
    return undefined
  }
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body ? JSON.stringify(body) : undefined }),
}
