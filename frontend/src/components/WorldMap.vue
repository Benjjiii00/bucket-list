<script setup lang="ts">
import axios from 'axios'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import L from 'leaflet'
import type { Bikepark, TrailRef } from '../services/bikeparkService'
import weatherService, { type WeatherData } from '../services/weatherService'
import geocodingService, { type SearchLocationResult } from '../services/geocodingService'
import trailService from '../services/trailService'

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

const route = useRoute()
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
const activeTab = ref<'info' | 'weather' | 'notes' | 'photos'>('info')
const editingNotes = ref('')
const editingStatus = ref<PlaceStatus>('bucketList')
const isSaving = ref(false)
const isUploading = ref(false)
const isGeocoding = ref(false)
const apiError = ref<string | null>(null)
const successMessage = ref<string | null>(null)

// Weather state
const currentWeather = ref<WeatherData | null>(null)
const isLoadingWeather = ref(false)

// Search & Online Suggestions
const searchQuery = ref('')
const isSearchingOnline = ref(false)
const onlineSearchResults = ref<SearchLocationResult[]>([])
const showSearchDropdown = ref(false)

// Filter states (Sprint 6)
const showPlaces = ref(true)
const showBikeparks = ref(true)
const showTrails = ref(true)
const statusFilter = ref<'all' | 'visited' | 'bucketList'>('all')
const difficultyFilter = ref<'all' | 'easy' | 'medium' | 'hard'>('all')
const selectedCountryFilter = ref<string>('all')
const showFilterDrawer = ref(false)

// Draft Place / Bikepark
const draftName = ref('')
const draftCountry = ref('')
const draftRegion = ref('')
const draftNotes = ref('')
const draftStatus = ref<PlaceStatus>('bucketList')
const draftIsBikepark = ref(false)
const draftDifficulty = ref('Medium')

// Draft Trail in Bikepark
const showAddTrailForm = ref(false)
const newTrailName = ref('')
const newTrailLength = ref<number>(2.5)
const newTrailElevation = ref<number>(250)
const newTrailDifficulty = ref('Medium')
const newTrailType = ref('Flow')
const newTrailDescription = ref('')
const newTrailRating = ref<number>(4)

let map: L.Map | null = null
let placesLayer: L.LayerGroup | null = null
let bikeparksLayer: L.LayerGroup | null = null
let trailsLayer: L.LayerGroup | null = null
let countriesLayer: L.GeoJSON | null = null
let draftMarker: L.CircleMarker | null = null

// Filter logic
const filteredPlaces = computed(() => {
  return places.value.filter(p => {
    if (!showPlaces.value) return false
    if (statusFilter.value !== 'all' && p.status !== statusFilter.value) return false
    if (selectedCountryFilter.value !== 'all' && p.country?.trim() !== selectedCountryFilter.value) return false
    if (!searchQuery.value.trim()) return true
    const q = searchQuery.value.toLowerCase()
    return (
      p.name.toLowerCase().includes(q) ||
      (p.country && p.country.toLowerCase().includes(q)) ||
      (p.region && p.region.toLowerCase().includes(q)) ||
      (p.notes && p.notes.toLowerCase().includes(q))
    )
  })
})

const filteredBikeparks = computed(() => {
  return bikeparks.value.filter(p => {
    if (!showBikeparks.value) return false
    if (selectedCountryFilter.value !== 'all' && p.country?.trim() !== selectedCountryFilter.value) return false
    if (difficultyFilter.value !== 'all' && p.difficulty?.toLowerCase() !== difficultyFilter.value.toLowerCase()) return false
    if (!searchQuery.value.trim()) return true
    const q = searchQuery.value.toLowerCase()
    return (
      p.name.toLowerCase().includes(q) ||
      (p.country && p.country.toLowerCase().includes(q)) ||
      (p.region && p.region.toLowerCase().includes(q)) ||
      (p.description && p.description.toLowerCase().includes(q))
    )
  })
})

const filteredTrails = computed(() => {
  return trails.value.filter(t => {
    if (!showTrails.value) return false
    if (difficultyFilter.value !== 'all') {
      const d = (t.difficulty || '').toLowerCase()
      if (difficultyFilter.value === 'easy' && !['easy', 's0', 's1'].includes(d)) return false
      if (difficultyFilter.value === 'medium' && !['medium', 's2', 's3'].includes(d)) return false
      if (difficultyFilter.value === 'hard' && !['hard', 's4', 's5'].includes(d)) return false
    }
    if (!searchQuery.value.trim()) return true
    const q = searchQuery.value.toLowerCase()
    return (
      t.name.toLowerCase().includes(q) ||
      (t.description && t.description.toLowerCase().includes(q)) ||
      (t.trailType && t.trailType.toLowerCase().includes(q))
    )
  })
})

// Search Results List for dropdown
interface SearchMatchItem {
  id: string | number
  type: 'place' | 'bikepark' | 'trail'
  title: string
  subtitle: string
  latitude: number
  longitude: number
  raw?: TravelPlace | Bikepark | TrailRef
}

const searchSuggestions = computed<SearchMatchItem[]>(() => {
  if (!searchQuery.value.trim() || searchQuery.value.trim().length < 2) return []
  const q = searchQuery.value.toLowerCase()
  const results: SearchMatchItem[] = []

  // Places
  for (const p of places.value) {
    if (p.name.toLowerCase().includes(q) || p.country?.toLowerCase().includes(q) || p.region?.toLowerCase().includes(q)) {
      results.push({
        id: `p-${p.id}`,
        type: 'place',
        title: p.name,
        subtitle: `${p.country || 'Ort'} · ${p.status === 'visited' ? '✓ Besucht' : '★ Bucket List'}`,
        latitude: p.latitude,
        longitude: p.longitude,
        raw: p,
      })
    }
  }

  // Bikeparks
  for (const b of bikeparks.value) {
    if (b.name.toLowerCase().includes(q) || b.country?.toLowerCase().includes(q) || b.region?.toLowerCase().includes(q)) {
      results.push({
        id: `b-${b.id}`,
        type: 'bikepark',
        title: b.name,
        subtitle: `Bikepark · ${b.country || ''} (${b.trails.length} Trails)`,
        latitude: b.latitude,
        longitude: b.longitude,
        raw: b,
      })
    }
  }

  // Trails
  for (const t of trails.value) {
    if (t.name.toLowerCase().includes(q)) {
      let lat = 0, lon = 0
      if (t.polyline) {
        try {
          const coords = JSON.parse(t.polyline)
          if (coords.length > 0) {
            lat = coords[0][0]
            lon = coords[0][1]
          }
        } catch { /* ignore */ }
      }
      results.push({
        id: `t-${t.id}`,
        type: 'trail',
        title: t.name,
        subtitle: `Trail · ${t.length.toFixed(1)} km · ${t.difficulty || 'Normal'}`,
        latitude: lat,
        longitude: lon,
        raw: t,
      })
    }
  }

  return results.slice(0, 8)
})

