<script setup lang="ts">
import axios from 'axios'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import L from 'leaflet'

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
  createdAtUtc: string
  updatedAtUtc: string
}

type CoordinateDraft = {
  latitude: number
  longitude: number
}

const mapElement = ref<HTMLDivElement | null>(null)
const selectedCountry = ref<string | null>(null)
const selectedPlace = ref<TravelPlace | null>(null)
const isMapReady = ref(false)
const countryCount = ref(0)
const places = ref<TravelPlace[]>([])
const draftLocation = ref<CoordinateDraft | null>(null)
const draftLatitudeStr = ref('')
const draftLongitudeStr = ref('')
const draftName = ref('')
const draftCountry = ref('')
const draftRegion = ref('')
const draftNotes = ref('')
const draftStatus = ref<PlaceStatus>('bucketList')
const isSavingPlace = ref(false)
const apiError = ref<string | null>(null)

let map: L.Map | null = null
let countriesLayer: L.GeoJSON | null = null
let placesLayer: L.LayerGroup | null = null

const defaultStyle: L.PathOptions = {
  color: '#88a2b7',
  weight: 1,
  opacity: 0.7,
  fillColor: '#285069',
  fillOpacity: 0.28,
}

const selectedStyle: L.PathOptions = {
  color: '#67d5c1',
  weight: 2,
  opacity: 1,
  fillColor: '#67d5c1',
  fillOpacity: 0.45,
}

const hoverStyle: L.PathOptions = {
  color: '#f6fbff',
  weight: 2,
  opacity: 1,
  fillColor: '#9fe8d9',
  fillOpacity: 0.42,
}

const selectionLabel = computed(() => selectedPlace.value?.name ?? selectedCountry.value ?? 'Noch kein Ort gewählt')
const placeCount = computed(() => places.value.length)
const draftCoordinatesLabel = computed(() => {
  if (!draftLocation.value) {
    return 'Klicke auf die Karte, um einen Ort für den neuen Marker zu setzen.'
  }

  return `${draftLocation.value.latitude.toFixed(4)}, ${draftLocation.value.longitude.toFixed(4)}`
})
const canSavePlace = computed(() => Boolean(draftLocation.value && draftName.value.trim() && !isSavingPlace.value))

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

function getPlaceStatusLabel(status: PlaceStatus): string {
  return status === 'visited' ? 'Besucht' : 'Bucket List'
}

function refreshLayerStyles() {
  if (!countriesLayer) {
    return
  }

  countriesLayer.eachLayer((layer) => {
    const pathLayer = layer as L.Path & { feature?: CountryFeature }
    const countryName = getCountryName(pathLayer.feature?.properties)
    pathLayer.setStyle(countryName === selectedCountry.value ? selectedStyle : defaultStyle)
  })
}

function selectCountry(feature: CountryFeature) {
  selectedCountry.value = getCountryName(feature.properties)
  selectedPlace.value = null
  draftCountry.value = selectedCountry.value
  refreshLayerStyles()
}

function selectPlace(place: TravelPlace) {
  selectedPlace.value = place
  selectedCountry.value = place.country ?? selectedCountry.value
  if (map) {
    map.flyTo([place.latitude, place.longitude], Math.max(map.getZoom(), 5), { animate: true })
  }
  refreshPlaceLayer()
}

function refreshPlaceLayer() {
  if (!placesLayer) {
    return
  }

  placesLayer.clearLayers()

  for (const place of places.value) {
    const isSelected = selectedPlace.value?.id === place.id
    const marker = L.circleMarker([place.latitude, place.longitude], {
      radius: isSelected ? 9 : 7,
      color: isSelected ? '#f6fbff' : place.status === 'visited' ? '#67d5c1' : '#f4b56b',
      weight: 2,
      fillColor: isSelected ? '#9fe8d9' : place.status === 'visited' ? '#67d5c1' : '#f4b56b',
      fillOpacity: 0.9,
    })

    marker.bindTooltip(place.name, {
      direction: 'top',
      offset: [0, -6],
    })

    marker.on('click', () => selectPlace(place))
    marker.addTo(placesLayer)
  }
}

function resetDraftPlace() {
  draftName.value = ''
  draftCountry.value = selectedCountry.value ?? ''
  draftRegion.value = ''
  draftNotes.value = ''
  draftStatus.value = 'bucketList'
  draftLocation.value = null
}

function setDraftLocation(latitude: number, longitude: number) {
  draftLocation.value = { latitude, longitude }
  if (!draftCountry.value && selectedCountry.value) {
    draftCountry.value = selectedCountry.value
  }
  // keep string inputs in sync when user clicks the map
  draftLatitudeStr.value = latitude.toString()
  draftLongitudeStr.value = longitude.toString()
}

function focusPlace(place: TravelPlace) {
  selectPlace(place)
}

