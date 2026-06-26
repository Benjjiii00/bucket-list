<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import placesService from '../services/placesService'
import type { TravelPlace } from '../services/placesService'

const route = useRoute()
const router = useRouter()

const place = ref<TravelPlace | null>(null)
const notes = ref('')
const status = ref<'bucketList' | 'visited'>('bucketList')
const photoUrl = ref<string | null>(null)
const activeTab = ref<'info' | 'notes' | 'photos'>('info')
const isSaving = ref(false)
const isUploading = ref(false)
const error = ref<string | null>(null)
const success = ref<string | null>(null)

onMounted(async () => {
  const id = Number(route.params.id)
  if (isNaN(id)) {
    error.value = 'Ungültige ID.'
    return
  }
  try {
    const data = await placesService.getById(id)
    place.value = data
    notes.value = data.notes ?? ''
    status.value = data.status
    photoUrl.value = data.photoUrl
  } catch {
    error.value = 'Ort konnte nicht geladen werden.'
  }
})

async function save() {
  if (!place.value) return
  isSaving.value = true
  error.value = null
  success.value = null
  try {
    const updated = await placesService.update(place.value.id, {
      name: place.value.name,
      country: place.value.country,
      region: place.value.region,
      latitude: place.value.latitude,
      longitude: place.value.longitude,
      status: status.value,
      notes: notes.value.trim() || null,
      photoUrl: photoUrl.value,
    })
    place.value = updated
    success.value = 'Gespeichert!'
    setTimeout(() => (success.value = null), 2500)
  } catch {
    error.value = 'Speichern fehlgeschlagen.'
  } finally {
    isSaving.value = false
  }
}

async function handleFileUpload(event: Event) {
  const input = event.target as HTMLInputElement
  if (!input.files?.length || !place.value) return
  isUploading.value = true
  error.value = null
  try {
    const updated = await placesService.uploadPhoto(place.value.id, input.files[0])
    place.value = updated
    photoUrl.value = updated.photoUrl
  } catch {
    error.value = 'Foto-Upload fehlgeschlagen.'
  } finally {
    isUploading.value = false
    input.value = ''
  }
}
function goBack() {
  router.push('/')
}
</script>

<template>
  <section class="detail-page">
    <button class="detail-back" @click="goBack">&larr; Zurück zur Karte</button>

    <div v-if="error && !place" class="detail-error">{{ error }}</div>

    <div v-if="place" class="detail-card">
      <div class="detail-header">
        <div class="detail-avatar">
          <span class="material-symbols-outlined" style="font-variation-settings:'FILL' 1">landscape</span>
        </div>
        <div>
          <h2 class="detail-title">{{ place.name }}</h2>
          <p class="detail-subtitle">Wilderness Explorer</p>
        </div>
      </div>

      <div class="detail-image" v-if="photoUrl">
        <img :src="photoUrl" alt="Foto" />
      </div>

      <div class="detail-tabs">
        <button class="detail-tab" :class="{ 'detail-tab--active': activeTab === 'info' }" @click="activeTab = 'info'">INFO</button>
        <button class="detail-tab" :class="{ 'detail-tab--active': activeTab === 'notes' }" @click="activeTab = 'notes'">NOTES</button>
        <button class="detail-tab" :class="{ 'detail-tab--active': activeTab === 'photos' }" @click="activeTab = 'photos'">PHOTOS</button>
      </div>

      <!-- INFO tab -->
      <div v-if="activeTab === 'info'" class="detail-tab-content">
        <div class="detail-grid">
          <div class="detail-metric">
            <span class="detail-metric-label">Land</span>
            <span class="detail-metric-value">{{ place.country ?? '–' }}</span>
          </div>
          <div class="detail-metric">
            <span class="detail-metric-label">Region</span>
            <span class="detail-metric-value">{{ place.region ?? '–' }}</span>
          </div>
          <div class="detail-metric">
            <span class="detail-metric-label">Latitude</span>
            <span class="detail-metric-value">{{ place.latitude.toFixed(4) }}</span>
          </div>
          <div class="detail-metric">
            <span class="detail-metric-label">Longitude</span>
            <span class="detail-metric-value">{{ place.longitude.toFixed(4) }}</span>
          </div>
        </div>

        <label class="detail-field">
          <span>Status</span>
          <select v-model="status">
            <option value="bucketList">Bucket List</option>
            <option value="visited">Besucht</option>
          </select>
        </label>
      </div>

      <!-- NOTES tab -->
      <div v-if="activeTab === 'notes'" class="detail-tab-content">
        <label class="detail-field">
          <span>Notizen</span>
          <textarea v-model="notes" rows="6" placeholder="Notizen zu diesem Ort …"></textarea>
        </label>
      </div>

      <!-- PHOTOS tab -->
      <div v-if="activeTab === 'photos'" class="detail-tab-content">
        <label class="detail-field">
          <span>Foto {{ photoUrl ? 'wechseln' : 'hochladen' }}</span>
          <input type="file" accept="image/*" @change="handleFileUpload" />
        </label>
        <p v-if="isUploading" class="detail-hint">Wird hochgeladen …</p>
      </div>

      <p v-if="error" class="detail-notice detail-notice--error">{{ error }}</p>
      <p v-if="success" class="detail-notice detail-notice--success">{{ success }}</p>

      <button class="detail-cta" :disabled="isSaving" @click="save">
        <span class="material-symbols-outlined">save</span>
        {{ isSaving ? 'Speichert …' : 'Speichern' }}
      </button>
    </div>
  </section>
