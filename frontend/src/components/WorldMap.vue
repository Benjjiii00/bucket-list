<script setup lang="ts">
import axios from 'axios'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import L from 'leaflet'
import type { Bikepark, TrailRef } from '../services/bikeparkService'

type CountryProperties = Record<string, unknown> & {
  name?: string
  NAME?: string
  admin?: string
  ADMIN?: string
  country?: string
  name_long?: string
  NAME_LONG?: string
}

type CountryFeature = {
  type: 'Feature'
  properties?: CountryProperties
}

type CountryCollection = {
  type: 'FeatureCollection'
  features: CountryFeature[]
}

type PlaceStatus = 'bucketList' | 'visited'

type TravelPlace = {
  id: number
  name: string
  country: string | null
  region: string | null
  latitude: number
  longitude: number
  status: PlaceStatus
  notes: string | null
  photoUrl: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

type PanelMode = 'closed' | 'view' | 'create'
type PanelEntity = 'place' | 'bikepark' | 'trail'

const router = useRouter()
const mapElement = ref<HTMLDivElement | null>(null)
const isMapReady = ref(false)

const places = ref<TravelPlace[]>([])
const bikeparks = ref<Bikepark[]>([])
const trails = ref<TrailRef[]>([])

const selectedPlace = ref<TravelPlace | null>(null)
const selectedBikepark = ref<Bikepark | null>(null)
const selectedTrail = ref<TrailRef | null>(null)
const selectedCountry = ref<string | null>(null)
const draftLocation = ref<{ latitude: number; longitude: number } | null>(null)

const panelMode = ref<PanelMode>('closed')
const panelEntity = ref<PanelEntity>('place')
const activeTab = ref<'info' | 'notes' | 'photos'>('info')
const editingNotes = ref('')
const editingStatus = ref<PlaceStatus>('bucketList')
const isSaving = ref(false)
const isUploading = ref(false)
const apiError = ref<string | null>(null)
const searchQuery = ref('')

const showPlaces = ref(true)
const showBikeparks = ref(true)
const showTrails = ref(true)

const draftName = ref('')
const draftCountry = ref('')
const draftRegion = ref('')
const draftNotes = ref('')
const draftStatus = ref<PlaceStatus>('bucketList')
const draftIsBikepark = ref(false)

let map: L.Map | null = null
let placesLayer: L.LayerGroup | null = null
let bikeparksLayer: L.LayerGroup | null = null
let trailsLayer: L.LayerGroup | null = null
let countriesLayer: L.GeoJSON | null = null
let draftMarker: L.CircleMarker | null = null

const filteredPlaces = computed(() => {
  if (!searchQuery.value.trim()) return places.value
  const q = searchQuery.value.toLowerCase()
  return places.value.filter(p =>
    p.name.toLowerCase().includes(q) ||
    (p.country && p.country.toLowerCase().includes(q)) ||
    (p.region && p.region.toLowerCase().includes(q))
  )
})

const filteredBikeparks = computed(() => {
  if (!searchQuery.value.trim()) return bikeparks.value
  const q = searchQuery.value.toLowerCase()
  return bikeparks.value.filter(p =>
    p.name.toLowerCase().includes(q) ||
    (p.country && p.country.toLowerCase().includes(q)) ||
    (p.region && p.region.toLowerCase().includes(q)) ||
    (p.description && p.description.toLowerCase().includes(q))
  )
})

const filteredTrails = computed(() => {
  if (!searchQuery.value.trim()) return trails.value
  const q = searchQuery.value.toLowerCase()
  return trails.value.filter(t =>
    t.name.toLowerCase().includes(q) ||
    (t.description && t.description.toLowerCase().includes(q)) ||
    (t.trailType && t.trailType.toLowerCase().includes(q))
  )
})

function getStatusColor(s: PlaceStatus): string {
  return s === 'visited' ? '#1a6b3c' : '#c4912a'
}

function getBikeparkColor(difficulty: string | null): string {
  switch (difficulty?.toLowerCase()) {
    case 'easy': return '#4caf50'
    case 'medium': return '#ff9800'
    case 'hard': return '#f44336'
    default: return '#7e57c2'
  }
}

function getTrailColor(difficulty: string | null): string {
  switch (difficulty?.toLowerCase()) {
    case 'easy':
    case 's0':
    case 's1': return '#4caf50'
    case 'medium':
    case 's2':
    case 's3': return '#ff9800'
    case 'hard':
    case 's4':
    case 's5': return '#f44336'
    default: return '#42a5f5'
  }
}

function getCountryName(properties?: CountryProperties): string {
  return (
    properties?.name?.toString() ??
    properties?.ADMIN?.toString() ??
    properties?.admin?.toString() ??
    properties?.NAME?.toString() ??
    properties?.name_long?.toString() ??
    properties?.NAME_LONG?.toString() ??
    properties?.country?.toString() ??
    'Unbekanntes Land'
  )
}

const defaultStyle: L.PathOptions = {
  color: '#717976',
  weight: 0.8,
  opacity: 0.5,
  fillColor: '#e5e6ff',
  fillOpacity: 0.15,
}

const hoverStyle: L.PathOptions = {
  color: '#01261f',
  weight: 1.5,
  opacity: 0.8,
  fillColor: '#1a3c34',
  fillOpacity: 0.25,
}

function refreshLayerStyles() {
  if (!countriesLayer) return
  countriesLayer.eachLayer((layer) => {
    const pathLayer = layer as L.Path & { feature?: CountryFeature }
    const countryName = getCountryName(pathLayer.feature?.properties)
    pathLayer.setStyle(countryName === selectedCountry.value ? hoverStyle : defaultStyle)
  })
}

function refreshPlaceLayer() {
  if (!placesLayer) return
  placesLayer.clearLayers()
  if (!showPlaces.value) return
  for (const place of filteredPlaces.value) {
    const marker = L.circleMarker([place.latitude, place.longitude], {
      radius: 8,
      color: '#ffffff',
      weight: 2,
      fillColor: getStatusColor(place.status),
      fillOpacity: 0.9,
      bubblingMouseEvents: false,
    })
    marker.bindTooltip(place.name, { direction: 'top', offset: [0, -6] })
    marker.on('click', () => openPlacePanel(place))
    marker.addTo(placesLayer)
  }
}

function refreshBikeparkLayer() {
  if (!bikeparksLayer) return
  bikeparksLayer.clearLayers()
  if (!showBikeparks.value) return
  for (const park of filteredBikeparks.value) {
    const marker = L.circleMarker([park.latitude, park.longitude], {
      radius: 10,
      color: '#ffffff',
      weight: 2,
      fillColor: getBikeparkColor(park.difficulty),
      fillOpacity: 0.9,
      bubblingMouseEvents: false,
    })
    marker.bindTooltip(`<b>&#x1F6B4; ${park.name}</b>`, { direction: 'top', offset: [0, -10] })
    marker.on('click', () => openBikeparkPanel(park))
    marker.addTo(bikeparksLayer)
  }
}

function refreshTrailLayer() {
  if (!trailsLayer) return
  trailsLayer.clearLayers()
  if (!showTrails.value) return
  for (const trail of filteredTrails.value) {
    if (!trail.polyline) continue
    try {
      const coords: [number, number][] = JSON.parse(trail.polyline)
      if (coords.length < 2) continue
      const polyline = L.polyline(coords, {
        color: getTrailColor(trail.difficulty),
        weight: 4,
        opacity: 0.8,
      })
      polyline.bindTooltip(`<b>&#x1F6B4; ${trail.name}</b><br/>${trail.length.toFixed(1)} km`)
      polyline.on('click', () => openTrailPanel(trail))
      polyline.addTo(trailsLayer)
    } catch {
      // skip invalid polyline data
    }
  }
}

function refreshDraftMarker() {
  if (!map || !draftLocation.value) return
  if (draftMarker) map.removeLayer(draftMarker)
  draftMarker = L.circleMarker([draftLocation.value.latitude, draftLocation.value.longitude], {
    radius: 10,
    color: '#ffffff',
    weight: 3,
    fillColor: '#ff7645',
    fillOpacity: 0.9,
    bubblingMouseEvents: false,
  }).addTo(map)
  draftMarker.bindTooltip('Neuer Ort', { direction: 'top' }).openTooltip()
}

function removeDraftMarker() {
  if (draftMarker && map) {
    map.removeLayer(draftMarker)
    draftMarker = null
  }
}

function openPlacePanel(place: TravelPlace) {
  removeDraftMarker()
  selectedPlace.value = place
  selectedBikepark.value = null
  selectedTrail.value = null
  editingNotes.value = place.notes ?? ''
  editingStatus.value = place.status
  activeTab.value = 'info'
  draftLocation.value = null
  panelMode.value = 'view'
  panelEntity.value = 'place'
}

function openBikeparkPanel(park: Bikepark) {
  removeDraftMarker()
  selectedBikepark.value = park
  selectedPlace.value = null
  selectedTrail.value = null
  draftLocation.value = null
  panelMode.value = 'view'
  panelEntity.value = 'bikepark'
}

function openTrailPanel(trail: TrailRef) {
  removeDraftMarker()
  selectedTrail.value = trail
  selectedPlace.value = null
  selectedBikepark.value = null
  draftLocation.value = null
  panelMode.value = 'view'
  panelEntity.value = 'trail'
}

function openCreatePlacePanel() {
  selectedPlace.value = null
  selectedBikepark.value = null
  selectedTrail.value = null
  panelMode.value = 'create'
  panelEntity.value = 'place'
  activeTab.value = 'info'
  draftIsBikepark.value = false
}

function closePanel() {
  panelMode.value = 'closed'
  selectedPlace.value = null
  selectedBikepark.value = null
  selectedTrail.value = null
  draftLocation.value = null
  removeDraftMarker()
  apiError.value = null
}

async function loadPlaces() {
  try {
    const response = await axios.get<TravelPlace[]>('/api/places')
    places.value = response.data
    refreshPlaceLayer()
  } catch {
    apiError.value = 'Orte konnten nicht geladen werden.'
  }
}

async function loadBikeparks() {
  try {
    const response = await axios.get<Bikepark[]>('/api/bikeparks')
    bikeparks.value = response.data
    refreshBikeparkLayer()
  } catch {
    apiError.value = 'Bikeparks konnten nicht geladen werden.'
  }
}

async function loadTrails() {
  try {
    const response = await axios.get<TrailRef[]>('/api/trails')
    trails.value = response.data
    refreshTrailLayer()
  } catch {
    apiError.value = 'Trails konnten nicht geladen werden.'
  }
}

async function saveDraftPlace() {
  if (!draftLocation.value || !draftName.value.trim()) return
  isSaving.value = true
  apiError.value = null
  try {
    if (draftIsBikepark.value) {
      const response = await axios.post<Bikepark>('/api/bikeparks', {
        name: draftName.value.trim(),
        country: draftCountry.value.trim() || null,
        region: draftRegion.value.trim() || null,
        latitude: draftLocation.value.latitude,
        longitude: draftLocation.value.longitude,
        description: draftNotes.value.trim() || null,
      })
      bikeparks.value = [...bikeparks.value, response.data].sort((a, b) => a.name.localeCompare(b.name))
      refreshBikeparkLayer()
      openBikeparkPanel(response.data)
    } else {
      const response = await axios.post<TravelPlace>('/api/places', {
        name: draftName.value.trim(),
        country: draftCountry.value.trim() || null,
        region: draftRegion.value.trim() || null,
        latitude: draftLocation.value.latitude,
        longitude: draftLocation.value.longitude,
        status: draftStatus.value,
        notes: draftNotes.value.trim() || null,
      })
      places.value = [...places.value, response.data].sort((a, b) => a.name.localeCompare(b.name))
      refreshPlaceLayer()
      openPlacePanel(response.data)
    }
  } catch {
    apiError.value = draftIsBikepark.value ? 'Bikepark konnte nicht gespeichert werden.' : 'Ort konnte nicht gespeichert werden.'
  } finally {
    isSaving.value = false
  }
}

async function savePlace() {
  if (!selectedPlace.value) return
  isSaving.value = true
  apiError.value = null
  try {
    const response = await axios.put<TravelPlace>(`/api/places/${selectedPlace.value.id}`, {
      name: selectedPlace.value.name,
      country: selectedPlace.value.country,
      region: selectedPlace.value.region,
      latitude: selectedPlace.value.latitude,
      longitude: selectedPlace.value.longitude,
      status: editingStatus.value,
      notes: editingNotes.value.trim() || null,
      photoUrl: selectedPlace.value.photoUrl,
    })
    selectedPlace.value = response.data
    places.value = places.value.map(p => p.id === response.data.id ? response.data : p)
    refreshPlaceLayer()
  } catch {
    apiError.value = 'Speichern fehlgeschlagen.'
  } finally {
    isSaving.value = false
  }
}

async function deletePlace(placeId: number) {
  if (!confirm('Möchtest du diesen Ort wirklich löschen?')) return
  apiError.value = null
  try {
    await axios.delete(`/api/places/${placeId}`)
    places.value = places.value.filter(p => p.id !== placeId)
    if (selectedPlace.value?.id === placeId) closePanel()
    else refreshPlaceLayer()
  } catch {
    apiError.value = 'Ort konnte nicht gelöscht werden.'
  }
}

async function deleteBikepark(parkId: number) {
  if (!confirm('Möchtest du diesen Bikepark wirklich löschen?')) return
  apiError.value = null
  try {
    await axios.delete(`/api/bikeparks/${parkId}`)
    bikeparks.value = bikeparks.value.filter(p => p.id !== parkId)
    if (selectedBikepark.value?.id === parkId) closePanel()
    else refreshBikeparkLayer()
  } catch {
    apiError.value = 'Bikepark konnte nicht gelöscht werden.'
  }
}

async function uploadPhoto(event: Event) {
  const input = event.target as HTMLInputElement
  if (!input.files?.length || !selectedPlace.value) return
  isUploading.value = true
  apiError.value = null
  try {
    const formData = new FormData()
    formData.append('file', input.files[0])
    const response = await axios.post<TravelPlace>(`/api/places/${selectedPlace.value.id}/photo`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    selectedPlace.value = response.data
    places.value = places.value.map(p => p.id === response.data.id ? response.data : p)
    refreshPlaceLayer()
  } catch {
    apiError.value = 'Foto-Upload fehlgeschlagen.'
  } finally {
    isUploading.value = false
    input.value = ''
  }
}

function goToPlaceDetail(placeId: number) {
  router.push(`/places/${placeId}`)
}

function zoomIn() { map?.zoomIn() }
function zoomOut() { map?.zoomOut() }

watch(searchQuery, () => {
  refreshPlaceLayer()
  refreshBikeparkLayer()
  refreshTrailLayer()
})

watch([showPlaces, showBikeparks, showTrails], () => {
  refreshPlaceLayer()
  refreshBikeparkLayer()
  refreshTrailLayer()
})

onMounted(async () => {
  if (!mapElement.value) return

  map = L.map(mapElement.value, {
    zoomControl: false,
    minZoom: 2,
    maxZoom: 19,
    worldCopyJump: true,
  }).setView([18, 0], 2)

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; OpenStreetMap contributors',
    maxZoom: 19,
  }).addTo(map)

  placesLayer = L.layerGroup().addTo(map)
  bikeparksLayer = L.layerGroup().addTo(map)
  trailsLayer = L.layerGroup().addTo(map)

  map.on('click', (event) => {
    const latlng = { latitude: event.latlng.lat, longitude: event.latlng.lng }

    if (panelMode.value === 'create') {
      draftLocation.value = latlng
      draftCountry.value = selectedCountry.value ?? ''
      refreshPlaceLayer()
      refreshBikeparkLayer()
      refreshTrailLayer()
      refreshDraftMarker()
      return
    }

    selectedPlace.value = null
    selectedBikepark.value = null
    selectedTrail.value = null
    panelMode.value = 'closed'
    draftLocation.value = latlng
    draftCountry.value = selectedCountry.value ?? ''
    refreshPlaceLayer()
    refreshBikeparkLayer()
    refreshTrailLayer()
    refreshDraftMarker()
    openCreatePlacePanel()
  })

  try {
    const response = await fetch('/data/countries.geojson')
    if (!response.ok) throw new Error(`GeoJSON Fehler: ${response.status}`)
    const countries = (await response.json()) as CountryCollection
    countriesLayer = L.geoJSON(countries as never, {
      style: defaultStyle,
      onEachFeature: (feature, layer) => {
        const countryFeature = feature as CountryFeature
        layer.on({
          click: () => {
            selectedCountry.value = getCountryName(countryFeature.properties)
            selectedPlace.value = null
            selectedBikepark.value = null
            selectedTrail.value = null
            draftCountry.value = selectedCountry.value ?? ''
            refreshLayerStyles()
          },
          mouseover: () => {
            if (layer instanceof L.Path) {
              layer.setStyle(hoverStyle)
            }
          },
          mouseout: () => {
            refreshLayerStyles()
          },
        })
      },
    }).addTo(map)
    map.fitBounds(countriesLayer.getBounds(), { padding: [24, 24] })
    isMapReady.value = true
  } catch {
    apiError.value = 'GeoJSON konnte nicht geladen werden.'
  }

  await Promise.all([
    loadPlaces(),
    loadBikeparks(),
    loadTrails(),
  ])
})

