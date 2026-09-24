<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import placesService, { type TravelPlace } from '../services/placesService'
import bikeparkService, { type Bikepark } from '../services/bikeparkService'
import trailService, { type Trail } from '../services/trailService'

const router = useRouter()

const places = ref<TravelPlace[]>([])
const bikeparks = ref<Bikepark[]>([])
const trails = ref<Trail[]>([])
const isLoading = ref(true)
const error = ref<string | null>(null)
const searchFilter = ref('')
const statusFilter = ref<'all' | 'visited' | 'bucketList'>('all')
const countryFilter = ref<string>('all')
const exportCopied = ref(false)

onMounted(async () => {
  isLoading.value = true
  error.value = null
  try {
    const [pData, bData, tData] = await Promise.all([
      placesService.getAll(),
      bikeparkService.getAll(),
      trailService.getAll(),
    ])
    places.value = pData
    bikeparks.value = bData
    trails.value = tData
  } catch {
    error.value = 'Fehler beim Laden der Statistiken.'
  } finally {
    isLoading.value = false
  }
})

// Metrics
const totalPlacesCount = computed(() => places.value.length)
const visitedPlaces = computed(() => places.value.filter(p => p.status === 'visited'))
const visitedPlacesCount = computed(() => visitedPlaces.value.length)
const bucketListPlaces = computed(() => places.value.filter(p => p.status === 'bucketList'))
const bucketListPlacesCount = computed(() => bucketListPlaces.value.length)

const visitedPercentage = computed(() => {
  if (totalPlacesCount.value === 0) return 0
  return Math.round((visitedPlacesCount.value / totalPlacesCount.value) * 100)
})

// Visited countries
const visitedCountries = computed(() => {
  const set = new Set<string>()
  for (const p of visitedPlaces.value) {
    if (p.country && p.country.trim()) set.add(p.country.trim())
  }
  return Array.from(set)
})

const allCountriesList = computed(() => {
  const set = new Set<string>()
  for (const p of places.value) {
    if (p.country && p.country.trim()) set.add(p.country.trim())
  }
  for (const b of bikeparks.value) {
    if (b.country && b.country.trim()) set.add(b.country.trim())
  }
  return Array.from(set).sort()
})

const worldCountriesTotal = 195
const worldPercentage = computed(() => {
  return ((visitedCountries.value.length / worldCountriesTotal) * 100).toFixed(1)
})

// Bikeparks & Trails
const totalBikeparksCount = computed(() => bikeparks.value.length)
const totalTrailsCount = computed(() => trails.value.length)
const totalTrailLength = computed(() => trails.value.reduce((acc, t) => acc + (t.length || 0), 0))
const totalElevationGain = computed(() => trails.value.reduce((acc, t) => acc + (t.elevationGain || 0), 0))

const ratedTrails = computed(() => trails.value.filter(t => t.rating && t.rating > 0))
const averageTrailRating = computed(() => {
  if (ratedTrails.value.length === 0) return 0
  const sum = ratedTrails.value.reduce((acc, t) => acc + (t.rating || 0), 0)
  return (sum / ratedTrails.value.length).toFixed(1)
})

// Country breakdown
interface CountryStat {
  country: string
  totalPlaces: number
  visitedCount: number
  bucketCount: number
}

const countryStats = computed<CountryStat[]>(() => {
  const map = new Map<string, { total: number; visited: number; bucket: number }>()

  for (const p of places.value) {
    const c = p.country?.trim() || 'Nicht angegeben'
    const entry = map.get(c) || { total: 0, visited: 0, bucket: 0 }
    entry.total++
    if (p.status === 'visited') entry.visited++
    else entry.bucket++
    map.set(c, entry)
  }

  return Array.from(map.entries()).map(([country, stats]) => ({
    country,
    totalPlaces: stats.total,
    visitedCount: stats.visited,
    bucketCount: stats.bucket,
  })).sort((a, b) => b.visitedCount - a.visitedCount || b.totalPlaces - a.totalPlaces)
})

