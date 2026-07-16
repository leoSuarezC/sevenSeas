<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { ManifestSummary } from '../types/manifest'

const props = defineProps<{
  manifests: ManifestSummary[]
  selectedId: string | null
  loading: boolean
  expected: number
}>()

const emit = defineEmits<{ select: [manifestId: string] }>()

const search = ref('')

/**
 * The bottle count the technician tallies by hand before checking anything in.
 *
 * Local to the screen on purpose: it is what the desk physically counted, not a fact about
 * the shipment, so it is not the server's to know until bottles are actually received.
 */
const counted = ref(props.expected)

// Re-seeded whenever the manifest changes — including on first load, when the expected
// count is still 0 because nothing has arrived from the API yet. The tally belongs to the
// manifest in front of the technician, so carrying the previous one over would be a lie.
watch(
  () => props.expected,
  (expected) => {
    counted.value = expected
  },
)

const matchesExpected = computed(() => counted.value === props.expected)

const visible = computed(() => {
  const term = search.value.trim().toLowerCase()

  if (!term) {
    return props.manifests
  }

  return props.manifests.filter(
    (manifest) =>
      manifest.code.toLowerCase().includes(term) ||
      manifest.originClinic.toLowerCase().includes(term),
  )
})

function chipFor(manifest: ManifestSummary) {
  if (manifest.openDiscrepancies > 0) {
    const count = manifest.openDiscrepancies
    return { text: `${count} discrepanc${count === 1 ? 'y' : 'ies'}`, tone: 'danger' }
  }

  if (manifest.status !== 'Open') {
    return { text: 'Received', tone: 'success' }
  }

  return { text: manifest.counts.pending > 0 ? 'In transit' : 'Ready', tone: 'info' }
}
</script>

<template>
  <aside class="worklist">
    <section class="card">
      <header class="card-head">
        <h2>Verification workflow</h2>
        <span class="tag">LAB SETTING</span>
      </header>
      <div class="toggle">
        <button type="button" class="on">Fast Count</button>
        <button type="button">Full Scan</button>
      </div>
    </section>

    <section class="card">
      <h3 class="label">Find manifest</h3>
      <div class="search">
        <span aria-hidden="true">▤</span>
        <input v-model="search" type="search" placeholder="Scan or search manifest…" />
      </div>
    </section>

    <section class="card">
      <h3 class="label">Verify &amp; receive</h3>
      <p class="hint">Total bottles counted by lab tech</p>
      <div class="stepper">
        <button type="button" aria-label="One fewer" @click="counted = Math.max(0, counted - 1)">
          −
        </button>
        <span class="count">{{ counted }}</span>
        <button type="button" aria-label="One more" @click="counted += 1">+</button>
      </div>
      <!-- With no manifest open there is nothing to match against, and "matches 0 expected"
           would read as approval of a count nobody has taken. -->
      <template v-if="expected > 0">
        <p v-if="matchesExpected" class="match ok">Matches {{ expected }} expected — ready to close.</p>
        <p v-else class="match off">Counted {{ counted }}, manifest lists {{ expected }}.</p>
      </template>
      <p v-else class="match idle">Open a manifest to verify its count.</p>
    </section>

    <section class="card recent">
      <h3 class="label">Recent manifests</h3>

      <p v-if="loading && manifests.length === 0" class="state">Loading manifests…</p>
      <p v-else-if="manifests.length === 0" class="state">
        No manifests for this lab yet. They appear here once a clinic ships one.
      </p>
      <p v-else-if="visible.length === 0" class="state">No manifest matches “{{ search }}”.</p>

      <ul v-else class="items">
        <li v-for="manifest in visible" :key="manifest.id">
          <button
            type="button"
            class="item"
            :class="{ current: manifest.id === selectedId }"
            :aria-current="manifest.id === selectedId ? 'true' : undefined"
            @click="emit('select', manifest.id)"
          >
            <span class="item-main">
              <strong>{{ manifest.code }}</strong>
              <span class="origin">{{ manifest.originClinic }}</span>
            </span>
            <span class="item-side">
              <span class="ratio">{{ manifest.counts.received }} / {{ manifest.counts.expected }} received</span>
              <span class="chip" :class="chipFor(manifest).tone">{{ chipFor(manifest).text }}</span>
            </span>
          </button>
        </li>
      </ul>
    </section>

    <button type="button" class="all">View all manifests ›</button>
  </aside>
</template>

<style scoped>
.worklist {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.card {
  background: var(--surface);
  border: 1px solid var(--line);
  border-radius: var(--radius);
  padding: 10px 12px;
}

.card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

h2 {
  margin: 0;
  font-size: 12px;
  font-weight: 600;
}

.tag {
  color: var(--ink-faint);
  font-size: 9px;
  letter-spacing: 0.06em;
}

.label {
  margin: 0 0 6px;
  color: var(--ink-faint);
  font-size: 10px;
  font-weight: 600;
  letter-spacing: 0.07em;
  text-transform: uppercase;
}

.toggle {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 6px;
}

.toggle button {
  padding: 6px;
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  background: var(--surface);
  color: var(--ink-soft);
  font-size: 12px;
}

.toggle .on {
  background: var(--navy);
  border-color: var(--navy);
  color: #fff;
  font-weight: 600;
}

.search {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 5px 8px;
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  color: var(--ink-faint);
}

.search input {
  flex: 1;
  border: 0;
  outline: none;
  font-size: 12px;
  color: var(--ink);
}

.hint {
  margin: 0 0 6px;
  color: var(--ink-soft);
  font-size: 11px;
}

.stepper {
  display: flex;
  align-items: center;
  gap: 10px;
}

.stepper button {
  width: 26px;
  height: 26px;
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  background: var(--surface);
  color: var(--ink-soft);
  font-size: 14px;
  line-height: 1;
}

.count {
  min-width: 26px;
  font-size: 15px;
  font-weight: 700;
  text-align: center;
}

.match {
  margin: 8px 0 0;
  font-size: 11px;
  font-weight: 600;
}

.match.ok {
  color: var(--green);
}

.match.off {
  color: var(--amber);
}

.match.idle {
  color: var(--ink-faint);
  font-weight: 400;
}

.items {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  width: 100%;
  padding: 8px;
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  background: var(--surface);
  text-align: left;
}

.item:hover {
  border-color: #c4d4e4;
  background: var(--canvas-soft);
}

.item.current {
  border-color: var(--blue);
  background: #eef4fa;
}

.item-main {
  display: flex;
  flex-direction: column;
  gap: 1px;
  min-width: 0;
}

.item-main strong {
  font-size: 12px;
}

.origin {
  color: var(--ink-faint);
  font-size: 10px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.item-side {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 3px;
  flex-shrink: 0;
}

.ratio {
  color: var(--ink-faint);
  font-size: 10px;
}

.chip {
  padding: 1px 7px;
  border-radius: 9px;
  font-size: 9px;
  font-weight: 600;
}

.chip.info {
  background: #e9f0f7;
  color: var(--blue);
}

.chip.success {
  background: var(--green-soft);
  color: var(--green);
}

.chip.danger {
  background: var(--red-soft);
  color: var(--red);
}

.state {
  margin: 0;
  padding: 10px 2px;
  color: var(--ink-faint);
  font-size: 11px;
}

.all {
  padding: 7px;
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  background: var(--surface);
  color: var(--ink-soft);
  font-size: 11px;
}
</style>
