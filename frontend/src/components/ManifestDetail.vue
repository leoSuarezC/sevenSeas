<script setup lang="ts">
import { computed } from 'vue'
import StatusPill from './StatusPill.vue'
import type { ManifestDetail } from '../types/manifest'

const props = defineProps<{
  manifest: ManifestDetail
  working: boolean
}>()

const emit = defineEmits<{
  receive: [specimenId: string]
  flag: [specimenId: string]
  close: []
}>()

const isClosed = computed(() => props.manifest.status !== 'Open')

/**
 * The header action flags the next bottle still unaccounted for.
 *
 * A discrepancy is always about a specific bottle, so this needs a target. The per-row
 * flags cover picking one deliberately; this covers the common case at the desk — working
 * down the list and reaching one that is not in the box.
 */
const nextPending = computed(
  () => props.manifest.specimens.find((specimen) => specimen.status === 'Pending') ?? null,
)

const closeLabel = computed(() => {
  if (isClosed.value) {
    return props.manifest.status === 'ClosedWithDiscrepancy' ? 'Closed with discrepancy' : 'Closed'
  }

  return 'Mark Received & Close'
})

/**
 * The close button explains itself rather than just going grey: a technician should not
 * have to guess which of seven bottles is holding the manifest open.
 */
const closeHint = computed(() => {
  if (isClosed.value) {
    return 'This manifest is closed.'
  }

  const { pending } = props.manifest.counts

  return pending > 0
    ? `${pending} specimen${pending === 1 ? '' : 's'} still pending — receive or flag them to close.`
    : ''
})

const sentAt = computed(() =>
  new Date(props.manifest.sentAt).toLocaleString(undefined, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }),
)

const time = (value: string | null) =>
  value ? new Date(value).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' }) : '—'
</script>

<template>
  <section class="detail">
    <header class="head">
      <div class="title">
        <h1>Manifest {{ manifest.code }}</h1>
        <span class="tag">Fast Count</span>
      </div>
      <div class="actions">
        <button
          type="button"
          class="danger"
          :disabled="working || isClosed || !nextPending"
          :title="nextPending ? `Flag ${nextPending.code} missing` : 'Nothing is pending'"
          @click="nextPending && emit('flag', nextPending.id)"
        >
          ⚑ Flag discrepancy
        </button>
        <button
          type="button"
          class="primary"
          :disabled="working || isClosed || !manifest.counts.readyToClose"
          :title="closeHint"
          @click="emit('close')"
        >
          {{ closeLabel }}
        </button>
      </div>
    </header>

    <p class="meta">
      From {{ manifest.originClinic }} · Sent {{ sentAt }} ·
      {{ manifest.counts.expected }} specimens expected
    </p>
    <p v-if="closeHint" class="close-hint">{{ closeHint }}</p>

    <div class="tiles">
      <div class="tile">
        <span class="value">{{ manifest.counts.expected }}</span>
        <span class="name">Expected</span>
      </div>
      <div class="tile">
        <span class="value green">{{ manifest.counts.received }}</span>
        <span class="name">Received</span>
      </div>
      <div class="tile">
        <span class="value">{{ manifest.counts.pending }}</span>
        <span class="name">Pending</span>
      </div>
      <div class="tile">
        <span class="value" :class="{ red: manifest.counts.flagged > 0 }">
          {{ manifest.counts.flagged }}
        </span>
        <span class="name">Flagged</span>
      </div>
    </div>

    <div class="card">
      <header class="card-head">
        <h2>Specimens on manifest</h2>
        <span class="chip">{{ manifest.counts.received }} received</span>
      </header>

      <p v-if="manifest.specimens.length === 0" class="state">
        This manifest lists no specimens.
      </p>

      <table v-else>
        <thead>
          <tr>
            <th>Status</th>
            <th>Specimen ID</th>
            <th>Patient</th>
            <th>Site</th>
            <th>Provider</th>
            <th>Received by</th>
            <th>At</th>
            <th class="right">Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="specimen in manifest.specimens" :key="specimen.id">
            <td><StatusPill :status="specimen.status" /></td>
            <td class="mono">{{ specimen.code }}</td>
            <td>{{ specimen.patient }}</td>
            <td class="soft">{{ specimen.site }}</td>
            <td class="soft">{{ specimen.provider }}</td>
            <td class="soft">{{ specimen.receivedBy ?? '—' }}</td>
            <td class="soft">{{ time(specimen.receivedAt) }}</td>
            <td class="right">
              <button
                type="button"
                class="row-action"
                :disabled="working || isClosed || specimen.status === 'Received'"
                :title="`Mark ${specimen.code} received`"
                @click="emit('receive', specimen.id)"
              >
                ✓
              </button>
              <button
                type="button"
                class="row-action danger"
                :disabled="working || isClosed || specimen.status === 'Flagged'"
                :title="`Flag ${specimen.code} missing`"
                @click="emit('flag', specimen.id)"
              >
                ⚑
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<style scoped>
.detail {
  display: flex;
  flex-direction: column;
  gap: 10px;
  min-width: 0;
}