onBeforeUnmount(() => {
  map?.remove()
  map = null
  countriesLayer = null
  placesLayer = null
  bikeparksLayer = null
  trailsLayer = null
})
</script>

<template>
  <section class="explorer">
    <div ref="mapElement" class="explorer__map"></div>

    <!-- Search -->
    <div class="explorer__search">
      <div class="explorer__search-bar glass-panel">
        <span class="material-symbols-outlined explorer__search-icon">search</span>
        <input v-model="searchQuery" class="explorer__search-input" placeholder="Search waypoints, trails, or parks …" />
        <button class="explorer__search-btn">FIND</button>
      </div>
    </div>

    <!-- Filter toggles -->
    <div class="explorer__filters">
      <button class="explorer__filter-btn" :class="{ 'explorer__filter-btn--active': showPlaces }" @click="showPlaces = !showPlaces">
        <span class="explorer__filter-dot" style="background:#c4912a"></span>
        Places
      </button>
      <button class="explorer__filter-btn" :class="{ 'explorer__filter-btn--active': showBikeparks }" @click="showBikeparks = !showBikeparks">
        <span class="explorer__filter-dot" style="background:#7e57c2"></span>
        Bikeparks
      </button>
      <button class="explorer__filter-btn" :class="{ 'explorer__filter-btn--active': showTrails }" @click="showTrails = !showTrails">
        <span class="explorer__filter-dot" style="background:#42a5f5"></span>
        Trails
      </button>
    </div>

    <!-- Map controls -->
    <div class="explorer__controls">
      <button class="explorer__ctrl" @click="zoomIn">
        <span class="material-symbols-outlined">add</span>
      </button>
      <button class="explorer__ctrl" @click="zoomOut">
        <span class="material-symbols-outlined">remove</span>
      </button>
      <button class="explorer__ctrl explorer__ctrl--primary">
        <span class="material-symbols-outlined" style="font-variation-settings:'FILL' 1">my_location</span>
      </button>
    </div>

    <!-- Side panel -->
    <aside v-if="panelMode !== 'closed'" class="explorer__panel">
      <div class="explorer__panel-header">
        <div class="explorer__panel-brand">
          <div class="explorer__panel-icon">
            <span class="material-symbols-outlined" style="font-variation-settings:'FILL' 1">
              {{ panelEntity === 'bikepark' ? 'pedal_bike' : panelEntity === 'trail' ? 'route' : panelMode === 'create' ? 'add_location' : 'landscape' }}
            </span>
          </div>
          <div>
            <h2 class="explorer__panel-title">
              <template v-if="panelEntity === 'bikepark' && panelMode === 'create'">Neuer Bikepark</template>
              <template v-else-if="panelEntity === 'bikepark' && selectedBikepark">{{ selectedBikepark.name }}</template>
              <template v-else-if="panelEntity === 'trail' && selectedTrail">{{ selectedTrail.name }}</template>
              <template v-else-if="panelMode === 'create'">Neuen Ort</template>
              <template v-else>{{ selectedPlace?.name ?? 'Details' }}</template>
            </h2>
            <p class="explorer__panel-subtitle">
              {{ panelEntity === 'bikepark' ? 'Bikepark' : panelEntity === 'trail' ? 'Trail' : 'Wilderness Explorer' }}
            </p>
          </div>
        </div>
        <button class="explorer__panel-close" @click="closePanel">
          <span class="material-symbols-outlined">close</span>
        </button>
      </div>

      <div class="explorer__panel-body">
        <!-- Place view -->
        <template v-if="panelEntity === 'place' && panelMode === 'view' && selectedPlace">
          <div v-if="selectedPlace.photoUrl" class="explorer__photo">
            <img :src="selectedPlace.photoUrl" alt="Foto" />
          </div>

          <h3 class="explorer__place-name">{{ selectedPlace.name }}</h3>

          <div class="explorer__tabs">
            <button class="explorer__tab" :class="{ 'explorer__tab--active': activeTab === 'info' }" @click="activeTab = 'info'">INFO</button>
            <button class="explorer__tab" :class="{ 'explorer__tab--active': activeTab === 'notes' }" @click="activeTab = 'notes'">NOTES</button>
            <button class="explorer__tab" :class="{ 'explorer__tab--active': activeTab === 'photos' }" @click="activeTab = 'photos'">PHOTOS</button>
          </div>

          <div v-if="activeTab === 'info'" class="explorer__tab-content">
            <div class="explorer__grid">
              <div class="explorer__metric">
                <span class="explorer__metric-label">Land</span>
                <span class="explorer__metric-value">{{ selectedPlace.country ?? '–' }}</span>
              </div>
              <div class="explorer__metric">
                <span class="explorer__metric-label">Region</span>
                <span class="explorer__metric-value">{{ selectedPlace.region ?? '–' }}</span>
              </div>
              <div class="explorer__metric">
                <span class="explorer__metric-label">Latitude</span>
                <span class="explorer__metric-value">{{ selectedPlace.latitude.toFixed(4) }}</span>
              </div>
              <div class="explorer__metric">
                <span class="explorer__metric-label">Longitude</span>
                <span class="explorer__metric-value">{{ selectedPlace.longitude.toFixed(4) }}</span>
              </div>
            </div>

            <label class="explorer__field">
              <span>Status</span>
              <select v-model="editingStatus">
                <option value="bucketList">Bucket List</option>
                <option value="visited">Besucht</option>
              </select>
            </label>
          </div>

          <div v-if="activeTab === 'notes'" class="explorer__tab-content">
            <label class="explorer__field">
              <span>Notizen</span>
              <textarea v-model="editingNotes" rows="6" placeholder="Notizen zu diesem Ort …"></textarea>
            </label>

            <div v-if="selectedPlace.notes" class="explorer__note-card">
              <p class="explorer__note-text">{{ selectedPlace.notes }}</p>
              <span class="explorer__note-date">Logged: {{ new Date(selectedPlace.updatedAtUtc).toLocaleDateString() }}</span>
            </div>
          </div>

          <div v-if="activeTab === 'photos'" class="explorer__tab-content">
            <label class="explorer__field">
              <span>Foto {{ selectedPlace.photoUrl ? 'wechseln' : 'hochladen' }}</span>
              <input type="file" accept="image/*" @change="uploadPhoto" />
            </label>
            <p v-if="isUploading" class="explorer__hint">Wird hochgeladen …</p>
          </div>
        </template>

        <!-- Bikepark view -->
        <template v-if="panelEntity === 'bikepark' && panelMode === 'view' && selectedBikepark">
          <div v-if="selectedBikepark.photoUrl" class="explorer__photo">
            <img :src="selectedBikepark.photoUrl" alt="Bikepark" />
          </div>

          <h3 class="explorer__place-name">{{ selectedBikepark.name }}</h3>

          <div class="explorer__grid">
            <div class="explorer__metric">
              <span class="explorer__metric-label">Land</span>
              <span class="explorer__metric-value">{{ selectedBikepark.country ?? '–' }}</span>
            </div>
            <div class="explorer__metric">
              <span class="explorer__metric-label">Region</span>
              <span class="explorer__metric-value">{{ selectedBikepark.region ?? '–' }}</span>
            </div>
            <div class="explorer__metric">
              <span class="explorer__metric-label">Schwierigkeit</span>
              <span class="explorer__metric-value">{{ selectedBikepark.difficulty ?? '–' }}</span>
            </div>
            <div class="explorer__metric">
              <span class="explorer__metric-label">Trails</span>
              <span class="explorer__metric-value">{{ selectedBikepark.trails.length }}</span>
            </div>
          </div>

          <p v-if="selectedBikepark.description" class="explorer__description">{{ selectedBikepark.description }}</p>

          <div v-if="selectedBikepark.trails.length" class="explorer__trail-list">
            <h4 class="explorer__section-title">Trails</h4>
            <div v-for="trail in selectedBikepark.trails" :key="trail.id" class="explorer__trail-item" @click="openTrailPanel(trail)">
              <div class="explorer__trail-info">
                <span class="explorer__trail-name">&#x1F6B4; {{ trail.name }}</span>
                <span class="explorer__trail-meta">{{ trail.length.toFixed(1) }} km · {{ trail.elevationGain }} hm</span>
              </div>
              <span v-if="trail.difficulty" class="explorer__trail-diff" :style="{ background: getTrailColor(trail.difficulty) }">{{ trail.difficulty }}</span>
            </div>
          </div>

          <label class="explorer__field">
            <span>Website</span>
            <a v-if="selectedBikepark.website" :href="selectedBikepark.website" target="_blank" class="explorer__link">{{ selectedBikepark.website }}</a>
            <span v-else class="explorer__metric-value">–</span>
          </label>
        </template>

        <!-- Trail view -->
        <template v-if="panelEntity === 'trail' && panelMode === 'view' && selectedTrail">
          <h3 class="explorer__place-name">{{ selectedTrail.name }}</h3>

          <div class="explorer__grid">
            <div class="explorer__metric">
              <span class="explorer__metric-label">Länge</span>
              <span class="explorer__metric-value">{{ selectedTrail.length.toFixed(1) }} km</span>
            </div>
            <div class="explorer__metric">
              <span class="explorer__metric-label">Höhenmeter</span>
              <span class="explorer__metric-value">{{ selectedTrail.elevationGain }} m</span>
            </div>
            <div class="explorer__metric">
              <span class="explorer__metric-label">Schwierigkeit</span>
              <span class="explorer__metric-value">{{ selectedTrail.difficulty ?? '–' }}</span>
            </div>
            <div class="explorer__metric">
              <span class="explorer__metric-label">Typ</span>
              <span class="explorer__metric-value">{{ selectedTrail.trailType ?? '–' }}</span>
            </div>
          </div>

          <p v-if="selectedTrail.description" class="explorer__description">{{ selectedTrail.description }}</p>
        </template>

        <!-- Place create form -->
        <template v-if="panelEntity === 'place' && panelMode === 'create'">
          <p v-if="draftLocation" class="explorer__coords-hint">
            Koordinaten: {{ draftLocation.latitude.toFixed(4) }}, {{ draftLocation.longitude.toFixed(4) }}
          </p>
          <p v-else class="explorer__coords-hint">Klicke auf die Karte, um einen Ort zu setzen.</p>

          <label class="explorer__field">
            <span>Name</span>
            <input v-model="draftName" type="text" placeholder="z. B. Finale Ligure" />
          </label>

          <label class="explorer__field">
            <span>Land</span>
            <input v-model="draftCountry" type="text" placeholder="z. B. Italien" />
          </label>

          <label class="explorer__field">
            <span>Region</span>
            <input v-model="draftRegion" type="text" placeholder="z. B. Ligurien" />
          </label>

          <label class="explorer__field">
            <span>Status</span>
            <select v-model="draftStatus">
              <option value="bucketList">Bucket List</option>
              <option value="visited">Besucht</option>
            </select>
          </label>

          <label class="explorer__field">
            <span>Notiz</span>
            <textarea v-model="draftNotes" rows="3" placeholder="Kurze Notiz …"></textarea>
          </label>

          <label class="explorer__toggle">
            <input type="checkbox" v-model="draftIsBikepark" />
            <span>Ist dieser neue Ort ein Bikepark?</span>
          </label>
        </template>

        <p v-if="apiError" class="explorer__error">{{ apiError }}</p>
      </div>

      <div class="explorer__panel-footer">
        <!-- Place view footer -->
        <template v-if="panelEntity === 'place' && panelMode === 'view' && selectedPlace">
          <button class="explorer__btn explorer__btn--primary" :disabled="isSaving" @click="savePlace">
            <span class="material-symbols-outlined">save</span>
            {{ isSaving ? 'Speichert …' : 'Speichern' }}
          </button>
          <button class="explorer__btn explorer__btn--secondary" @click="goToPlaceDetail(selectedPlace.id)">
            <span class="material-symbols-outlined">open_in_new</span>
            Vollansicht
          </button>
          <button class="explorer__btn explorer__btn--danger" @click="deletePlace(selectedPlace.id)">
            <span class="material-symbols-outlined">delete</span>
            Löschen
          </button>
        </template>

        <!-- Bikepark view footer -->
        <template v-if="panelEntity === 'bikepark' && panelMode === 'view' && selectedBikepark">
          <button class="explorer__btn explorer__btn--danger" @click="deleteBikepark(selectedBikepark.id)">
            <span class="material-symbols-outlined">delete</span>
            Löschen
          </button>
        </template>

        <!-- Place create footer -->
        <template v-if="panelEntity === 'place' && panelMode === 'create'">
          <button class="explorer__btn explorer__btn--primary" :disabled="!draftLocation || !draftName.trim() || isSaving" @click="saveDraftPlace">
            <span class="material-symbols-outlined">add_location</span>
            {{ isSaving ? 'Speichert …' : draftIsBikepark ? 'Bikepark speichern' : 'Ort speichern' }}
          </button>
        </template>

      </div>
    </aside>

    <!-- Overlay -->
    <div v-if="panelMode !== 'closed'" class="explorer__overlay" @click="closePanel"></div>
  </section>
