// src/services/authService.ts
import axios from 'axios';

const API_URL = '/api/auth';

export interface RegisterRequest {
  email: string;
  password: string;
  confirmPassword: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  userId: number;
  email: string;
  token: string;
}

export interface AuthError {
  message: string;
  errors?: Record<string, string>;
}

class AuthService {
  private tokenKey = 'auth_token';
  private userKey = 'auth_user';

  async register(data: RegisterRequest): Promise<AuthResponse> {
    try {
      const response = await axios.post<AuthResponse>(`${API_URL}/register`, data);
      this.setToken(response.data.token);
      this.setUser(response.data);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  async login(data: LoginRequest): Promise<AuthResponse> {
    try {
      const response = await axios.post<AuthResponse>(`${API_URL}/login`, data);
      this.setToken(response.data.token);
      this.setUser(response.data);
      return response.data;
    } catch (error: any) {
      throw this.handleError(error);
    }
  }

  logout(): void {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  getUser(): AuthResponse | null {
    const user = localStorage.getItem(this.userKey);
    return user ? JSON.parse(user) : null;
  }

  isAuthenticated(): boolean {
    return !!this.getToken();
  }

  private setToken(token: string): void {
    localStorage.setItem(this.tokenKey, token);
    axios.defaults.headers.common['Authorization'] = `Bearer ${token}`;
  }

  private setUser(user: AuthResponse): void {
    localStorage.setItem(this.userKey, JSON.stringify(user));
  }

  private handleError(error: any): AuthError {
    if (error.response?.data) {
      return error.response.data;
    }
    return { message: 'Ein Fehler ist aufgetreten' };
  }
}

export default new AuthService();