// Unique countries list
const uniqueCountries = computed(() => {
  const set = new Set<string>()
  for (const p of places.value) {
    if (p.country && p.country.trim()) set.add(p.country.trim())
  }
  for (const b of bikeparks.value) {
    if (b.country && b.country.trim()) set.add(b.country.trim())
  }
  return Array.from(set).sort()
})

const activeFiltersCount = computed(() => {
  let count = 0
  if (!showPlaces.value || !showBikeparks.value || !showTrails.value) count++
  if (statusFilter.value !== 'all') count++
  if (difficultyFilter.value !== 'all') count++
  if (selectedCountryFilter.value !== 'all') count++
  return count
})

function resetFilters() {
  showPlaces.value = true
  showBikeparks.value = true
  showTrails.value = true
  statusFilter.value = 'all'
  difficultyFilter.value = 'all'
  selectedCountryFilter.value = 'all'
  searchQuery.value = ''
}

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
    marker.bindTooltip(`<b>${place.name}</b><br/>${place.status === 'visited' ? '✓ Besucht' : '★ Bucket List'}`, { direction: 'top', offset: [0, -6] })
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
    marker.bindTooltip(`<b>&#x1F6B4; ${park.name}</b><br/>${park.trails.length} Trails`, { direction: 'top', offset: [0, -10] })
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
      const ratingStars = trail.rating ? ` · ⭐ ${trail.rating}/5` : ''
      polyline.bindTooltip(`<b>&#x1F6B4; ${trail.name}</b><br/>${trail.length.toFixed(1)} km · ${trail.elevationGain} hm${ratingStars}`)
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
  draftMarker.bindTooltip('Neuer Eintrag', { direction: 'top' }).openTooltip()
}

function removeDraftMarker() {
  if (draftMarker && map) {
    map.removeLayer(draftMarker)
    draftMarker = null
  }
}