</template>

<style scoped>
.explorer {
  position: relative;
  width: 100%;
  height: 100%;
  overflow: hidden;
}

.explorer__map {
  width: 100%;
  height: 100%;
}

.explorer__search {
  position: absolute;
  top: 16px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 1000;
  width: 100%;
  max-width: 480px;
  padding: 0 16px;
  pointer-events: none;
}

.explorer__search-bar {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 6px 6px 6px 14px;
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-outline-variant);
  box-shadow: var(--shadow-md);
  pointer-events: auto;
}

.explorer__search-icon {
  color: var(--color-outline);
  font-size: 20px;
}

.explorer__search-input {
  flex: 1;
  border: none;
  background: transparent;
  color: var(--color-on-surface);
  outline: none;
  font-size: 14px;
}

.explorer__search-input::placeholder {
  color: var(--color-outline);
}

.explorer__search-btn {
  padding: 8px 16px;
  border-radius: var(--radius-md);
  background: var(--color-secondary);
  color: var(--color-on-secondary);
  border: none;
  font-weight: 700;
  font-size: 13px;
  cursor: pointer;
  pointer-events: auto;
  transition: opacity 0.18s;
}

.explorer__search-btn:hover {
  opacity: 0.9;
}

.explorer__filters {
  position: absolute;
  top: 72px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 1000;
  display: flex;
  gap: 8px;
}

