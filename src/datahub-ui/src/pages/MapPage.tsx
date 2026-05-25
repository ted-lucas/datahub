import { Box, Typography } from '@mui/material'
import { MapView } from '../features/map/MapView'

export default function MapPage() {
  return (
    <Box>
      <Typography variant="h5" gutterBottom>
        Map
      </Typography>
      <MapView />
    </Box>
  )
}