.head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.title {
  display: flex;
  align-items: center;
  gap: 8px;
}

h1 {
  margin: 0;
  font-size: 15px;
  font-weight: 700;
}

.tag {
  padding: 1px 7px;
  border-radius: 9px;
  background: #e9f0f7;
  color: var(--blue);
  font-size: 9px;
  font-weight: 600;
}

.actions {
  display: flex;
  gap: 8px;
}

.actions button {
  padding: 6px 12px;
  border-radius: var(--radius-sm);
  font-size: 12px;
  font-weight: 600;
}

.primary {
  border: 1px solid var(--navy);
  background: var(--navy);
  color: #fff;
}

.primary:hover:not(:disabled) {
  background: var(--navy-deep);
}

.danger {
  border: 1px solid var(--red-line);
  background: var(--surface);
  color: var(--red);
}

.danger:hover:not(:disabled) {
  background: var(--red-soft);
}

.meta {
  margin: -4px 0 0;
  color: var(--ink-faint);
  font-size: 11px;
}

.close-hint {
  margin: 0;
  color: var(--amber);
  font-size: 11px;
}

.tiles {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 10px;
}

.tile {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  padding: 10px;
  background: var(--surface);
  border: 1px solid var(--line);
  border-radius: var(--radius);
}

.value {
  font-size: 20px;
  font-weight: 700;
}

.value.green {
  color: var(--green);
}

.value.red {
  color: var(--red);
}

.name {
  color: var(--ink-faint);
  font-size: 9px;
  font-weight: 600;
  letter-spacing: 0.07em;
  text-transform: uppercase;
}

.card {
  background: var(--surface);
  border: 1px solid var(--line);
  border-radius: var(--radius);
  overflow: hidden;
}

.card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  border-bottom: 1px solid var(--line-soft);
}

h2 {
  margin: 0;
  font-size: 12px;
  font-weight: 600;
}

.chip {
  padding: 1px 7px;
  border-radius: 9px;
  background: var(--green-soft);
  color: var(--green);
  font-size: 9px;
  font-weight: 600;
}

table {
  width: 100%;
  border-collapse: collapse;
}

th {
  padding: 6px 10px;
  background: var(--canvas-soft);
  border-bottom: 1px solid var(--line-soft);
  color: var(--ink-faint);
  font-size: 9px;
  font-weight: 600;
  letter-spacing: 0.06em;
  text-align: left;
  text-transform: uppercase;
}

td {
  padding: 6px 10px;
  border-bottom: 1px solid var(--line-soft);
  font-size: 12px;
}

tbody tr:last-child td {
  border-bottom: 0;
}

tbody tr:hover {
  background: var(--canvas-soft);
}

.mono {
  font-family: ui-monospace, 'Cascadia Mono', Consolas, monospace;
  font-size: 11px;
}

.soft {
  color: var(--ink-soft);
}

.right {
  text-align: right;
  white-space: nowrap;
}

.row-action {
  width: 22px;
  height: 22px;
  margin-left: 3px;
  border: 1px solid var(--line);
  border-radius: var(--radius-sm);
  background: var(--surface);
  color: var(--ink-soft);
  font-size: 11px;
  line-height: 1;
}

.row-action:hover:not(:disabled) {
  border-color: var(--green-line);
  background: var(--green-soft);
  color: var(--green);
}

.row-action.danger:hover:not(:disabled) {
  border-color: var(--red-line);
  background: var(--red-soft);
  color: var(--red);
}

.state {
  margin: 0;
  padding: 20px 12px;
  color: var(--ink-faint);
  font-size: 12px;
  text-align: center;
}
</style>