// Filtered places list
const filteredPlacesList = computed(() => {
  return places.value.filter(p => {
    if (statusFilter.value !== 'all' && p.status !== statusFilter.value) return false
    if (countryFilter.value !== 'all' && p.country?.trim() !== countryFilter.value) return false
    if (searchFilter.value.trim()) {
      const q = searchFilter.value.toLowerCase()
      const matchesName = p.name.toLowerCase().includes(q)
      const matchesCountry = p.country?.toLowerCase().includes(q)
      const matchesRegion = p.region?.toLowerCase().includes(q)
      const matchesNotes = p.notes?.toLowerCase().includes(q)
      if (!matchesName && !matchesCountry && !matchesRegion && !matchesNotes) return false
    }
    return true
  })
})

// Difficulty distribution for trails
const trailDifficultyBreakdown = computed(() => {
  const easy = trails.value.filter(t => ['easy', 's0', 's1'].includes((t.difficulty || '').toLowerCase())).length
  const medium = trails.value.filter(t => ['medium', 's2', 's3'].includes((t.difficulty || '').toLowerCase())).length
  const hard = trails.value.filter(t => ['hard', 's4', 's5'].includes((t.difficulty || '').toLowerCase())).length
  const other = trails.value.length - (easy + medium + hard)
  return { easy, medium, hard, other }
})

function jumpToMap(entity: 'place' | 'bikepark', id: number) {
  router.push({ path: '/', query: { [entity === 'place' ? 'placeId' : 'bikeparkId']: id.toString() } })
}