async function fetchWeatherForLocation(lat: number, lon: number) {
  currentWeather.value = null
  isLoadingWeather.value = true
  try {
    currentWeather.value = await weatherService.getWeather(lat, lon)
  } catch {
    currentWeather.value = null
  } finally {
    isLoadingWeather.value = false
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
  fetchWeatherForLocation(place.latitude, place.longitude)
}

function openBikeparkPanel(park: Bikepark) {
  removeDraftMarker()
  selectedBikepark.value = park
  selectedPlace.value = null
  selectedTrail.value = null
  draftLocation.value = null
  panelMode.value = 'view'
  panelEntity.value = 'bikepark'
  showAddTrailForm.value = false
  fetchWeatherForLocation(park.latitude, park.longitude)
}

function openTrailPanel(trail: TrailRef) {
  removeDraftMarker()
  selectedTrail.value = trail
  selectedPlace.value = null
  selectedBikepark.value = null
  draftLocation.value = null
  panelMode.value = 'view'
  panelEntity.value = 'trail'

  if (trail.polyline) {
    try {
      const coords = JSON.parse(trail.polyline)
      if (coords.length > 0) {
        fetchWeatherForLocation(coords[0][0], coords[0][1])
      }
    } catch { /* ignore */ }
  }
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
  successMessage.value = null
  showAddTrailForm.value = false
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

// Reverse Geocoding on Map Click (Sprint 7 Feature)
async function onMapLocationSelected(lat: number, lng: number) {
  draftLocation.value = { latitude: lat, longitude: lng }
  draftCountry.value = selectedCountry.value ?? ''
  draftRegion.value = ''
  draftName.value = ''
  draftNotes.value = ''
  draftIsBikepark.value = false

  isGeocoding.value = true
  openCreatePlacePanel()
  refreshDraftMarker()

  try {
    const geo = await geocodingService.reverseGeocode(lat, lng)
    if (geo) {
      if (geo.name && !draftName.value) draftName.value = geo.name
      if (geo.country) draftCountry.value = geo.country
      if (geo.region) draftRegion.value = geo.region
    }
  } catch {
    // Non fatal
  } finally {
    isGeocoding.value = false
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
        difficulty: draftDifficulty.value,
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
    showToast('Erfolgreich gespeichert!')
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
    showToast('Ort aktualisiert!')
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
    showToast('Ort gelöscht.')
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
    showToast('Bikepark gelöscht.')
  } catch {
    apiError.value = 'Bikepark konnte nicht gelöscht werden.'
  }
}

// Rate trail (Sprint 5/7 Feature)
async function rateTrail(trail: TrailRef, rating: number) {
  try {
    const updated = await trailService.setRating(trail.id, rating)
    trail.rating = updated.rating
    if (selectedTrail.value?.id === trail.id) {
      selectedTrail.value.rating = updated.rating
    }
    // Update inside trails list
    trails.value = trails.value.map(t => t.id === trail.id ? { ...t, rating: updated.rating } : t)
    // Update inside bikeparks list
    bikeparks.value = bikeparks.value.map(b => ({
      ...b,
      trails: b.trails.map(t => t.id === trail.id ? { ...t, rating: updated.rating } : t)
    }))
    if (selectedBikepark.value) {
      selectedBikepark.value.trails = selectedBikepark.value.trails.map(t => t.id === trail.id ? { ...t, rating: updated.rating } : t)
    }
    showToast(`Trail mit ${rating} Sternen bewertet!`)
  } catch {
    apiError.value = 'Bewertung konnte nicht gespeichert werden.'
  }
}

// Add trail to bikepark
async function addTrailToBikepark() {
  if (!selectedBikepark.value || !newTrailName.value.trim()) return
  isSaving.value = true
  apiError.value = null
  try {
    const created = await trailService.create({
      name: newTrailName.value.trim(),
      bikeparkId: selectedBikepark.value.id,
      length: newTrailLength.value || 1.0,
      elevationGain: newTrailElevation.value || 100,
      difficulty: newTrailDifficulty.value,
      trailType: newTrailType.value,
      description: newTrailDescription.value.trim() || null,
      polyline: null,
      rating: newTrailRating.value,
    })

    const trailRef: TrailRef = {
      id: created.id,
      name: created.name,
      length: created.length,
      elevationGain: created.elevationGain,
      difficulty: created.difficulty,
      trailType: created.trailType,
      description: created.description,
      polyline: created.polyline,
      bikeparkId: created.bikeparkId,
      rating: created.rating,
      createdAtUtc: created.createdAtUtc,
      updatedAtUtc: created.updatedAtUtc,
    }

    selectedBikepark.value.trails.push(trailRef)
    trails.value.push(trailRef)
    showAddTrailForm.value = false
    newTrailName.value = ''
    newTrailDescription.value = ''
    showToast('Trail erfolgreich hinzugefügt!')
  } catch {
    apiError.value = 'Trail konnte nicht gespeichert werden.'
  } finally {
    isSaving.value = false
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
    showToast('Foto hochgeladen!')
  } catch {
    apiError.value = 'Foto-Upload fehlgeschlagen.'
  } finally {
    isUploading.value = false
    input.value = ''
  }
}

function showToast(msg: string) {
  successMessage.value = msg
  setTimeout(() => { successMessage.value = null }, 2500)
}

function selectSearchResult(item: SearchMatchItem) {
  showSearchDropdown.value = false
  if (item.type === 'place' && item.raw) {
    const p = item.raw as TravelPlace
    map?.flyTo([p.latitude, p.longitude], 10, { duration: 1.2 })
    openPlacePanel(p)
  } else if (item.type === 'bikepark' && item.raw) {
    const b = item.raw as Bikepark
    map?.flyTo([b.latitude, b.longitude], 11, { duration: 1.2 })
    openBikeparkPanel(b)
  } else if (item.type === 'trail' && item.raw) {
    const t = item.raw as TrailRef
    if (item.latitude && item.longitude) {
      map?.flyTo([item.latitude, item.longitude], 12, { duration: 1.2 })
    }
    openTrailPanel(t)
  }
}

async function handleOnlineLocationSearch() {
  if (!searchQuery.value.trim() || searchQuery.value.trim().length < 2) return
  isSearchingOnline.value = true
  try {
    onlineSearchResults.value = await geocodingService.searchLocation(searchQuery.value)
    showSearchDropdown.value = true
  } catch {
    onlineSearchResults.value = []
  } finally {
    isSearchingOnline.value = false
  }
}

function selectOnlineResult(res: SearchLocationResult) {
  showSearchDropdown.value = false
  searchQuery.value = ''
  map?.flyTo([res.latitude, res.longitude], 12, { duration: 1.2 })
  onMapLocationSelected(res.latitude, res.longitude)
  draftName.value = res.name
}

function goToPlaceDetail(placeId: number) {
  router.push(`/places/${placeId}`)
}

function zoomIn() { map?.zoomIn() }
function zoomOut() { map?.zoomOut() }

function centerUserLocation() {
  if (!navigator.geolocation) return
  navigator.geolocation.getCurrentPosition(
    (pos) => {
      map?.flyTo([pos.coords.latitude, pos.coords.longitude], 10, { duration: 1.2 })
    },
    () => {
      showToast('Standort konnte nicht ermittelt werden.')
    }
  )
}

watch(searchQuery, () => {
  showSearchDropdown.value = searchQuery.value.trim().length >= 2
  refreshPlaceLayer()
  refreshBikeparkLayer()
  refreshTrailLayer()
})

watch([showPlaces, showBikeparks, showTrails, statusFilter, difficultyFilter, selectedCountryFilter], () => {
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
  }).setView([20, 10], 3)

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; OpenStreetMap contributors',
    maxZoom: 19,
  }).addTo(map)

  placesLayer = L.layerGroup().addTo(map)
  bikeparksLayer = L.layerGroup().addTo(map)
  trailsLayer = L.layerGroup().addTo(map)

  map.on('click', (event) => {
    showSearchDropdown.value = false
    onMapLocationSelected(event.latlng.lat, event.latlng.lng)
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
    isMapReady.value = true
  } catch {
    apiError.value = 'GeoJSON konnte nicht geladen werden.'
  }

  await Promise.all([
    loadPlaces(),
    loadBikeparks(),
    loadTrails(),
  ])

  // Check query params for deep-link navigation
  if (route.query.placeId) {
    const id = Number(route.query.placeId)
    const target = places.value.find(p => p.id === id)
    if (target) {
      map.flyTo([target.latitude, target.longitude], 10, { duration: 1.0 })
      openPlacePanel(target)
    }
  } else if (route.query.bikeparkId) {
    const id = Number(route.query.bikeparkId)
    const target = bikeparks.value.find(b => b.id === id)
    if (target) {
      map.flyTo([target.latitude, target.longitude], 11, { duration: 1.0 })
      openBikeparkPanel(target)
    }
  }
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
        <input v-model="searchQuery" class="explorer__search-input" placeholder="Orte, Trails, Bikeparks suchen …" @keyup.enter="handleOnlineLocationSearch" @focus="showSearchDropdown = searchQuery.trim().length >= 2" />
        <button v-if="searchQuery" class="explorer__clear-btn" @click="searchQuery = ''; showSearchDropdown = false">×</button>
        <button class="explorer__search-btn" :disabled="isSearchingOnline" @click="handleOnlineLocationSearch">{{ isSearchingOnline ? '…' : 'FIND' }}</button>
      </div>

      <!-- Dropdown Suggestions -->
      <div v-if="showSearchDropdown && (searchSuggestions.length > 0 || onlineSearchResults.length > 0)" class="search-dropdown glass-panel">
        <div v-if="searchSuggestions.length > 0" class="search-dropdown__section">
          <div class="search-dropdown__header">In deinen Einträgen</div>
          <div v-for="item in searchSuggestions" :key="item.id" class="search-dropdown__item" @click="selectSearchResult(item)">
            <span class="search-dropdown__icon">{{ item.type === 'place' ? '📍' : item.type === 'bikepark' ? '🚲' : '🚵' }}</span>
            <div class="search-dropdown__info">
              <span class="search-dropdown__title">{{ item.title }}</span>
              <span class="search-dropdown__subtitle">{{ item.subtitle }}</span>
            </div>
          </div>
        </div>
        <div v-if="onlineSearchResults.length > 0" class="search-dropdown__section">
          <div class="search-dropdown__header">Weltweite Orte (OpenStreetMap)</div>
          <div v-for="(res, idx) in onlineSearchResults" :key="idx" class="search-dropdown__item" @click="selectOnlineResult(res)">
            <span class="search-dropdown__icon">🌍</span>
            <div class="search-dropdown__info">
              <span class="search-dropdown__title">{{ res.name }}</span>
              <span class="search-dropdown__subtitle">{{ res.displayName }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Filter toggles (Sprint 6) -->
    <div class="explorer__filters">
      <button class="explorer__filter-btn" :class="{ 'explorer__filter-btn--active': showPlaces }" @click="showPlaces = !showPlaces">
        <span class="explorer__filter-dot" style="background:#c4912a"></span>
        Orte ({{ filteredPlaces.length }})
      </button>
      <button class="explorer__filter-btn" :class="{ 'explorer__filter-btn--active': showBikeparks }" @click="showBikeparks = !showBikeparks">
        <span class="explorer__filter-dot" style="background:#7e57c2"></span>
        Bikeparks ({{ filteredBikeparks.length }})
      </button>
      <button class="explorer__filter-btn" :class="{ 'explorer__filter-btn--active': showTrails }" @click="showTrails = !showTrails">
        <span class="explorer__filter-dot" style="background:#42a5f5"></span>
        Trails ({{ filteredTrails.length }})
      </button>

      <!-- Advanced Filter Toggle Button -->
      <button
        class="explorer__filter-btn explorer__filter-btn--more"
        :class="{ 'explorer__filter-btn--active': activeFiltersCount > 0 || showFilterDrawer }"
        @click="showFilterDrawer = !showFilterDrawer"
      >
        <span class="material-symbols-outlined" style="font-size:16px;">tune</span>
        Filter {{ activeFiltersCount > 0 ? `(${activeFiltersCount})` : '' }}
      </button>
    </div>

    <!-- Filter Drawer Panel -->
    <div v-if="showFilterDrawer" class="filter-drawer glass-panel">
      <div class="filter-drawer__header">
        <span class="filter-drawer__title">Erweiterte Filter</span>
        <button class="filter-drawer__close" @click="showFilterDrawer = false">×</button>
      </div>
      <div class="filter-drawer__body">
        <label class="filter-drawer__field">
          <span>Status (Orte)</span>
          <select v-model="statusFilter">
            <option value="all">Alle (Besucht & Bucket List)</option>
            <option value="visited">✓ Nur Besucht</option>
            <option value="bucketList">★ Nur Bucket List</option>
          </select>
        </label>

        <label class="filter-drawer__field">
          <span>Schwierigkeit (Parks & Trails)</span>
          <select v-model="difficultyFilter">
            <option value="all">Alle Schwierigkeiten</option>
            <option value="easy">Leicht / Easy (S0-S1)</option>
            <option value="medium">Mittel / Medium (S2-S3)</option>
            <option value="hard">Schwer / Hard (S4-S5)</option>
          </select>
        </label>

        <label class="filter-drawer__field">
          <span>Land</span>
          <select v-model="selectedCountryFilter">
            <option value="all">Alle Länder</option>
            <option v-for="c in uniqueCountries" :key="c" :value="c">{{ c }}</option>
          </select>
        </label>

        <div class="filter-drawer__actions">
          <button class="filter-drawer__btn filter-drawer__btn--reset" @click="resetFilters">Zurücksetzen</button>
          <button class="filter-drawer__btn filter-drawer__btn--apply" @click="showFilterDrawer = false">Fertig</button>
        </div>
      </div>
    </div>

    <!-- Map controls -->
    <div class="explorer__controls">
      <button class="explorer__ctrl" title="Zoom In" @click="zoomIn">
        <span class="material-symbols-outlined">add</span>
      </button>
      <button class="explorer__ctrl" title="Zoom Out" @click="zoomOut">
        <span class="material-symbols-outlined">remove</span>
      </button>
      <button class="explorer__ctrl explorer__ctrl--primary" title="Mein Standort" @click="centerUserLocation">
        <span class="material-symbols-outlined" style="font-variation-settings:'FILL' 1">my_location</span>
      </button>
    </div>

    <!-- Toast message notification -->
    <transition name="fade">
      <div v-if="successMessage" class="explorer__toast">
        <span class="material-symbols-outlined">check_circle</span>
        {{ successMessage }}
      </div>
    </transition>

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
              <template v-else-if="panelMode === 'create'">{{ draftIsBikepark ? 'Neuer Bikepark' : 'Neuer Ort' }}</template>
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
        <!-- ================= Place view ================= -->
        <template v-if="panelEntity === 'place' && panelMode === 'view' && selectedPlace">
          <div v-if="selectedPlace.photoUrl" class="explorer__photo">
            <img :src="selectedPlace.photoUrl" alt="Foto" />
          </div>

          <h3 class="explorer__place-name">{{ selectedPlace.name }}</h3>

          <div class="explorer__tabs">
            <button class="explorer__tab" :class="{ 'explorer__tab--active': activeTab === 'info' }" @click="activeTab = 'info'">INFO</button>
            <button class="explorer__tab" :class="{ 'explorer__tab--active': activeTab === 'weather' }" @click="activeTab = 'weather'">WETTER</button>
            <button class="explorer__tab" :class="{ 'explorer__tab--active': activeTab === 'notes' }" @click="activeTab = 'notes'">NOTIZEN</button>
            <button class="explorer__tab" :class="{ 'explorer__tab--active': activeTab === 'photos' }" @click="activeTab = 'photos'">FOTOS</button>
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
                <option value="bucketList">★ Bucket List</option>
                <option value="visited">✓ Besucht</option>
              </select>
            </label>
          </div>

          <!-- Weather Tab (Sprint 7 Feature) -->
          <div v-if="activeTab === 'weather'" class="explorer__tab-content">
            <div v-if="isLoadingWeather" class="weather-box weather-box--loading">
              <div class="spinner-small"></div>
              <span>Live-Wetter wird geladen …</span>
            </div>
            <div v-else-if="currentWeather" class="weather-box">
              <div class="weather-box__main">
                <span class="weather-box__icon">{{ currentWeather.icon }}</span>
                <div>
                  <span class="weather-box__temp">{{ currentWeather.temperature }}°C</span>
                  <span class="weather-box__desc">{{ currentWeather.description }} (Gefühlt {{ currentWeather.apparentTemperature }}°C)</span>
                </div>
              </div>
              <div class="weather-box__meta">
                <span>💧 Feuchtigkeit: {{ currentWeather.humidity }}%</span>
                <span>💨 Wind: {{ currentWeather.windSpeed }} km/h</span>
              </div>

              <div class="weather-forecast" v-if="currentWeather.daily && currentWeather.daily.length">
                <h5 class="weather-forecast__title">Vorhersage</h5>
                <div class="weather-forecast__grid">
                  <div v-for="d in currentWeather.daily" :key="d.date" class="weather-forecast__item">
                    <span class="weather-forecast__day">{{ new Date(d.date).toLocaleDateString('de-DE', { weekday: 'short' }) }}</span>
                    <span>{{ d.icon }}</span>
                    <span class="weather-forecast__temps">{{ d.maxTemp }}° / {{ d.minTemp }}°</span>
                  </div>
                </div>
              </div>
            </div>
            <div v-else class="explorer__hint">Keine Wetterdaten verfügbar.</div>
          </div>

          <div v-if="activeTab === 'notes'" class="explorer__tab-content">
            <label class="explorer__field">
              <span>Notizen</span>
              <textarea v-model="editingNotes" rows="6" placeholder="Notizen zu diesem Ort …"></textarea>
            </label>

            <div v-if="selectedPlace.notes" class="explorer__note-card">
              <p class="explorer__note-text">{{ selectedPlace.notes }}</p>
              <span class="explorer__note-date">Zuletzt bearbeitet: {{ new Date(selectedPlace.updatedAtUtc).toLocaleDateString() }}</span>
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

        <!-- ================= Bikepark view ================= -->
        <template v-if="panelEntity === 'bikepark' && panelMode === 'view' && selectedBikepark">
          <div v-if="selectedBikepark.photoUrl" class="explorer__photo">
            <img :src="selectedBikepark.photoUrl" alt="Bikepark" />
          </div>

          <h3 class="explorer__place-name">{{ selectedBikepark.name }}</h3>

          <div class="explorer__tabs">
            <button class="explorer__tab" :class="{ 'explorer__tab--active': activeTab === 'info' }" @click="activeTab = 'info'">INFO</button>
            <button class="explorer__tab" :class="{ 'explorer__tab--active': activeTab === 'weather' }" @click="activeTab = 'weather'">WETTER</button>
          </div>

          <div v-if="activeTab === 'info'">
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

            <!-- Trails List with interactive Rating & Add button -->
            <div class="explorer__trail-list">
              <div class="explorer__section-header">
                <h4 class="explorer__section-title">Trails ({{ selectedBikepark.trails.length }})</h4>
                <button class="add-trail-btn" @click="showAddTrailForm = !showAddTrailForm">
                  {{ showAddTrailForm ? 'Abbrechen' : '+ Trail hinzufügen' }}
                </button>
              </div>

              <!-- Add Trail inline form -->
              <div v-if="showAddTrailForm" class="add-trail-card">
                <h5 class="add-trail-title">Neuen Trail hinzufügen</h5>
                <label class="explorer__field">
                  <span>Name</span>
                  <input v-model="newTrailName" type="text" placeholder="z. B. Flow Trail Line" />
                </label>
                <div class="form-row">
                  <label class="explorer__field">
                    <span>Länge (km)</span>
                    <input v-model.number="newTrailLength" type="number" step="0.1" />
                  </label>
                  <label class="explorer__field">
                    <span>Höhenmeter (hm)</span>
                    <input v-model.number="newTrailElevation" type="number" />
                  </label>
                </div>
                <div class="form-row">
                  <label class="explorer__field">
                    <span>Schwierigkeit</span>
                    <select v-model="newTrailDifficulty">
                      <option value="Easy">Easy (S0-S1)</option>
                      <option value="Medium">Medium (S2-S3)</option>
                      <option value="Hard">Hard (S4-S5)</option>
                    </select>
                  </label>
                  <label class="explorer__field">
                    <span>Typ</span>
                    <input v-model="newTrailType" type="text" placeholder="Flow, Downhill …" />
                  </label>
                </div>
                <label class="explorer__field">
                  <span>Bewertung (1-5 Sterne)</span>
                  <div class="star-rating-select">
                    <span
                      v-for="star in 5"
                      :key="star"
                      class="star-clickable"
                      :class="{ 'star-clickable--active': newTrailRating >= star }"
                      @click="newTrailRating = star"
                    >★</span>
                  </div>
                </label>
                <button class="explorer__btn explorer__btn--primary" :disabled="!newTrailName.trim() || isSaving" @click="addTrailToBikepark">
                  {{ isSaving ? 'Speichert …' : 'Trail speichern' }}
                </button>
              </div>

              <!-- Trail item list -->
              <div v-for="trail in selectedBikepark.trails" :key="trail.id" class="explorer__trail-item">
                <div class="explorer__trail-info" @click="openTrailPanel(trail)">
                  <span class="explorer__trail-name">&#x1F6B4; {{ trail.name }}</span>
                  <span class="explorer__trail-meta">{{ trail.length.toFixed(1) }} km · {{ trail.elevationGain }} hm</span>
                </div>
                <div class="explorer__trail-right">
                  <!-- Star rating -->
                  <div class="star-rating-interactive" title="Trail bewerten">
                    <span
                      v-for="star in 5"
                      :key="star"
                      class="star-btn"
                      :class="{ 'star-btn--active': (trail.rating || 0) >= star }"
                      @click.stop="rateTrail(trail, star)"
                    >★</span>
                  </div>
                  <span v-if="trail.difficulty" class="explorer__trail-diff" :style="{ background: getTrailColor(trail.difficulty) }">
                    {{ trail.difficulty }}
                  </span>
                </div>
              </div>
            </div>

            <label class="explorer__field" style="margin-top:12px;">
              <span>Website</span>
              <a v-if="selectedBikepark.website" :href="selectedBikepark.website" target="_blank" class="explorer__link">{{ selectedBikepark.website }}</a>
              <span v-else class="explorer__metric-value">–</span>
            </label>
          </div>

          <!-- Weather tab for bikepark -->
          <div v-if="activeTab === 'weather'" class="explorer__tab-content">
            <div v-if="isLoadingWeather" class="weather-box weather-box--loading">
              <div class="spinner-small"></div>
              <span>Live-Wetter wird geladen …</span>
            </div>
            <div v-else-if="currentWeather" class="weather-box">
              <div class="weather-box__main">
                <span class="weather-box__icon">{{ currentWeather.icon }}</span>
                <div>
                  <span class="weather-box__temp">{{ currentWeather.temperature }}°C</span>
                  <span class="weather-box__desc">{{ currentWeather.description }}</span>
                </div>
              </div>
              <div class="weather-box__meta">
                <span>💧 Feuchtigkeit: {{ currentWeather.humidity }}%</span>
                <span>💨 Wind: {{ currentWeather.windSpeed }} km/h</span>
              </div>
              <div class="weather-forecast" v-if="currentWeather.daily && currentWeather.daily.length">
                <h5 class="weather-forecast__title">Vorhersage</h5>
                <div class="weather-forecast__grid">
                  <div v-for="d in currentWeather.daily" :key="d.date" class="weather-forecast__item">
                    <span class="weather-forecast__day">{{ new Date(d.date).toLocaleDateString('de-DE', { weekday: 'short' }) }}</span>
                    <span>{{ d.icon }}</span>
                    <span class="weather-forecast__temps">{{ d.maxTemp }}° / {{ d.minTemp }}°</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </template>

        <!-- ================= Trail view ================= -->
        <template v-if="panelEntity === 'trail' && panelMode === 'view' && selectedTrail">
          <h3 class="explorer__place-name">{{ selectedTrail.name }}</h3>

          <div class="explorer__tabs">
            <button class="explorer__tab" :class="{ 'explorer__tab--active': activeTab === 'info' }" @click="activeTab = 'info'">INFO</button>
            <button class="explorer__tab" :class="{ 'explorer__tab--active': activeTab === 'weather' }" @click="activeTab = 'weather'">WETTER</button>
          </div>

          <div v-if="activeTab === 'info'">
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

            <!-- Trail Rating Interactive -->
            <div class="trail-rating-card">
              <span class="trail-rating-label">Trail-Bewertung</span>
              <div class="star-rating-large">
                <span
                  v-for="star in 5"
                  :key="star"
                  class="star-large-btn"
                  :class="{ 'star-large-btn--active': (selectedTrail.rating || 0) >= star }"
                  @click="rateTrail(selectedTrail, star)"
                >★</span>
              </div>
              <span class="trail-rating-current">
                {{ selectedTrail.rating ? `${selectedTrail.rating} von 5 Sternen` : 'Noch nicht bewertet — klicke zum Bewerten' }}
              </span>
            </div>

            <p v-if="selectedTrail.description" class="explorer__description">{{ selectedTrail.description }}</p>
          </div>

          <!-- Trail weather -->
          <div v-if="activeTab === 'weather'" class="explorer__tab-content">
            <div v-if="isLoadingWeather" class="weather-box weather-box--loading">
              <div class="spinner-small"></div>
              <span>Live-Wetter wird geladen …</span>
            </div>
            <div v-else-if="currentWeather" class="weather-box">
              <div class="weather-box__main">
                <span class="weather-box__icon">{{ currentWeather.icon }}</span>
                <div>
                  <span class="weather-box__temp">{{ currentWeather.temperature }}°C</span>
                  <span class="weather-box__desc">{{ currentWeather.description }}</span>
                </div>
              </div>
              <div class="weather-box__meta">
                <span>💧 Feuchtigkeit: {{ currentWeather.humidity }}%</span>
                <span>💨 Wind: {{ currentWeather.windSpeed }} km/h</span>
              </div>
            </div>
          </div>
        </template>

        <!-- ================= Place/Bikepark create form ================= -->
        <template v-if="panelEntity === 'place' && panelMode === 'create'">
          <div v-if="isGeocoding" class="geocoding-indicator">
            <div class="spinner-small"></div>
            <span>Standort-Details werden automatisch erkannt …</span>
          </div>

          <p v-if="draftLocation" class="explorer__coords-hint">
            📍 Koordinaten: {{ draftLocation.latitude.toFixed(4) }}, {{ draftLocation.longitude.toFixed(4) }}
          </p>

          <label class="explorer__field">
            <span>Name *</span>
            <input v-model="draftName" type="text" placeholder="z. B. Lago di Garda oder Bikepark Leogang" />
          </label>

          <label class="explorer__field">
            <span>Land</span>
            <input v-model="draftCountry" type="text" placeholder="z. B. Italien" />
          </label>

          <label class="explorer__field">
            <span>Region</span>
            <input v-model="draftRegion" type="text" placeholder="z. B. Trentino" />
          </label>

          <label class="explorer__toggle">
            <input type="checkbox" v-model="draftIsBikepark" />
            <span>🚲 Ist dieser Ort ein Bikepark?</span>
          </label>

          <template v-if="!draftIsBikepark">
            <label class="explorer__field">
              <span>Status</span>
              <select v-model="draftStatus">
                <option value="bucketList">★ Bucket List</option>
                <option value="visited">✓ Besucht</option>
              </select>
            </label>
          </template>

          <template v-else>
            <label class="explorer__field">
              <span>Schwierigkeit</span>
              <select v-model="draftDifficulty">
                <option value="Easy">Easy</option>
                <option value="Medium">Medium</option>
                <option value="Hard">Hard</option>
              </select>
            </label>
          </template>

          <label class="explorer__field">
            <span>Notiz / Beschreibung</span>
            <textarea v-model="draftNotes" rows="3" placeholder="Kurze Beschreibung oder Reiseziel …"></textarea>
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
            {{ isSaving ? 'Speichert …' : draftIsBikepark ? 'Bikepark anlegen' : 'Ort anlegen' }}
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
  max-width: 520px;
  padding: 0 16px;
}

.explorer__search-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 8px 6px 14px;
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-outline-variant);
  box-shadow: var(--shadow-md);
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