.explorer__filter-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  border-radius: var(--radius-full);
  border: 1px solid var(--color-outline-variant);
  background: rgba(251, 248, 255, 0.85);
  backdrop-filter: blur(12px);
  color: var(--color-on-surface-variant);
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.18s, border-color 0.18s;
  box-shadow: var(--shadow-sm);
}

.explorer__filter-btn:hover {
  border-color: var(--color-primary);
}

.explorer__filter-btn--active {
  background: var(--color-primary-container);
  color: var(--color-on-primary-container);
  border-color: var(--color-primary);
}

.explorer__filter-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
}

.explorer__toggle {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border-radius: var(--radius-md);
  background: var(--color-surface-container-low);
  border: 1px solid var(--color-outline-variant);
  cursor: pointer;
  transition: border-color 0.18s, background 0.18s;
}

.explorer__toggle:hover {
  border-color: var(--color-primary);
}

.explorer__toggle:has(input:checked) {
  border-color: var(--color-secondary);
  background: var(--color-secondary-container);
}

.explorer__toggle input {
  width: 18px;
  height: 18px;
  accent-color: var(--color-secondary);
  cursor: pointer;
  flex-shrink: 0;
}

.explorer__toggle span {
  font-size: 14px;
  font-weight: 600;
  color: var(--color-on-surface);
}