async function loadPlaces() {
  try {
    apiError.value = null
    const response = await axios.get<TravelPlace[]>('/api/places')
    places.value = response.data
    refreshPlaceLayer()
  } catch (error) {
    console.error(error)
    apiError.value = 'Die Orte konnten nicht vom Backend geladen werden.'
  }
}

async function saveDraftPlace() {
  if (!draftLocation.value || !draftName.value.trim()) {
    return
  }

  isSavingPlace.value = true
  apiError.value = null

  try {
    const response = await axios.post<TravelPlace>('/api/places', {
      name: draftName.value.trim(),
      country: draftCountry.value.trim() || null,
      region: draftRegion.value.trim() || null,
      latitude: draftLocation.value.latitude,
      longitude: draftLocation.value.longitude,
      status: draftStatus.value,
      notes: draftNotes.value.trim() || null,
    })

    places.value = [...places.value, response.data].sort((left, right) => left.name.localeCompare(right.name))
    refreshPlaceLayer()
    selectPlace(response.data)
    resetDraftPlace()
  } catch (error) {
    console.error(error)
    apiError.value = 'Der Ort konnte nicht gespeichert werden.'
  } finally {
    isSavingPlace.value = false
  }
}

async function deletePlace(placeId: number) {
  if (!confirm('Möchtest du diesen Ort wirklich löschen?')) {
    return
  }

  apiError.value = null

  try {
    await axios.delete(`/api/places/${placeId}`)
    places.value = places.value.filter((p) => p.id !== placeId)
    if (selectedPlace.value?.id === placeId) {
      selectedPlace.value = null
    }
    refreshPlaceLayer()
  } catch (error) {
    console.error(error)
    apiError.value = 'Der Ort konnte nicht gelöscht werden.'
  }
}

onMounted(async () => {
  if (!mapElement.value) {
    return
  }

  map = L.map(mapElement.value, {
    zoomControl: true,
    minZoom: 2,
    maxZoom: 19,
    worldCopyJump: true,
  }).setView([18, 0], 2)

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; OpenStreetMap contributors',
    maxZoom: 19,
  }).addTo(map)

  placesLayer = L.layerGroup().addTo(map)

  map.on('click', (event) => {
    setDraftLocation(event.latlng.lat, event.latlng.lng)
    selectedPlace.value = null
    refreshPlaceLayer()
  })

  // keep draft string inputs synced with draftLocation
  watch(draftLocation, (loc) => {
    if (loc) {
      draftLatitudeStr.value = loc.latitude.toString()
      draftLongitudeStr.value = loc.longitude.toString()
    } else {
      draftLatitudeStr.value = ''
      draftLongitudeStr.value = ''
    }
  })

  watch([draftLatitudeStr, draftLongitudeStr], ([latStr, lngStr]) => {
    const lat = parseFloat(latStr)
    const lng = parseFloat(lngStr)
    if (!Number.isNaN(lat) && !Number.isNaN(lng)) {
      draftLocation.value = { latitude: lat, longitude: lng }
    } else {
      draftLocation.value = null
    }
  })

  try {
    const response = await fetch('/data/countries.geojson')

    if (!response.ok) {
      throw new Error(`GeoJSON konnte nicht geladen werden: ${response.status}`)
    }

    const countries = (await response.json()) as CountryCollection
    countryCount.value = countries.features.length

    countriesLayer = L.geoJSON(countries as never, {
      style: defaultStyle,
      onEachFeature: (feature, layer) => {
        const countryFeature = feature as CountryFeature

        layer.on({
          click: () => selectCountry(countryFeature),
          mouseover: () => {
            if (layer instanceof L.Path) {
              layer.setStyle(hoverStyle)
              layer.bringToFront()
            }
          },
          mouseout: () => {
            refreshLayerStyles()
          },
        })
      },
    }).addTo(map)

    map.fitBounds(countriesLayer.getBounds(), {
      padding: [24, 24],
    })

    isMapReady.value = true
  } catch (error) {
    console.error(error)
    apiError.value = 'GeoJSON konnte nicht geladen werden.'
  }

  await loadPlaces()
})

onBeforeUnmount(() => {
  map?.remove()
  map = null
  countriesLayer = null
  placesLayer = null
})
</script>