.explorer__clear-btn {
  background: transparent;
  border: none;
  font-size: 18px;
  color: var(--color-outline);
  cursor: pointer;
  padding: 0 4px;
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
  transition: opacity 0.18s;
}

.explorer__search-btn:hover:not(:disabled) {
  opacity: 0.9;
}

/* Search Dropdown */
.search-dropdown {
  position: absolute;
  top: 100%;
  left: 16px;
  right: 16px;
  margin-top: 8px;
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-outline-variant);
  box-shadow: var(--shadow-lg);
  max-height: 340px;
  overflow-y: auto;
  z-index: 1001;
}

.search-dropdown__section {
  padding: 6px 0;
}

.search-dropdown__header {
  padding: 6px 14px;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--color-outline);
  background: rgba(0, 0, 0, 0.02);
}

.search-dropdown__item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 14px;
  cursor: pointer;
  transition: background 0.15s;
  border-bottom: 1px solid var(--color-surface-container-high);
}

.search-dropdown__item:hover {
  background: var(--color-surface-container);
}

.search-dropdown__icon {
  font-size: 20px;
}

.search-dropdown__info {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.search-dropdown__title {
  font-weight: 600;
  font-size: 14px;
  color: var(--color-on-surface);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.search-dropdown__subtitle {
  font-size: 12px;
  color: var(--color-on-surface-variant);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* Filters */
.explorer__filters {
  position: absolute;
  top: 72px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 1000;
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  justify-content: center;
  padding: 0 16px;
}

.explorer__filter-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  border-radius: var(--radius-full);
  border: 1px solid var(--color-outline-variant);
  background: rgba(251, 248, 255, 0.9);
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

.explorer__filter-btn--more {
  background: rgba(251, 248, 255, 0.95);
}

.explorer__filter-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
}

/* Filter Drawer */
.filter-drawer {
  position: absolute;
  top: 115px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 1000;
  width: 340px;
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-outline-variant);
  box-shadow: var(--shadow-lg);
  padding: 16px;
}

.filter-drawer__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.filter-drawer__title {
  font-weight: 700;
  font-size: 14px;
  color: var(--color-primary);
}

.filter-drawer__close {
  background: transparent;
  border: none;
  font-size: 20px;
  cursor: pointer;
  color: var(--color-outline);
}

.filter-drawer__body {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.filter-drawer__field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 12px;
  font-weight: 600;
}

.filter-drawer__field select {
  padding: 8px;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-outline-variant);
  background: var(--color-surface);
  font-size: 13px;
}