.explorer__controls {
  position: absolute;
  bottom: 32px;
  right: 24px;
  z-index: 1000;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.explorer__ctrl {
  width: 44px;
  height: 44px;
  border-radius: var(--radius-full);
  background: rgba(251, 248, 255, 0.85);
  backdrop-filter: blur(12px);
  border: 1px solid var(--color-outline-variant);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: var(--color-primary);
  box-shadow: var(--shadow-md);
  transition: background 0.18s;
}

.explorer__ctrl:hover {
  background: var(--color-surface-container-high);
}

.explorer__ctrl--primary {
  background: var(--color-primary);
  color: var(--color-on-primary);
  border-color: var(--color-primary);
}

.explorer__ctrl--primary:hover {
  opacity: 0.9;
}

.explorer__panel {
  position: absolute;
  top: 0;
  left: 0;
  z-index: 2000;
  width: 380px;
  max-width: 90vw;
  height: 100%;
  background: var(--color-surface);
  box-shadow: var(--shadow-xl);
  display: flex;
  flex-direction: column;
  animation: slideIn 0.25s ease-out;
}

@keyframes slideIn {
  from { transform: translateX(-100%); }
  to { transform: translateX(0); }
}

.explorer__panel-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: var(--space-md);
  border-bottom: 1px solid var(--color-outline-variant);
  flex-shrink: 0;
}

