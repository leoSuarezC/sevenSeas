import { api } from './client'
import type { ManifestDetail, ManifestSummary, Session } from '../types/manifest'

/**
 * Who the server says we are.
 *
 * The header renders this rather than what the client asked to be, so the lab shown above
 * the table cannot disagree with the data inside it.
 */
export const sessionApi = {
  get: () => api.get<Session>('/session'),
}

/**
 * The check-in endpoints.
 *
 * Each action answers with the manifest as it now stands, so the screen re-renders from
 * what the server says rather than from what the client guessed would happen.
 */
export const manifestsApi = {
  list: () => api.get<ManifestSummary[]>('/manifests'),

  get: (manifestId: string) => api.get<ManifestDetail>(`/manifests/${manifestId}`),

  receive: (manifestId: string, specimenId: string) =>
    api.post<ManifestDetail>(`/manifests/${manifestId}/specimens/${specimenId}/receive`),

  flag: (manifestId: string, specimenId: string, notes?: string) =>
    api.post<ManifestDetail>(`/manifests/${manifestId}/specimens/${specimenId}/flag`, { notes }),

  close: (manifestId: string) => api.post<ManifestDetail>(`/manifests/${manifestId}/close`),
}
