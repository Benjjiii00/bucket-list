import axios from 'axios'

const API_URL = '/api/trails'

export interface Trail {
  id: number
  name: string
  bikeparkId: number | null
  length: number
  elevationGain: number
  difficulty: string | null
  trailType: string | null
  description: string | null
  polyline: string | null
  rating?: number | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface UpdateTrailRequest {
  name: string
  bikeparkId: number | null
  length: number
  elevationGain: number
  difficulty: string | null
  trailType: string | null
  description: string | null
  polyline: string | null
  rating?: number | null
}

class TrailService {
  async getAll(): Promise<Trail[]> {
    const response = await axios.get<Trail[]>(API_URL)
    return response.data
  }

  async getById(id: number): Promise<Trail> {
    const response = await axios.get<Trail>(`${API_URL}/${id}`)
    return response.data
  }

  async create(data: UpdateTrailRequest): Promise<Trail> {
    const response = await axios.post<Trail>(API_URL, data)
    return response.data
  }

  async update(id: number, data: UpdateTrailRequest): Promise<Trail> {
    const response = await axios.put<Trail>(`${API_URL}/${id}`, data)
    return response.data
  }

  async setRating(id: number, rating: number | null): Promise<Trail> {
    const response = await axios.patch<Trail>(`${API_URL}/${id}/rating`, rating, {
      headers: { 'Content-Type': 'application/json' }
    })
    return response.data
  }

  async delete(id: number): Promise<void> {
    await axios.delete(`${API_URL}/${id}`)
  }
}

export default new TrailService()