.filter-drawer__actions {
  display: flex;
  justify-content: space-between;
  margin-top: 6px;
}

.filter-drawer__btn {
  padding: 6px 12px;
  border-radius: var(--radius-md);
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  border: none;
}

.filter-drawer__btn--reset {
  background: transparent;
  color: var(--color-error);
}

.filter-drawer__btn--apply {
  background: var(--color-primary);
  color: var(--color-on-primary);
}

/* Toast */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

.explorer__toast {
  position: absolute;
  bottom: 24px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 1100;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 20px;
  background: var(--color-primary-container);
  color: var(--color-on-primary-container);
  border: 1px solid var(--color-primary);
  border-radius: var(--radius-full);
  box-shadow: var(--shadow-lg);
  font-weight: 600;
  font-size: 14px;
}

/* Controls */
.explorer__controls {
  position: absolute;
  bottom: 24px;
  right: 24px;
  z-index: 1000;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.explorer__ctrl {
  width: 44px;
  height: 44px;
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  border: 1px solid var(--color-outline-variant);
  color: var(--color-on-surface);
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: var(--shadow-md);
  cursor: pointer;
  transition: all 0.18s;
}

.explorer__ctrl:hover {
  border-color: var(--color-primary);
  color: var(--color-primary);
}

.explorer__ctrl--primary {
  background: var(--color-primary);
  color: var(--color-on-primary);
  border-color: var(--color-primary);
}

.explorer__ctrl--primary:hover {
  background: var(--color-primary-container);
  color: var(--color-on-primary-container);
}

/* Panel */
.explorer__panel {
  position: absolute;
  top: 16px;
  right: 16px;
  bottom: 16px;
  width: 400px;
  max-width: calc(100vw - 32px);
  background: var(--color-surface);
  border: 1px solid var(--color-outline-variant);
  border-radius: var(--radius-xl);
  box-shadow: var(--shadow-xl);
  z-index: 1050;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  animation: slideIn 0.25s ease-out;
}

@keyframes slideIn {
  from { transform: translateX(100%); opacity: 0; }
  to { transform: translateX(0); opacity: 1; }
}

.explorer__panel-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 16px;
  border-bottom: 1px solid var(--color-surface-container-high);
  flex-shrink: 0;
}

