<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { storeToRefs } from 'pinia'
import ManifestWorklist from './components/ManifestWorklist.vue'
import ManifestDetail from './components/ManifestDetail.vue'
import { useManifestsStore } from './stores/manifests'

const store = useManifestsStore()
const { session, manifests, selected, loadingList, loadingDetail, working, error } = storeToRefs(store)

// Context only, per the brief — Check-In is the tab this slice implements.
const tabs = ['Check-In', 'Scan History', 'Manifests', 'Discrepancies']

const initials = computed(() =>
  (session.value?.labTech ?? '')
    .split(' ')
    .filter((part) => /[a-z]/i.test(part))
    .map((part) => part[0]!.toUpperCase())
    .join('')
    .slice(0, 2) || '—',
)

onMounted(() => store.loadManifests())
</script>

<template>
  <div class="app">
    <header class="bar">
      <div class="left">
        <span class="brand">IPI</span>
        <span class="env">UAT</span>
        <span class="crumb">Mode: <strong>Check-In</strong></span>
        <span class="crumb">Location: {{ session?.labName ?? '…' }} — Receiving</span>
      </div>
      <div class="right">
        <span class="crumb">{{ session?.labTech ?? '…' }}</span>
        <span class="avatar">{{ initials }}</span>
      </div>
    </header>

    <nav class="tabs">
      <button v-for="tab in tabs" :key="tab" type="button" :class="{ on: tab === 'Check-In' }">
        {{ tab }}
      </button>
    </nav>

    <div v-if="error" class="error" role="alert">
      <span>{{ error }}</span>
      <div class="error-actions">
        <button type="button" @click="store.loadManifests()">Retry</button>
        <button type="button" class="dismiss" aria-label="Dismiss" @click="store.dismissError()">
          ✕
        </button>
      </div>
    </div>

    <main class="layout">
      <ManifestWorklist
        :manifests="manifests"
        :selected-id="selected?.id ?? null"
        :loading="loadingList"
        :expected="selected?.counts.expected ?? 0"
        @select="store.selectManifest($event)"
      />

      <ManifestDetail
        v-if="selected"
        :manifest="selected"
        :working="working || loadingDetail"
        @receive="store.receive($event)"
        @flag="store.flag($event)"
        @close="store.close()"
      />

      <section v-else class="placeholder">
        <p v-if="loadingList || loadingDetail">Loading…</p>
        <!-- Only claim the lab has no manifests once we have actually heard back. While the
             API is unreachable we do not know that, and saying so would be a guess dressed
             up as a fact — the banner above already explains what went wrong. -->
        <p v-else-if="error">Manifests could not be loaded.</p>
        <p v-else-if="manifests.length === 0">
          Nothing to check in. Once a clinic ships a manifest to this lab, it appears here.
        </p>
        <p v-else>Select a manifest to start checking it in.</p>
      </section>
    </main>
  </div>
</template>

<style scoped>
.app {
  min-height: 100vh;
}

.bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 7px 14px;
  background: var(--navy);
  color: #dce6ef;
  font-size: 11px;
}

.left,
.right {
  display: flex;
  align-items: center;
  gap: 10px;
}

.brand {
  padding: 2px 7px;
  border-radius: var(--radius-sm);
  background: #fff;
  color: var(--navy);
  font-size: 11px;
  font-weight: 700;
}

.env {
  padding: 1px 6px;
  border: 1px solid #4d7194;
  border-radius: var(--radius-sm);
  font-size: 9px;
  font-weight: 600;
}

.crumb strong {
  color: #fff;
}

.avatar {
  display: grid;
  place-items: center;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  background: #4d7194;
  color: #fff;
  font-size: 9px;
  font-weight: 700;
}

.tabs {
  display: flex;
  gap: 2px;
  padding: 0 14px;
  background: var(--surface);
  border-bottom: 1px solid var(--line);
}

.tabs button {
  padding: 8px 12px;
  border: 0;
  border-bottom: 2px solid transparent;
  background: none;
  color: var(--ink-soft);
  font-size: 12px;
}

.tabs button.on {
  border-bottom-color: var(--blue);
  color: var(--blue);
  font-weight: 600;
}

.error {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin: 10px 14px -2px;
  padding: 8px 12px;
  background: var(--red-soft);
  border: 1px solid var(--red-line);
  border-radius: var(--radius);
  color: #8c2c22;
  font-size: 12px;
}

.error-actions {
  display: flex;
  gap: 6px;
  flex-shrink: 0;
}

.error button {
  padding: 3px 9px;
  border: 1px solid var(--red-line);
  border-radius: var(--radius-sm);
  background: var(--surface);
  color: var(--red);
  font-size: 11px;
  font-weight: 600;
}

.error .dismiss {
  border-color: transparent;
  background: none;
}

.layout {
  display: grid;
  grid-template-columns: 260px minmax(0, 1fr);
  gap: 12px;
  padding: 12px 14px;
  align-items: start;
}

.placeholder {
  display: grid;
  place-items: center;
  min-height: 260px;
  padding: 20px;
  background: var(--surface);
  border: 1px solid var(--line);
  border-radius: var(--radius);
  color: var(--ink-faint);
  text-align: center;
}

/* The desk runs on wide monitors, but the panel should not become unusable if it is not. */
@media (max-width: 860px) {
  .layout {
    grid-template-columns: 1fr;
  }
}
</style>