</template>

<style scoped>
.detail-page {
  max-width: 640px;
  margin: 0 auto;
  padding: var(--space-md);
  height: 100%;
  overflow-y: auto;
}

.detail-back {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: transparent;
  border: 1px solid var(--color-outline-variant);
  color: var(--color-on-surface-variant);
  border-radius: var(--radius-md);
  padding: 8px 16px;
  cursor: pointer;
  font-weight: 600;
  font-size: 14px;
  margin-bottom: var(--space-sm);
  transition: border-color 0.18s;
}

.detail-back:hover {
  border-color: var(--color-primary);
  color: var(--color-primary);
}

.detail-error {
  padding: 14px 18px;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-error);
  background: var(--color-error-container);
  color: var(--color-on-error-container);
}

.detail-card {
  display: flex;
  flex-direction: column;
  gap: var(--space-sm);
  padding: var(--space-md);
  border-radius: var(--radius-xl);
  border: 1px solid var(--color-outline-variant);
  background: var(--color-surface);
  box-shadow: var(--shadow-md);
}

.detail-header {
  display: flex;
  align-items: center;
  gap: var(--space-sm);
}

.detail-avatar {
  width: 48px;
  height: 48px;
  border-radius: var(--radius-lg);
  background: var(--color-primary-container);
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--color-on-primary-container);
  flex-shrink: 0;
}

.detail-title {
  margin: 0;
  font-family: var(--font-display);
  font-size: 22px;
  font-weight: 700;
  color: var(--color-primary);
  line-height: 1.2;
}

.detail-subtitle {
  margin: 2px 0 0;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--color-on-surface-variant);
}

.detail-image {
  border-radius: var(--radius-lg);
  overflow: hidden;
  border: 1px solid var(--color-outline-variant);
}

.detail-image img {
  display: block;
  width: 100%;
  max-height: 300px;
  object-fit: cover;
}

.detail-tabs {
  display: flex;
  gap: var(--space-xs);
  border-bottom: 1px solid var(--color-outline-variant);
}

.detail-tab {
  flex: 1;
  padding: 10px 8px;
  border: none;
  background: transparent;
  color: var(--color-on-surface-variant);
  cursor: pointer;
  border-bottom: 2px solid transparent;
  transition: color 0.18s, border-color 0.18s;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.05em;
}

.detail-tab:hover {
  color: var(--color-primary);
  background: var(--color-surface-container-low);
  border-radius: var(--radius-md) var(--radius-md) 0 0;
}

.detail-tab--active {
  color: var(--color-primary);
  border-bottom-color: var(--color-primary);
}

.detail-tab-content {
  display: flex;
  flex-direction: column;
  gap: var(--space-sm);
}

.detail-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--space-xs);
}

.detail-metric {
  background: var(--color-surface-container-low);
  padding: 12px;
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-outline-variant);
}

.detail-metric-label {
  display: block;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--color-outline);
}

.detail-metric-value {
  display: block;
  font-family: var(--font-body);
  font-size: 18px;
  font-weight: 600;
  color: var(--color-primary);
  margin-top: 4px;
  letter-spacing: -0.01em;
}

.detail-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.detail-field > span {
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--color-on-surface-variant);
}

.detail-field input,
.detail-field select,
.detail-field textarea {
  width: 100%;
  padding: 10px 12px;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-outline-variant);
  background: var(--color-surface-container-low);
  color: var(--color-on-surface);
  outline: none;
  transition: border-color 0.18s, box-shadow 0.18s;
}

.detail-field input:focus,
.detail-field select:focus,
.detail-field textarea:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px rgba(1, 38, 31, 0.12);
}

.detail-field input[type="file"] {
  padding: 8px;
}

.detail-hint {
  margin: 0;
  color: var(--color-on-surface-variant);
  font-size: 13px;
}

.detail-notice {
  margin: 0;
  padding: 10px 14px;
  border-radius: var(--radius-md);
  font-size: 13px;
}

.detail-notice--error {
  background: var(--color-error-container);
  color: var(--color-on-error-container);
  border: 1px solid var(--color-error);
}

.detail-notice--success {
  background: var(--color-primary-container);
  color: var(--color-on-primary-container);
  border: 1px solid var(--color-primary);
}

.detail-cta {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  width: 100%;
  padding: 14px;
  border: none;
  border-radius: var(--radius-md);
  background: var(--color-secondary);
  color: var(--color-on-secondary);
  font-weight: 700;
  font-size: 15px;
  cursor: pointer;
  transition: opacity 0.18s, transform 0.12s;
}

.detail-cta:hover:not(:disabled) {
  opacity: 0.9;
}

.detail-cta:active:not(:disabled) {
  transform: scale(0.98);
}

.detail-cta:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