.explorer__panel-brand {
  display: flex;
  align-items: center;
  gap: 12px;
}

.explorer__panel-icon {
  width: 40px;
  height: 40px;
  border-radius: var(--radius-md);
  background: var(--color-primary-container);
  color: var(--color-on-primary-container);
  display: flex;
  align-items: center;
  justify-content: center;
}

.explorer__panel-title {
  font-family: var(--font-display);
  font-size: 17px;
  font-weight: 700;
  color: var(--color-primary);
  margin: 0;
}

.explorer__panel-subtitle {
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--color-on-surface-variant);
  margin: 2px 0 0 0;
}

.explorer__panel-close {
  background: transparent;
  border: none;
  cursor: pointer;
  color: var(--color-outline);
}

.explorer__panel-body {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.explorer__photo {
  border-radius: var(--radius-md);
  overflow: hidden;
  max-height: 180px;
  border: 1px solid var(--color-outline-variant);
}

.explorer__photo img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.explorer__place-name {
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 700;
  color: var(--color-primary);
  margin: 0;
}

.explorer__tabs {
  display: flex;
  gap: 4px;
  border-bottom: 1px solid var(--color-surface-container-high);
}

.explorer__tab {
  flex: 1;
  padding: 8px 4px;
  background: transparent;
  border: none;
  border-bottom: 2px solid transparent;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.5px;
  color: var(--color-on-surface-variant);
  cursor: pointer;
}

.explorer__tab--active {
  color: var(--color-primary);
  border-bottom-color: var(--color-primary);
}

.explorer__grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}

