import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { authApi, type UserDto } from '../api/endpoints'
import { setAccessToken } from '../api/client'

interface AuthContextValue {
  user: UserDto | null
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
  hasPermission: (perm: string) => boolean
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null)
  const [loading, setLoading] = useState(true)

  // Try silent refresh on app load (in case user has valid refresh cookie)
  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        const r = await authApi.refresh()
        if (cancelled) return
        setAccessToken(r.accessToken)
        // We don't have full user on refresh response yet; could add a /me endpoint.
        // For now, leave user null until login or implement /me.
      } catch {
        // not logged in
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const res = await authApi.login(email, password)
    setAccessToken(res.accessToken)
    setUser(res.user)
  }, [])

  const logout = useCallback(async () => {
    try {
      await authApi.logout()
    } finally {
      setAccessToken(null)
      setUser(null)
    }
  }, [])

  const hasPermission = useCallback(
    (perm: string) => user?.permissions?.includes(perm) ?? false,
    [user]
  )

  const value = useMemo(
    () => ({ user, loading, login, logout, hasPermission }),
    [user, loading, login, logout, hasPermission]
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
