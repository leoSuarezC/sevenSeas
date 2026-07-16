<script setup lang="ts">
import { computed } from 'vue'
import type { SpecimenStatus } from '../types/manifest'

const props = defineProps<{ status: SpecimenStatus }>()

/** Received reads as a tick, flagged as a warning — colour is never the only signal. */
const mark = computed(() => {
  switch (props.status) {
    case 'Received':
      return '✓'
    case 'Flagged':
      return '⚑'
    default:
      return '○'
  }
})
</script>

<template>
  <span class="pill" :class="status.toLowerCase()">
    <span aria-hidden="true">{{ mark }}</span>
    {{ status }}
  </span>
</template>

<style scoped>
.pill {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  border: 1px solid;
  border-radius: 10px;
  font-size: 11px;
  font-weight: 600;
  white-space: nowrap;
}

.received {
  background: var(--green-soft);
  border-color: var(--green-line);
  color: var(--green);
}

.flagged {
  background: var(--red-soft);
  border-color: var(--red-line);
  color: var(--red);
}

.pending {
  background: var(--canvas);
  border-color: var(--line);
  color: var(--ink-soft);
}
</style>