.explorer__panel-brand {
  display: flex;
  align-items: center;
  gap: 12px;
}

.explorer__panel-icon {
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

.explorer__panel-title {
  margin: 0;
  font-family: var(--font-display);
  font-size: 18px;
  font-weight: 700;
  color: var(--color-primary);
  line-height: 1.2;
}

.explorer__panel-subtitle {
  margin: 2px 0 0;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--color-on-surface-variant);
}

.explorer__panel-close {
  width: 36px;
  height: 36px;
  border-radius: var(--radius-full);
  border: none;
  background: transparent;
  color: var(--color-outline);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: color 0.18s;
  flex-shrink: 0;
}

.explorer__panel-close:hover {
  color: var(--color-primary);
}

.explorer__panel-body {
  flex: 1;
  overflow-y: auto;
  padding: var(--space-md);
  display: flex;
  flex-direction: column;
  gap: var(--space-sm);
}

.explorer__panel-footer {
  padding: var(--space-md);
  border-top: 1px solid var(--color-outline-variant);
  display: flex;
  flex-direction: column;
  gap: 8px;
  flex-shrink: 0;
}

.explorer__photo {
  border-radius: var(--radius-lg);
  overflow: hidden;
  border: 1px solid var(--color-outline-variant);
}

.explorer__photo img {
  display: block;
  width: 100%;
  max-height: 200px;
  object-fit: cover;
}

