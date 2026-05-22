import axios from 'axios'

// Vite proxies /api → https://localhost:7283 in dev (see vite.config.ts).
export const api = axios.create({
  baseURL: '/api',
  withCredentials: true, // for the httpOnly refresh cookie
})

let accessToken: string | null = null
export const setAccessToken = (token: string | null) => {
  accessToken = token
}
export const getAccessToken = () => accessToken

api.interceptors.request.use((config) => {
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`
  }
  return config
})

// Refresh-on-401 logic. The refresh cookie lives at /api/auth path on the backend.
let refreshing: Promise<string | null> | null = null

api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const original = error.config
    const status = error.response?.status

    if (status === 401 && !original._retry && !original.url?.includes('/auth/')) {
      original._retry = true
      if (!refreshing) {
        refreshing = api
          .post<{ accessToken: string }>('/auth/refresh')
          .then((r) => {
            setAccessToken(r.data.accessToken)
            return r.data.accessToken
          })
          .catch(() => {
            setAccessToken(null)
            return null
          })
          .finally(() => {
            refreshing = null
          })
      }
      const newToken = await refreshing
      if (newToken) {
        original.headers.Authorization = `Bearer ${newToken}`
        return api(original)
      }
    }
    return Promise.reject(error)
  }
)
