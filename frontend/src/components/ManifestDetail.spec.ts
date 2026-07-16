import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ManifestDetail from './ManifestDetail.vue'
import type { ManifestDetail as Manifest, Specimen, SpecimenStatus } from '../types/manifest'

/**
 * The check-in screen's detail panel.
 *
 * What is worth testing here is the judgement the component makes on the technician's
 * behalf: which counts it shows, whether closing is offered, and which bottle an action
 * refers to. The styling is not — it is meant to be looked at, not asserted.
 */
describe('ManifestDetail', () => {
  it('shows the counts the server reported', () => {
    const panel = mount(ManifestDetail, {
      props: { manifest: manifestOf('Pending', 'Received', 'Received', 'Flagged'), working: false },
    })

    expect(tiles(panel)).toEqual({ Expected: '4', Received: '2', Pending: '1', Flagged: '1' })
  })

  it('will not offer to close a manifest with bottles still pending', () => {
    const panel = mount(ManifestDetail, {
      props: { manifest: manifestOf('Received', 'Pending', 'Pending'), working: false },
    })

    expect(closeButton(panel).attributes('disabled')).toBeDefined()

    // Disabling on its own would leave the technician guessing. The panel has to say what
    // is holding the manifest open.
    expect(panel.text()).toContain('2 specimens still pending')
  })

  it('offers to close once every bottle is accounted for', async () => {
    const panel = mount(ManifestDetail, {
      props: { manifest: manifestOf('Received', 'Flagged'), working: false },
    })

    // Flagged counts as accounted for: the lab knows the bottle is missing, which is what
    // reconciled means. Nothing is left unknown, so closing is allowed.
    expect(closeButton(panel).attributes('disabled')).toBeUndefined()

    await closeButton(panel).trigger('click')

    expect(panel.emitted('close')).toHaveLength(1)
  })

  it('asks to receive the bottle whose row was clicked', async () => {
    const manifest = manifestOf('Pending', 'Pending')
    const panel = mount(ManifestDetail, { props: { manifest, working: false } })

    await panel.findAll('tbody tr')[1].findAll('button')[0].trigger('click')

    expect(panel.emitted('receive')).toEqual([[manifest.specimens[1].id]])
  })

  it('flags the next unaccounted-for bottle from the header action', async () => {
    const manifest = manifestOf('Received', 'Pending')
    const panel = mount(ManifestDetail, { props: { manifest, working: false } })

    await panel.find('.actions .danger').trigger('click')

    // Not the first row — the first bottle is already in hand. The header action is about
    // the one the technician has just failed to find.
    expect(panel.emitted('flag')).toEqual([[manifest.specimens[1].id]])
  })

  it('stops offering actions once the manifest is closed', () => {
    const manifest = { ...manifestOf('Received'), status: 'ClosedWithDiscrepancy' as const }
    const panel = mount(ManifestDetail, { props: { manifest, working: false } })

    expect(closeButton(panel).text()).toBe('Closed with discrepancy')
    expect(panel.findAll('button').every((button) => button.attributes('disabled') !== undefined))
      .toBe(true)
  })

  it('holds off while an action is in flight', () => {
    const panel = mount(ManifestDetail, {
      props: { manifest: manifestOf('Received'), working: true },
    })

    // Guards the double-click: a second receive would be harmless server-side, but a
    // second close racing the first is worth not sending at all.
    expect(closeButton(panel).attributes('disabled')).toBeDefined()
  })
})

function closeButton(panel: ReturnType<typeof mount>) {
  return panel.find('.actions .primary')
}

/** Reads the stat tiles back as { label: value }, the way the technician reads them. */
function tiles(panel: ReturnType<typeof mount>) {
  return Object.fromEntries(
    panel.findAll('.tile').map((tile) => [tile.find('.name').text(), tile.find('.value').text()]),
  )
}

function manifestOf(...statuses: SpecimenStatus[]): Manifest {
  const specimens: Specimen[] = statuses.map((status, index) => ({
    id: `specimen-${index}`,
    code: `SP-2026-A00${41 + index}`,
    patient: 'Sarah Lin',
    site: 'Right cheek',
    provider: 'Dr. Patel',
    status,
    receivedBy: status === 'Received' ? 'Lab Tech 1' : null,
    receivedAt: status === 'Received' ? '2026-05-26T11:02:00Z' : null,
  }))

  const received = statuses.filter((status) => status === 'Received').length
  const pending = statuses.filter((status) => status === 'Pending').length
  const flagged = statuses.filter((status) => status === 'Flagged').length

  return {
    id: 'manifest-1',
    code: 'MF-2026-0042',
    originClinic: 'Riverside Clinic — Bay 2',
    status: 'Open',
    sentAt: '2026-05-26T10:48:00Z',
    // Mirrors the API: the counts come from the server, and the component renders them
    // rather than deriving its own from the rows.
    counts: { expected: statuses.length, received, pending, flagged, readyToClose: pending === 0 },
    specimens,
  }
}