<template>
  <section class="world-map">
    <header class="world-map__hero">
      <div>
        <p class="world-map__eyebrow">Bucket List</p>
        <h1 class="world-map__title">Interaktive Weltkarte</h1>
        <p class="world-map__lead">
          Klick auf ein Land oder setze einen Marker auf der Karte. Orte werden aus der API
          geladen und im Backend gespeichert.
        </p>
      </div>

      <div class="world-map__stats">
        <div class="world-map__stat">
          <span class="world-map__stat-value">{{ countryCount }}</span>
          <span class="world-map__stat-label">Länder geladen</span>
        </div>
        <div class="world-map__stat">
          <span class="world-map__stat-value">{{ placeCount }}</span>
          <span class="world-map__stat-label">Orte gespeichert</span>
        </div>
        <div class="world-map__stat">
          <span class="world-map__stat-value">{{ isMapReady ? 'Bereit' : 'Lädt …' }}</span>
          <span class="world-map__stat-label">Kartenstatus</span>
        </div>
      </div>
    </header>

    <div class="world-map__layout">
      <div class="world-map__panel">
        <div ref="mapElement" class="world-map__map" aria-label="Weltkarte"></div>
      </div>

      <aside class="world-map__sidebar">
        <span class="world-map__tag">{{ selectedPlace ? 'Marker' : selectedCountry ? 'Land' : 'Orte speichern' }}</span>

        <div v-if="selectedPlace">
          <h2>{{ selectedPlace.name }}</h2>
          <p>
            {{ selectedPlace.country || 'Kein Land hinterlegt' }} ·
            {{ getPlaceStatusLabel(selectedPlace.status) }}
          </p>
        </div>
        <div v-else-if="selectedCountry">
          <h2>{{ selectedCountry }}</h2>
          <p>Das Land ist markiert. Klicke auf die Karte, um dort einen Ort zu speichern.</p>
        </div>
        <div v-else>
          <h2>Wähle ein Land oder setze einen Ort</h2>
          <p>
            Die Karte ist mit dem Backend verbunden. Setze Koordinaten, ergänze einen Namen und
            speichere den Ort direkt in der Datenbank.
          </p>
        </div>

        <p v-if="apiError" class="world-map__notice">{{ apiError }}</p>

        <ul v-if="selectedPlace" class="world-map__list">
          <li>
            <span class="world-map__meta">Land</span>
            <span class="world-map__value">{{ selectedPlace.country ?? 'Nicht gesetzt' }}</span>
          </li>
          <li>
            <span class="world-map__meta">Region</span>
            <span class="world-map__value">{{ selectedPlace.region ?? 'Nicht gesetzt' }}</span>
          </li>
          <li>
            <span class="world-map__meta">Status</span>
            <span class="world-map__value">{{ getPlaceStatusLabel(selectedPlace.status) }}</span>
          </li>
          <li>
            <span class="world-map__meta">Koordinaten</span>
            <span class="world-map__value">
              {{ selectedPlace.latitude.toFixed(4) }}, {{ selectedPlace.longitude.toFixed(4) }}
            </span>
          </li>
          <li>
            <span class="world-map__meta">Notizen</span>
            <span class="world-map__value">{{ selectedPlace.notes ?? 'Keine Notiz' }}</span>
          </li>
        </ul>

        <div v-if="selectedPlace" class="world-map__actions">
          <button class="world-map__button world-map__button--danger" type="button" @click="deletePlace(selectedPlace.id)">
            Ort löschen
          </button>
        </div>

        <section class="world-map__form">
          <div>
            <h3>Neuen Ort speichern</h3>
            <p>Die aktuelle Position kommt von deinem letzten Klick auf die Karte.</p>
          </div>

          <div class="world-map__coords">{{ draftCoordinatesLabel }}</div>

          <label class="world-map__field">
            <span>Name</span>
            <input v-model="draftName" type="text" placeholder="z. B. Finale Ligure" />
          </label>

          <label class="world-map__field">
            <span>Land</span>
            <input v-model="draftCountry" type="text" placeholder="z. B. Italien" />
          </label>

          <label class="world-map__field">
            <span>Region</span>
            <input v-model="draftRegion" type="text" placeholder="z. B. Ligurien" />
          </label>

          <label class="world-map__field">
            <span>Status</span>
            <select v-model="draftStatus">
              <option value="bucketList">Bucket List</option>
              <option value="visited">Besucht</option>
            </select>
          </label>

          <label class="world-map__field">
            <span>Notiz</span>
            <textarea
              v-model="draftNotes"
              rows="3"
              placeholder="Kurze Notiz zu diesem Ort"
            ></textarea>
          </label>

          <div class="world-map__actions">
            <button class="world-map__button" :disabled="!canSavePlace" @click="saveDraftPlace">
              {{ isSavingPlace ? 'Speichert …' : 'Ort speichern' }}
            </button>
            <button class="world-map__button world-map__button--ghost" type="button" @click="resetDraftPlace">
              Zurücksetzen
            </button>
          </div>
        </section>

        <section v-if="places.length > 0" class="world-map__places">
          <h3>Gespeicherte Orte</h3>
          <ul class="world-map__place-list">
            <li v-for="place in places" :key="place.id">
              <button class="world-map__place-button" type="button" @click="focusPlace(place)">
                <span class="world-map__place-name">{{ place.name }}</span>
                <span class="world-map__place-meta">
                  {{ place.country ?? 'Ohne Land' }} · {{ getPlaceStatusLabel(place.status) }}
                </span>
              </button>
            </li>
          </ul>
        </section>

        <ul class="world-map__list">
          <li>
            <span class="world-map__meta">Auswahl</span>
            <span class="world-map__value">{{ selectionLabel }}</span>
          </li>
        </ul>
      </aside>
    </div>
  </section>
</template>