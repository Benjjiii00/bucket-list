import axios from 'axios'

export interface ReverseGeocodeResult {
  displayName: string
  name: string
  country: string | null
  region: string | null
  city: string | null
}

export interface SearchLocationResult {
  displayName: string
  name: string
  latitude: number
  longitude: number
  type: string
}

class GeocodingService {
  async reverseGeocode(lat: number, lon: number): Promise<ReverseGeocodeResult | null> {
    try {
      const url = `https://nominatim.openstreetmap.org/reverse?format=json&lat=${lat}&lon=${lon}&zoom=14&addressdetails=1`
      const response = await axios.get(url, {
        headers: { 'Accept-Language': 'de,en' },
        timeout: 4000,
      })
      const data = response.data
      if (!data || !data.address) return null

      const addr = data.address
      const country = addr.country || null
      const region = addr.state || addr.region || addr.county || null
      const city = addr.city || addr.town || addr.village || addr.municipality || addr.suburb || null
      const name = addr.tourism || addr.natural || addr.leisure || addr.amenity || city || data.name || ''

      return {
        displayName: data.display_name,
        name,
        country,
        region,
        city,
      }
    } catch {
      return null
    }
  }

  async searchLocation(query: string): Promise<SearchLocationResult[]> {
    if (!query || query.trim().length < 2) return []
    try {
      const url = `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(query)}&limit=5&addressdetails=1`
      const response = await axios.get(url, {
        headers: { 'Accept-Language': 'de,en' },
        timeout: 4000,
      })
      const data = response.data
      if (!Array.isArray(data)) return []

      return data.map((item: { display_name: string; name?: string; lat: string; lon: string; type?: string }) => ({
        displayName: item.display_name,
        name: item.name || item.display_name.split(',')[0],
        latitude: parseFloat(item.lat),
        longitude: parseFloat(item.lon),
        type: item.type || 'place',
      }))
    } catch {
      return []
    }
  }
}

export default new GeocodingService()