function exportData() {
  const exportPayload = {
    exportedAt: new Date().toISOString(),
    stats: {
      totalPlaces: totalPlacesCount.value,
      visitedPlaces: visitedPlacesCount.value,
      visitedCountries: visitedCountries.value,
      bikeparks: totalBikeparksCount.value,
      trails: totalTrailsCount.value,
      totalTrailKm: totalTrailLength.value.toFixed(1),
      totalElevationGain: totalElevationGain.value,
    },
    places: places.value,
    bikeparks: bikeparks.value,
    trails: trails.value,
  }

  const blob = new Blob([JSON.stringify(exportPayload, null, 2)], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `bucket-list-export-${new Date().toISOString().slice(0, 10)}.json`
  a.click()
  URL.revokeObjectURL(url)
}

function copySummary() {
  const summary = `🌍 Meine Bucket List Statistiken:
✨ ${visitedCountries.value.length} Länder bereist (${worldPercentage.value}% der Welt)
📍 ${visitedPlacesCount.value} / ${totalPlacesCount.value} Orte besucht
🚲 ${totalBikeparksCount.value} Bikeparks getrackt
🚵 ${totalTrailsCount.value} Trails (${totalTrailLength.value.toFixed(1)} km, ${totalElevationGain.value} hm)
⭐ Durchschnittliche Trail-Bewertung: ${averageTrailRating.value} / 5.0`

  navigator.clipboard.writeText(summary).then(() => {
    exportCopied.value = true
    setTimeout(() => { exportCopied.value = false }, 3000)
  })
}
</script>

<template>
  <div class="stats-page">
    <div class="stats-container">
      <!-- Header -->
      <div class="stats-header">
        <div>
          <h1 class="stats-title">📊 Reise- & Trail-Statistiken</h1>
          <p class="stats-subtitle">Dein persönlicher Fortschritt auf der Weltkarte & Trails</p>
        </div>
        <div class="stats-actions">
          <button class="stats-btn stats-btn--outline" @click="copySummary">
            <span class="material-symbols-outlined">{{ exportCopied ? 'check' : 'content_copy' }}</span>
            {{ exportCopied ? 'Kopiert!' : 'Zusammenfassung' }}
          </button>
          <button class="stats-btn stats-btn--primary" @click="exportData">
            <span class="material-symbols-outlined">download</span>
            JSON Export
          </button>
        </div>
      </div>

      <div v-if="isLoading" class="stats-loading">
        <div class="stats-spinner"></div>
        <p>Statistiken werden geladen …</p>
      </div>

      <div v-else-if="error" class="stats-error">
        <p>{{ error }}</p>
      </div>

      <template v-else>
        <!-- KPI Cards Grid -->
        <div class="kpi-grid">
          <!-- Visited Countries -->
          <div class="kpi-card">
            <div class="kpi-icon kpi-icon--emerald">
              <span class="material-symbols-outlined">public</span>
            </div>
            <div class="kpi-content">
              <span class="kpi-label">Bereiste Länder</span>
              <div class="kpi-value-row">
                <span class="kpi-value">{{ visitedCountries.length }}</span>
                <span class="kpi-subtext">/ {{ worldCountriesTotal }} weltweit ({{ worldPercentage }}%)</span>
              </div>
              <div class="progress-bar">
                <div class="progress-fill progress-fill--emerald" :style="{ width: `${Math.min(parseFloat(worldPercentage) * 5, 100)}%` }"></div>
              </div>
            </div>
          </div>

          <!-- Total Places & Status -->
          <div class="kpi-card">
            <div class="kpi-icon kpi-icon--amber">
              <span class="material-symbols-outlined">location_on</span>
            </div>
            <div class="kpi-content">
              <span class="kpi-label">Orte Status</span>
              <div class="kpi-value-row">
                <span class="kpi-value">{{ visitedPlacesCount }}</span>
                <span class="kpi-subtext">besucht · {{ bucketListPlacesCount }} auf Bucket List</span>
              </div>
              <div class="progress-bar">
                <div class="progress-fill progress-fill--visited" :style="{ width: `${visitedPercentage}%` }"></div>
              </div>
            </div>
          </div>

          <!-- Bikeparks -->
          <div class="kpi-card">
            <div class="kpi-icon kpi-icon--purple">
              <span class="material-symbols-outlined">pedal_bike</span>
            </div>
            <div class="kpi-content">
              <span class="kpi-label">Bikeparks</span>
              <div class="kpi-value-row">
                <span class="kpi-value">{{ totalBikeparksCount }}</span>
                <span class="kpi-subtext">Parks in der Datenbank</span>
              </div>
              <div class="kpi-tags">
                <span class="kpi-tag">{{ totalTrailsCount }} Trails insgesamt</span>
              </div>
            </div>
          </div>

          <!-- Trail Meters & Rating -->
          <div class="kpi-card">
            <div class="kpi-icon kpi-icon--blue">
              <span class="material-symbols-outlined">trending_up</span>
            </div>
            <div class="kpi-content">
              <span class="kpi-label">Trail Distanz & Rating</span>
              <div class="kpi-value-row">
                <span class="kpi-value">{{ totalTrailLength.toFixed(1) }} <small>km</small></span>
                <span class="kpi-subtext">{{ totalElevationGain.toLocaleString() }} hm · ⭐ {{ averageTrailRating }}</span>
              </div>
              <div class="kpi-tags">
                <span class="kpi-tag">{{ ratedTrails.length }} bewertete Trails</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Two Column Layout: Countries & Trail Difficulty -->
        <div class="dashboard-grid">
          <!-- Countries Breakdown Card -->
          <div class="card">
            <div class="card-header">
              <h2 class="card-title">
                <span class="material-symbols-outlined">flag</span>
                Länder-Übersicht
              </h2>
              <span class="card-badge">{{ countryStats.length }} Länder</span>
            </div>
            <div class="card-body">
              <div v-if="countryStats.length === 0" class="empty-state">
                Noch keine Orte mit Länderangabe vorhanden.
              </div>
              <div v-else class="country-list">
                <div v-for="stat in countryStats" :key="stat.country" class="country-item">
                  <div class="country-info">
                    <span class="country-name">{{ stat.country }}</span>
                    <span class="country-meta">
                      {{ stat.visitedCount }} besucht · {{ stat.bucketCount }} Bucket List
                    </span>
                  </div>
                  <div class="country-progress-wrapper">
                    <div class="country-bar">
                      <div
                        class="country-bar-visited"
                        :style="{ width: `${(stat.visitedCount / stat.totalPlaces) * 100}%` }"
                        :title="`${stat.visitedCount} besucht`"
                      ></div>
                    </div>
                    <span class="country-count">{{ stat.totalPlaces }} {{ stat.totalPlaces === 1 ? 'Ort' : 'Orte' }}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Trails & Bikepark Insights Card -->
          <div class="card">
            <div class="card-header">
              <h2 class="card-title">
                <span class="material-symbols-outlined">route</span>
                Trail Schwierigkeit & Highlights
              </h2>
              <span class="card-badge">{{ totalTrailsCount }} Trails</span>
            </div>
            <div class="card-body">
              <div class="difficulty-section">
                <h3 class="section-subtitle">Schwierigkeits-Verteilung</h3>
                <div class="difficulty-bars">
                  <div class="difficulty-row">
                    <div class="difficulty-meta">
                      <span class="diff-dot diff-dot--easy"></span>
                      <span>Leicht / S0-S1</span>
                    </div>
                    <span class="diff-count">{{ trailDifficultyBreakdown.easy }}</span>
                  </div>
                  <div class="difficulty-row">
                    <div class="difficulty-meta">
                      <span class="diff-dot diff-dot--medium"></span>
                      <span>Mittel / S2-S3</span>
                    </div>
                    <span class="diff-count">{{ trailDifficultyBreakdown.medium }}</span>
                  </div>
                  <div class="difficulty-row">
                    <div class="difficulty-meta">
                      <span class="diff-dot diff-dot--hard"></span>
                      <span>Schwer / S4-S5</span>
                    </div>
                    <span class="diff-count">{{ trailDifficultyBreakdown.hard }}</span>
                  </div>
                </div>
              </div>

              <!-- Top rated trails -->
              <div class="top-trails-section" v-if="ratedTrails.length > 0">
                <h3 class="section-subtitle">⭐ Top bewertete Trails</h3>
                <div class="trail-mini-list">
                  <div v-for="trail in [...ratedTrails].sort((a, b) => (b.rating || 0) - (a.rating || 0)).slice(0, 4)" :key="trail.id" class="trail-mini-item">
                    <div class="trail-mini-name">
                      <span>{{ trail.name }}</span>
                      <small>{{ trail.length.toFixed(1) }} km · {{ trail.elevationGain }} hm</small>
                    </div>
                    <div class="trail-stars">
                      <span v-for="star in 5" :key="star" class="star-icon" :class="{ 'star-icon--filled': (trail.rating || 0) >= star }">★</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Places Detailed Table Card -->
        <div class="card card--full">
          <div class="card-header card-header--wrap">
            <div>
              <h2 class="card-title">
                <span class="material-symbols-outlined">explore</span>
                Alle gespeicherten Orte
              </h2>
              <p class="card-description">Durchsuche und filtere alle Orte deiner Bucket List</p>
            </div>
            
            <!-- Filters & Search Toolbar -->
            <div class="table-toolbar">
              <div class="search-box">
                <span class="material-symbols-outlined search-icon">search</span>
                <input v-model="searchFilter" type="text" placeholder="Ort, Land oder Notiz suchen …" />
                <button v-if="searchFilter" class="clear-btn" @click="searchFilter = ''">×</button>
              </div>

              <select v-model="statusFilter" class="select-box">
                <option value="all">Alle Status</option>
                <option value="visited">Nur Besucht</option>
                <option value="bucketList">Nur Bucket List</option>
              </select>

              <select v-model="countryFilter" class="select-box">
                <option value="all">Alle Länder</option>
                <option v-for="c in allCountriesList" :key="c" :value="c">{{ c }}</option>
              </select>
            </div>
          </div>

          <div class="card-body">
            <div v-if="filteredPlacesList.length === 0" class="empty-state">
              Keine Orte gefunden, die den Kriterien entsprechen.
            </div>
            <div v-else class="table-responsive">
              <table class="places-table">
                <thead>
                  <tr>
                    <th>Ort</th>
                    <th>Land / Region</th>
                    <th>Koordinaten</th>
                    <th>Status</th>
                    <th>Notiz</th>
                    <th>Aktionen</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="place in filteredPlacesList" :key="place.id">
                    <td class="table-cell-name">
                      <div class="place-avatar" v-if="place.photoUrl">
                        <img :src="place.photoUrl" alt="" />
                      </div>
                      <div class="place-avatar-placeholder" v-else>
                        📍
                      </div>
                      <span class="place-title-text">{{ place.name }}</span>
                    </td>
                    <td>
                      <span class="country-pill">{{ place.country || '–' }}</span>
                      <small v-if="place.region" class="region-sub">{{ place.region }}</small>
                    </td>
                    <td class="coords-cell">
                      {{ place.latitude.toFixed(3) }}, {{ place.longitude.toFixed(3) }}
                    </td>
                    <td>
                      <span class="status-badge" :class="place.status === 'visited' ? 'status-badge--visited' : 'status-badge--bucket'">
                        {{ place.status === 'visited' ? '✓ Besucht' : '★ Bucket List' }}
                      </span>
                    </td>
                    <td class="notes-cell" :title="place.notes || ''">
                      {{ place.notes ? (place.notes.length > 50 ? place.notes.slice(0, 50) + '…' : place.notes) : '–' }}
                    </td>
                    <td class="actions-cell">
                      <button class="action-btn" title="Auf Karte ansehen" @click="jumpToMap('place', place.id)">
                        <span class="material-symbols-outlined">map</span>
                      </button>
                      <button class="action-btn" title="Details öffnen" @click="router.push(`/places/${place.id}`)">
                        <span class="material-symbols-outlined">edit</span>
                      </button>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped>
.stats-page {
  width: 100%;
  height: 100%;
  overflow-y: auto;
  background: var(--color-background);
  padding: var(--space-md) var(--space-sm);
}

.stats-container {
  max-width: 1200px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: var(--space-md);
}

.stats-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: var(--space-sm);
}