.explorer__place-name {
  margin: 0;
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 700;
  color: var(--color-primary);
}

.explorer__tabs {
  display: flex;
  gap: var(--space-xs);
  border-bottom: 1px solid var(--color-outline-variant);
}

.explorer__tab {
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

.explorer__tab:hover {
  color: var(--color-primary);
  background: var(--color-surface-container-low);
  border-radius: var(--radius-md) var(--radius-md) 0 0;
}

.explorer__tab--active {
  color: var(--color-primary);
  border-bottom-color: var(--color-primary);
}

.explorer__tab-content {
  display: flex;
  flex-direction: column;
  gap: var(--space-sm);
}

.explorer__grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--space-xs);
}

.explorer__metric {
  background: var(--color-surface-container-low);
  padding: 12px;
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-outline-variant);
}

.explorer__metric-label {
  display: block;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--color-outline);
}

.explorer__metric-value {
  display: block;
  font-size: 18px;
  font-weight: 600;
  color: var(--color-primary);
  margin-top: 4px;
  letter-spacing: -0.01em;
}

.explorer__field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.explorer__field > span {
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--color-on-surface-variant);
}

.explorer__field input,
.explorer__field select,
.explorer__field textarea {
  width: 100%;
  padding: 10px 12px;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-outline-variant);
  background: var(--color-surface-container-low);
  color: var(--color-on-surface);
  outline: none;
  transition: border-color 0.18s, box-shadow 0.18s;
  font-size: 14px;
}

