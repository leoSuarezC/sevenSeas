import { defineStore } from 'pinia'
import { ref } from 'vue'
import { manifestsApi, sessionApi } from '../api/manifests'
import { ApiError } from '../api/client'
import type { ManifestDetail, ManifestSummary, Session } from '../types/manifest'

/**
 * The check-in screen's state.
 *
 * Counts and status are never recomputed here — every action answers with the manifest as
 * the server now sees it, and that answer replaces what we hold. It keeps the client from
 * drifting from the server about what "received" means, and it means a rejected action
 * leaves the screen showing the truth rather than an optimistic guess.
 */
export const useManifestsStore = defineStore('manifests', () => {
  const session = ref<Session | null>(null)
  const manifests = ref<ManifestSummary[]>([])
  const selected = ref<ManifestDetail | null>(null)

  const loadingList = ref(false)
  const loadingDetail = ref(false)
  /** Set while an action is in flight, to keep a double-click from firing twice. */
  const working = ref(false)
  const error = ref<string | null>(null)

  async function loadManifests() {
    loadingList.value = true
    error.value = null

    try {
      // Asked for together: the header naming a lab the list does not belong to would be
      // worse than showing neither.
      const [current, list] = await Promise.all([sessionApi.get(), manifestsApi.list()])

      session.value = current
      manifests.value = list

      if (!selected.value && manifests.value.length > 0) {
        await selectManifest(manifests.value[0].id)
      }
    } catch (caught) {
      error.value = messageOf(caught)
    } finally {
      loadingList.value = false
    }
  }

  async function selectManifest(manifestId: string) {
    loadingDetail.value = true
    error.value = null

    try {
      selected.value = await manifestsApi.get(manifestId)
    } catch (caught) {
      error.value = messageOf(caught)
      selected.value = null
    } finally {
      loadingDetail.value = false
    }
  }

  const receive = (specimenId: string) =>
    act((manifestId) => manifestsApi.receive(manifestId, specimenId))

  const flag = (specimenId: string, notes?: string) =>
    act((manifestId) => manifestsApi.flag(manifestId, specimenId, notes))

  const close = () => act((manifestId) => manifestsApi.close(manifestId))

  /** Runs an action against the selected manifest and adopts the state it answers with. */
  async function act(action: (manifestId: string) => Promise<ManifestDetail>) {
    const manifest = selected.value

    if (!manifest || working.value) {
      return
    }

    working.value = true
    error.value = null

    try {
      selected.value = await action(manifest.id)

      // The worklist shows counts and status too, so it would go stale otherwise.
      manifests.value = await manifestsApi.list()
    } catch (caught) {
      error.value = messageOf(caught)
    } finally {
      working.value = false
    }
  }

  function dismissError() {
    error.value = null
  }

  return {
    session,
    manifests,
    selected,
    loadingList,
    loadingDetail,
    working,
    error,
    loadManifests,
    selectManifest,
    receive,
    flag,
    close,
    dismissError,
  }
})

function messageOf(caught: unknown): string {
  // The API explains its refusals; anything else is unexpected and should not be dressed
  // up as if we understood it.
  return caught instanceof ApiError ? caught.message : 'Something went wrong. Please try again.'
}
