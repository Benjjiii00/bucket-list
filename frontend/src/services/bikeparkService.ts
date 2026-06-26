import axios from 'axios'

const API_URL = '/api/bikeparks'

export interface TrailRef {
  id: number
  name: string
  length: number
  elevationGain: number
  difficulty: string | null
  trailType: string | null
  description: string | null
  polyline: string | null
  bikeparkId: number | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface Bikepark {
  id: number
  name: string
  country: string | null
  region: string | null
  latitude: number
  longitude: number
  description: string | null
  difficulty: string | null
  website: string | null
  photoUrl: string | null
  trails: TrailRef[]
  createdAtUtc: string
  updatedAtUtc: string
}

export interface UpdateBikeparkRequest {
  name: string
  country: string | null
  region: string | null
  latitude: number
  longitude: number
  description: string | null
  difficulty: string | null
  website: string | null
  photoUrl: string | null
}

class BikeparkService {
  async getAll(): Promise<Bikepark[]> {
    const response = await axios.get<Bikepark[]>(API_URL)
    return response.data
  }

  async getById(id: number): Promise<Bikepark> {
    const response = await axios.get<Bikepark>(`${API_URL}/${id}`)
    return response.data
  }

  async create(data: UpdateBikeparkRequest): Promise<Bikepark> {
    const response = await axios.post<Bikepark>(API_URL, data)
    return response.data
  }

  async update(id: number, data: UpdateBikeparkRequest): Promise<Bikepark> {
    const response = await axios.put<Bikepark>(`${API_URL}/${id}`, data)
    return response.data
  }

  async delete(id: number): Promise<void> {
    await axios.delete(`${API_URL}/${id}`)
  }
}

export default new BikeparkService()