.stats-title {
  font-family: var(--font-display);
  font-size: 28px;
  font-weight: 700;
  color: var(--color-primary);
  margin: 0;
}

.stats-subtitle {
  color: var(--color-on-surface-variant);
  margin: 4px 0 0 0;
  font-size: 15px;
}

.stats-actions {
  display: flex;
  gap: 10px;
}

.stats-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  border-radius: var(--radius-md);
  font-weight: 600;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.18s;
  border: none;
}

.stats-btn--primary {
  background: var(--color-secondary);
  color: var(--color-on-secondary);
}

.stats-btn--primary:hover {
  opacity: 0.9;
}

.stats-btn--outline {
  background: var(--color-surface);
  border: 1px solid var(--color-outline-variant);
  color: var(--color-on-surface);
}

.stats-btn--outline:hover {
  border-color: var(--color-primary);
  color: var(--color-primary);
}

.stats-loading, .stats-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: var(--space-xl);
  color: var(--color-on-surface-variant);
}

.stats-spinner {
  width: 36px;
  height: 36px;
  border: 3px solid var(--color-outline-variant);
  border-top-color: var(--color-primary);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin-bottom: var(--space-sm);
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* KPI Cards */
.kpi-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  gap: var(--space-sm);
}

.kpi-card {
  display: flex;
  align-items: flex-start;
  gap: 16px;
  background: var(--color-surface);
  border: 1px solid var(--color-outline-variant);
  border-radius: var(--radius-lg);
  padding: 18px;
  box-shadow: var(--shadow-sm);
}

