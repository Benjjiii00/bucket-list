import axios from 'axios';

const API_URL = '/api/places';

export interface TravelPlace {
  id: number;
  name: string;
  country: string | null;
  region: string | null;
  latitude: number;
  longitude: number;
  status: 'bucketList' | 'visited';
  notes: string | null;
  photoUrl: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UpdatePlaceRequest {
  name: string;
  country: string | null;
  region: string | null;
  latitude: number;
  longitude: number;
  status: 'bucketList' | 'visited';
  notes: string | null;
  photoUrl: string | null;
}

class PlacesService {
  async getAll(): Promise<TravelPlace[]> {
    const response = await axios.get<TravelPlace[]>(API_URL);
    return response.data;
  }

  async getById(id: number): Promise<TravelPlace> {
    const response = await axios.get<TravelPlace>(`${API_URL}/${id}`);
    return response.data;
  }

  async update(id: number, data: UpdatePlaceRequest): Promise<TravelPlace> {
    const response = await axios.put<TravelPlace>(`${API_URL}/${id}`, data);
    return response.data;
  }

  async uploadPhoto(id: number, file: File): Promise<TravelPlace> {
    const formData = new FormData();
    formData.append('file', file);
    const response = await axios.post<TravelPlace>(`${API_URL}/${id}/photo`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return response.data;
  }
}

export default new PlacesService();
