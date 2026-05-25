import { BrowserRouter, Routes, Route } from 'react-router-dom'
import { ThemeProvider, CssBaseline } from '@mui/material'
import { theme } from './theme/theme'
import { AuthProvider } from './auth/AuthContext'
import { ProtectedRoute } from './auth/ProtectedRoute'
import { TimeRangeProvider } from './features/time/TimeRangeContext'
import { Layout } from './components/Layout'
import Login from './pages/Login'
import Dashboard from './pages/Dashboard'
import DataSources from './pages/DataSources'
import DataEntries from './pages/DataEntries'
import MapPage from './pages/MapPage'
import Users from './pages/admin/Users'
import SportsTaxonomy from './pages/admin/SportsTaxonomy'
import MlbTeams from './pages/sports/MlbTeams'

export default function App() {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <BrowserRouter>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<Login />} />
            <Route
              path="/*"
              element={
                <ProtectedRoute>
                  <TimeRangeProvider>
                    <Layout>
                      <Routes>
                        <Route path="/" element={<Dashboard />} />
                        <Route path="/data-sources" element={<DataSources />} />
                        <Route path="/data-entries" element={<DataEntries />} />
                        <Route path="/map" element={<MapPage />} />
                        <Route path="/admin/users" element={<Users />} />
                        <Route path="/admin/taxonomy/sports" element={<SportsTaxonomy />} />
                        <Route path="/sports/mlb" element={<MlbTeams />} />
                      </Routes>
                    </Layout>
                  </TimeRangeProvider>
                </ProtectedRoute>
              }
            />
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </ThemeProvider>
  )
}