.kpi-icon {
  width: 48px;
  height: 48px;
  border-radius: var(--radius-md);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.kpi-icon span {
  font-size: 26px;
}

.kpi-icon--emerald {
  background: #e8f5e9;
  color: #1b5e20;
}

.kpi-icon--amber {
  background: #fff8e1;
  color: #b78103;
}

.kpi-icon--purple {
  background: #f3e5f5;
  color: #6a1b9a;
}

.kpi-icon--blue {
  background: #e3f2fd;
  color: #1565c0;
}

.kpi-content {
  flex: 1;
  min-width: 0;
}

.kpi-label {
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--color-on-surface-variant);
}

.kpi-value-row {
  display: flex;
  align-items: baseline;
  gap: 8px;
  margin: 4px 0 8px 0;
}

.kpi-value {
  font-family: var(--font-display);
  font-size: 26px;
  font-weight: 700;
  color: var(--color-on-surface);
  line-height: 1;
}

.kpi-value small {
  font-size: 16px;
  font-weight: 600;
}

.kpi-subtext {
  font-size: 12px;
  color: var(--color-on-surface-variant);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.progress-bar {
  width: 100%;
  height: 6px;
  background: var(--color-surface-container-high);
  border-radius: var(--radius-full);
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  border-radius: var(--radius-full);
  transition: width 0.4s ease;
}

.progress-fill--emerald {
  background: #2e7d32;
}

.progress-fill--visited {
  background: var(--color-status-visited);
}

.kpi-tags {
  display: flex;
  gap: 6px;
}

.kpi-tag {
  display: inline-block;
  font-size: 11px;
  font-weight: 600;
  padding: 2px 8px;
  background: var(--color-surface-container);
  border-radius: var(--radius-full);
  color: var(--color-on-surface-variant);
}

/* Dashboard Grid */
.dashboard-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(360px, 1fr));
  gap: var(--space-md);
}