.explorer__metric {
  background: var(--color-surface-container-low);
  padding: 10px;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-outline-variant);
}

.explorer__metric-label {
  display: block;
  font-size: 10px;
  font-weight: 700;
  text-transform: uppercase;
  color: var(--color-outline);
}

.explorer__metric-value {
  font-weight: 600;
  font-size: 15px;
  color: var(--color-on-surface);
  margin-top: 2px;
}

.explorer__field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 12px;
  font-weight: 600;
}

.explorer__field input,
.explorer__field select,
.explorer__field textarea {
  padding: 8px 10px;
  border-radius: var(--radius-md);
  border: 1px solid var(--color-outline-variant);
  background: var(--color-surface-container-low);
  font-size: 13px;
  outline: none;
}

.explorer__toggle {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: var(--color-surface-container-low);
  border-radius: var(--radius-md);
  border: 1px solid var(--color-outline-variant);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
}

/* Weather UI in panel */
.weather-box {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 12px;
  background: var(--color-surface-container-low);
  border-radius: var(--radius-md);
  border: 1px solid var(--color-outline-variant);
}

.weather-box--loading {
  align-items: center;
  justify-content: center;
  padding: 20px;
  gap: 8px;
  color: var(--color-on-surface-variant);
  font-size: 13px;
}

