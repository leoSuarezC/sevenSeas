/** The running tally shown above the specimen table. */
export interface Counts {
  expected: number
  received: number
  pending: number
  flagged: number
  readyToClose: boolean
}

export type ManifestStatus = 'Open' | 'Closed' | 'ClosedWithDiscrepancy'

export type SpecimenStatus = 'Pending' | 'Received' | 'Flagged'

/** A bottle listed on a manifest. */
export interface Specimen {
  id: string
  code: string
  patient: string
  site: string
  provider: string
  status: SpecimenStatus
  receivedBy: string | null
  receivedAt: string | null
}

/** A manifest as it appears in the left-hand worklist. */
export interface ManifestSummary {
  id: string
  code: string
  originClinic: string
  status: ManifestStatus
  sentAt: string
  counts: Counts
  openDiscrepancies: number
}

/** A manifest with everything the check-in screen needs to render it. */
export interface ManifestDetail {
  id: string
  code: string
  originClinic: string
  status: ManifestStatus
  sentAt: string
  counts: Counts
  specimens: Specimen[]
}

/**
 * The shape the API refuses with (RFC 7807), plus our stable reason code.
 *
 * The code is what to branch on — `detail` is prose written for a person and can be
 * reworded at any time.
 */
export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  code?: string
}

/** Who the current request is acting as, as the server sees it. */
export interface Session {
  labId: number
  labName: string
  labTech: string
}