.card {
  background: var(--color-surface);
  border: 1px solid var(--color-outline-variant);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-sm);
  overflow: hidden;
}

.card--full {
  grid-column: 1 / -1;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid var(--color-surface-container-high);
}

.card-header--wrap {
  flex-wrap: wrap;
  gap: 12px;
}

.card-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-family: var(--font-display);
  font-size: 18px;
  font-weight: 700;
  color: var(--color-primary);
  margin: 0;
}

.card-description {
  margin: 4px 0 0 0;
  font-size: 13px;
  color: var(--color-on-surface-variant);
}

.card-badge {
  font-size: 12px;
  font-weight: 600;
  padding: 4px 10px;
  border-radius: var(--radius-full);
  background: var(--color-surface-container);
  color: var(--color-on-surface-variant);
}

.card-body {
  padding: 20px;
}

/* Country List */
.country-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
  max-height: 280px;
  overflow-y: auto;
}

.country-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  padding: 8px 12px;
  border-radius: var(--radius-md);
  background: var(--color-surface-container-low);
}

.country-info {
  display: flex;
  flex-direction: column;
  min-width: 100px;
}

.country-name {
  font-weight: 600;
  font-size: 14px;
  color: var(--color-on-surface);
}

.country-meta {
  font-size: 11px;
  color: var(--color-on-surface-variant);
}

.country-progress-wrapper {
  display: flex;
  align-items: center;
  gap: 12px;
  flex: 1;
  max-width: 200px;
}

.country-bar {
  flex: 1;
  height: 8px;
  background: #c4912a33;
  border-radius: var(--radius-full);
  overflow: hidden;
}

.country-bar-visited {
  height: 100%;
  background: var(--color-status-visited);
  border-radius: var(--radius-full);
}

.country-count {
  font-size: 12px;
  font-weight: 600;
  color: var(--color-on-surface);
  white-space: nowrap;
}

/* Trail Difficulty */
.difficulty-section {
  margin-bottom: 20px;
}

.section-subtitle {
  font-size: 14px;
  font-weight: 700;
  color: var(--color-on-surface);
  margin: 0 0 10px 0;
}

.difficulty-bars {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.difficulty-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  background: var(--color-surface-container-low);
  border-radius: var(--radius-md);
  font-size: 13px;
  font-weight: 600;
}

.difficulty-meta {
  display: flex;
  align-items: center;
  gap: 8px;
}

.diff-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
}

