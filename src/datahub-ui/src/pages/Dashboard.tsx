import { Typography, Box, Paper } from '@mui/material'
import { useAuth } from '../auth/AuthContext'

export default function Dashboard() {
  const { user } = useAuth()
  return (
    <Box>
      <Typography variant="h4" gutterBottom>Dashboard</Typography>
      <Paper sx={{ p: 3 }}>
        <Typography>Welcome{user ? `, ${user.firstName || user.email}` : ''}.</Typography>
        <Typography variant="body2" sx={{ mt: 1 }}>
          This is the DataHub. Use the sidebar to navigate to data sources, entries, and admin areas.
        </Typography>
      </Paper>
    </Box>
  )
}
