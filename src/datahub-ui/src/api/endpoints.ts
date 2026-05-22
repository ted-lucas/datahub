import { api } from './client'

export interface UserDto {
  id: string
  email: string
  firstName: string
  lastName: string
  isActive: boolean
  roles: string[]
  permissions: string[]
}

export interface LoginResponse {
  accessToken: string
  accessTokenExpiresAt: string
  user: UserDto
}

export const authApi = {
  login: (email: string, password: string) =>
    api.post<LoginResponse>('/auth/login', { email, password }).then((r) => r.data),
  refresh: () =>
    api.post<{ accessToken: string; accessTokenExpiresAt: string }>('/auth/refresh').then((r) => r.data),
  logout: () => api.post('/auth/logout'),
}

export const usersApi = {
  list: () => api.get<UserDto[]>('/users').then((r) => r.data),
}