.diff-dot--easy { background: #4caf50; }
.diff-dot--medium { background: #ff9800; }
.diff-dot--hard { background: #f44336; }

.diff-count {
  font-weight: 700;
  background: var(--color-surface);
  padding: 2px 8px;
  border-radius: var(--radius-sm);
}

.top-trails-section {
  border-top: 1px solid var(--color-surface-container-high);
  padding-top: 16px;
}

.trail-mini-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.trail-mini-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  background: var(--color-surface-container-low);
  border-radius: var(--radius-md);
}

.trail-mini-name {
  display: flex;
  flex-direction: column;
}

.trail-mini-name span {
  font-weight: 600;
  font-size: 13px;
}

.trail-mini-name small {
  font-size: 11px;
  color: var(--color-on-surface-variant);
}

.trail-stars {
  display: flex;
  gap: 2px;
}

.star-icon {
  color: var(--color-outline-variant);
  font-size: 14px;
}

.star-icon--filled {
  color: #fbc02d;
}

/* Toolbar & Table */
.table-toolbar {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.search-box {
  display: flex;
  align-items: center;
  gap: 6px;
  background: var(--color-surface-container-low);
  border: 1px solid var(--color-outline-variant);
  border-radius: var(--radius-md);
  padding: 6px 12px;
  min-width: 220px;
}

.search-box input {
  border: none;
  background: transparent;
  outline: none;
  font-size: 13px;
  flex: 1;
}

.search-icon {
  font-size: 18px;
  color: var(--color-outline);
}

.clear-btn {
  background: transparent;
  border: none;
  cursor: pointer;
  font-size: 16px;
  color: var(--color-outline);
}

.select-box {
  padding: 8px 12px;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-outline-variant);
  background: var(--color-surface-container-low);
  font-size: 13px;
  outline: none;
  cursor: pointer;
}

.table-responsive {
  overflow-x: auto;
}

.places-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
  font-size: 13px;
}

.places-table th {
  padding: 10px 14px;
  font-weight: 600;
  color: var(--color-on-surface-variant);
  border-bottom: 2px solid var(--color-surface-container-high);
}

.places-table td {
  padding: 12px 14px;
  border-bottom: 1px solid var(--color-surface-container-high);
  vertical-align: middle;
}

.table-cell-name {
  display: flex;
  align-items: center;
  gap: 10px;
  font-weight: 600;
}

.place-avatar {
  width: 32px;
  height: 32px;
  border-radius: var(--radius-sm);
  overflow: hidden;
  flex-shrink: 0;
}

.place-avatar img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.place-avatar-placeholder {
  width: 32px;
  height: 32px;
  border-radius: var(--radius-sm);
  background: var(--color-surface-container);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  flex-shrink: 0;
}

.country-pill {
  display: inline-block;
  font-weight: 600;
  color: var(--color-on-surface);
}

.region-sub {
  display: block;
  font-size: 11px;
  color: var(--color-on-surface-variant);
}

.coords-cell {
  font-family: monospace;
  font-size: 12px;
  color: var(--color-on-surface-variant);
}

.status-badge {
  display: inline-block;
  padding: 4px 10px;
  border-radius: var(--radius-full);
  font-size: 12px;
  font-weight: 600;
}

.status-badge--visited {
  background: #e8f5e9;
  color: #1b5e20;
}

.status-badge--bucket {
  background: #fff8e1;
  color: #b78103;
}

.notes-cell {
  max-width: 220px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--color-on-surface-variant);
}

.actions-cell {
  white-space: nowrap;
}

.action-btn {
  background: transparent;
  border: 1px solid var(--color-outline-variant);
  border-radius: var(--radius-sm);
  padding: 4px 8px;
  cursor: pointer;
  margin-right: 6px;
  transition: all 0.18s;
  color: var(--color-on-surface-variant);
}

.action-btn:hover {
  border-color: var(--color-primary);
  color: var(--color-primary);
  background: var(--color-surface-container);
}

.action-btn span {
  font-size: 16px;
  vertical-align: middle;
}

.empty-state {
  text-align: center;
  padding: 32px;
  color: var(--color-on-surface-variant);
  font-size: 14px;
}
</style>