.weather-box__main {
  display: flex;
  align-items: center;
  gap: 12px;
}

.weather-box__icon {
  font-size: 32px;
}

.weather-box__temp {
  font-family: var(--font-display);
  font-size: 22px;
  font-weight: 700;
  display: block;
}

.weather-box__desc {
  font-size: 12px;
  color: var(--color-on-surface-variant);
}

.weather-box__meta {
  display: flex;
  justify-content: space-between;
  font-size: 12px;
  color: var(--color-on-surface-variant);
}

.weather-forecast__title {
  margin: 8px 0 4px 0;
  font-size: 11px;
  text-transform: uppercase;
  color: var(--color-outline);
}

.weather-forecast__grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 4px;
}

.weather-forecast__item {
  display: flex;
  flex-direction: column;
  align-items: center;
  font-size: 11px;
  padding: 4px;
  background: var(--color-surface);
  border-radius: var(--radius-sm);
}

.weather-forecast__day {
  font-weight: 600;
}

.weather-forecast__temps {
  font-weight: 700;
  font-size: 10px;
}

/* Trail Rating in Panel */
.trail-rating-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  padding: 12px;
  background: var(--color-surface-container-low);
  border-radius: var(--radius-md);
  border: 1px solid var(--color-outline-variant);
  margin: 6px 0;
}

.trail-rating-label {
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  color: var(--color-outline);
}

.star-rating-large {
  display: flex;
  gap: 6px;
}

.star-large-btn {
  font-size: 28px;
  color: var(--color-outline-variant);
  cursor: pointer;
  transition: transform 0.1s, color 0.1s;
}

.star-large-btn:hover {
  transform: scale(1.2);
}

.star-large-btn--active {
  color: #fbc02d;
}

.trail-rating-current {
  font-size: 12px;
  color: var(--color-on-surface-variant);
}

/* Trail List in Bikepark */
.explorer__section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 10px;
}

.explorer__section-title {
  font-size: 13px;
  font-weight: 700;
  text-transform: uppercase;
  margin: 0;
}

.add-trail-btn {
  background: transparent;
  border: 1px solid var(--color-secondary);
  color: var(--color-secondary);
  border-radius: var(--radius-full);
  padding: 3px 10px;
  font-size: 11px;
  font-weight: 700;
  cursor: pointer;
}

.add-trail-card {
  padding: 12px;
  background: var(--color-surface-container);
  border-radius: var(--radius-md);
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin: 8px 0;
}

.add-trail-title {
  margin: 0;
  font-size: 13px;
  font-weight: 700;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}

.star-rating-select {
  display: flex;
  gap: 4px;
}

.star-clickable {
  font-size: 20px;
  color: var(--color-outline-variant);
  cursor: pointer;
}

.star-clickable--active {
  color: #fbc02d;
}

.explorer__trail-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 10px;
  background: var(--color-surface-container-low);
  border-radius: var(--radius-md);
  margin-top: 6px;
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
  flex: 1;
}

.explorer__trail-name {
  font-weight: 600;
  font-size: 13px;
}

.explorer__trail-meta {
  font-size: 11px;
  color: var(--color-on-surface-variant);
}

.explorer__trail-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.star-rating-interactive {
  display: flex;
  gap: 1px;
}

.star-btn {
  font-size: 14px;
  color: var(--color-outline-variant);
  cursor: pointer;
}

.star-btn:hover {
  transform: scale(1.15);
}

.star-btn--active {
  color: #fbc02d;
}

.explorer__trail-diff {
  padding: 2px 8px;
  border-radius: var(--radius-full);
  font-size: 10px;
  font-weight: 700;
  color: #ffffff;
}

.geocoding-indicator {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: var(--color-surface-container);
  border-radius: var(--radius-md);
  font-size: 12px;
  color: var(--color-primary);
}

.spinner-small {
  width: 16px;
  height: 16px;
  border: 2px solid var(--color-outline-variant);
  border-top-color: var(--color-primary);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.explorer__note-card {
  padding: 10px;
  background: var(--color-surface-container-low);
  border-radius: var(--radius-md);
  border: 1px solid var(--color-outline-variant);
}

.explorer__note-text {
  margin: 0;
  font-size: 13px;
}

.explorer__note-date {
  display: block;
  font-size: 10px;
  color: var(--color-outline);
  margin-top: 4px;
}

.explorer__coords-hint {
  font-size: 12px;
  color: var(--color-on-surface-variant);
  margin: 0;
}

.explorer__error {
  color: var(--color-error);
  font-size: 12px;
  margin: 0;
}

.explorer__hint {
  font-size: 12px;
  color: var(--color-on-surface-variant);
}

/* Panel Footer */
.explorer__panel-footer {
  display: flex;
  gap: 8px;
  padding: 14px 16px;
  border-top: 1px solid var(--color-surface-container-high);
}

.explorer__btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 10px 14px;
  border-radius: var(--radius-md);
  font-weight: 700;
  font-size: 13px;
  cursor: pointer;
  border: none;
  flex: 1;
}

.explorer__btn--primary {
  background: var(--color-secondary);
  color: var(--color-on-secondary);
}

.explorer__btn--secondary {
  background: var(--color-surface-container);
  color: var(--color-on-surface);
  border: 1px solid var(--color-outline-variant);
}

.explorer__btn--danger {
  background: transparent;
  border: 1px solid var(--color-error);
  color: var(--color-error);
  flex: 0 0 auto;
}

.explorer__overlay {
  display: none;
}

@media (max-width: 640px) {
  .explorer__panel {
    top: auto;
    left: 0;
    right: 0;
    bottom: 0;
    width: 100%;
    max-width: 100%;
    max-height: 80vh;
    border-radius: var(--radius-xl) var(--radius-xl) 0 0;
  }
}
</style>