.explorer__field input:focus,
.explorer__field select:focus,
.explorer__field textarea:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px rgba(1, 38, 31, 0.12);
}

.explorer__field input[type="file"] {
  padding: 8px;
}

.explorer__note-card {
  background: var(--color-surface-container-highest);
  padding: 16px;
  border-radius: var(--radius-lg);
  border-left: 4px solid var(--color-secondary);
}

.explorer__note-text {
  margin: 0;
  font-style: italic;
  color: var(--color-on-surface);
  font-size: 14px;
  line-height: 1.6;
}

.explorer__note-date {
  display: block;
  margin-top: 8px;
  font-size: 11px;
  color: var(--color-outline);
}

.explorer__coords-hint {
  margin: 0;
  padding: 10px 12px;
  border-radius: var(--radius-md);
  background: var(--color-primary-container);
  color: var(--color-on-primary-container);
  font-size: 13px;
  font-weight: 600;
}

.explorer__error {
  margin: 0;
  padding: 10px 14px;
  border-radius: var(--radius-md);
  background: var(--color-error-container);
  color: var(--color-on-error-container);
  border: 1px solid var(--color-error);
  font-size: 13px;
}

.explorer__hint {
  margin: 0;
  color: var(--color-on-surface-variant);
  font-size: 13px;
}

.explorer__description {
  margin: 0;
  color: var(--color-on-surface);
  font-size: 14px;
  line-height: 1.6;
}

.explorer__link {
  color: var(--color-primary);
  font-size: 14px;
  word-break: break-all;
}

.explorer__section-title {
  margin: 0 0 4px;
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--color-on-surface-variant);
}

.explorer__trail-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.explorer__trail-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 12px;
  border-radius: var(--radius-md);
  background: var(--color-surface-container-low);
  border: 1px solid var(--color-outline-variant);
  cursor: pointer;
  transition: border-color 0.18s;
}

.explorer__trail-item:hover {
  border-color: var(--color-primary);
}

.explorer__trail-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.explorer__trail-name {
  font-weight: 600;
  font-size: 14px;
  color: var(--color-on-surface);
}

.explorer__trail-meta {
  font-size: 12px;
  color: var(--color-outline);
}

.explorer__trail-diff {
  padding: 2px 8px;
  border-radius: var(--radius-full);
  color: white;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  flex-shrink: 0;
}

.explorer__btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  width: 100%;
  padding: 12px;
  border-radius: var(--radius-md);
  font-weight: 700;
  font-size: 14px;
  cursor: pointer;
  border: none;
  transition: opacity 0.18s, transform 0.12s;
}

.explorer__btn:active:not(:disabled) {
  transform: scale(0.98);
}

.explorer__btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.explorer__btn--primary {
  background: var(--color-secondary);
  color: var(--color-on-secondary);
}

.explorer__btn--primary:hover:not(:disabled) {
  opacity: 0.9;
}

.explorer__btn--secondary {
  background: var(--color-primary);
  color: var(--color-on-primary);
}

.explorer__btn--secondary:hover:not(:disabled) {
  opacity: 0.9;
}

.explorer__btn--danger {
  background: transparent;
  color: var(--color-error);
  border: 1px solid var(--color-error);
}

.explorer__btn--danger:hover:not(:disabled) {
  background: var(--color-error-container);
}

.explorer__overlay {
  position: absolute;
  inset: 0;
  z-index: 1500;
  background: rgba(0, 0, 0, 0.2);
}

@media (max-width: 640px) {
  .explorer__panel {
    width: 100%;
    max-width: 100%;
  }

  .explorer__controls {
    bottom: 16px;
    right: 16px;
  }

  .explorer__search {
    top: 12px;
    max-width: 100%;
  }

  .explorer__filters {
    top: 68px;
  }

}
</style>
