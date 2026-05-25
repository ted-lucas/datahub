import { useState, type ReactNode } from 'react'
import {
  AppBar,
  Box,
  CssBaseline,
  Divider,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
  Toolbar,
  Typography,
} from '@mui/material'
import DashboardIcon from '@mui/icons-material/Dashboard'
import StorageIcon from '@mui/icons-material/Storage'
import TableChartIcon from '@mui/icons-material/TableChart'
import MapIcon from '@mui/icons-material/Map'
import PeopleIcon from '@mui/icons-material/People'
import SportsBaseballIcon from '@mui/icons-material/SportsBaseball'
import AccountCircleIcon from '@mui/icons-material/AccountCircle'
import { Link as RouterLink, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

const drawerWidth = 240

const navItems = [
  { label: 'Dashboard', to: '/', icon: <DashboardIcon /> },
  { label: 'Data Sources', to: '/data-sources', icon: <StorageIcon /> },
  { label: 'Data Entries', to: '/data-entries', icon: <TableChartIcon /> },
  { label: 'Map', to: '/map', icon: <MapIcon />, perm: 'geo:read' },
  { label: 'Sports', to: '/admin/taxonomy/sports', icon: <SportsBaseballIcon />, perm: 'sports:read' },
  { label: 'Users', to: '/admin/users', icon: <PeopleIcon />, perm: 'users:read' },
]

export function Layout({ children }: { children: ReactNode }) {
  const { user, logout, hasPermission } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)

  const handleLogout = async () => {
    setAnchor(null)
    await logout()
    navigate('/login')
  }

  return (
    <Box sx={{ display: 'flex' }}>
      <CssBaseline />
      <AppBar position="fixed" sx={{ zIndex: (t) => t.zIndex.drawer + 1 }}>
        <Toolbar>
          <Typography variant="h6" sx={{ flexGrow: 1 }}>DataHub</Typography>
          {user && (
            <>
              <Typography variant="body2" sx={{ mr: 1 }}>{user.email}</Typography>
              <IconButton color="inherit" onClick={(e) => setAnchor(e.currentTarget)}>
                <AccountCircleIcon />
              </IconButton>
              <Menu anchorEl={anchor} open={Boolean(anchor)} onClose={() => setAnchor(null)}>
                <MenuItem onClick={handleLogout}>Logout</MenuItem>
              </Menu>
            </>
          )}
        </Toolbar>
      </AppBar>
      <Drawer
        variant="permanent"
        sx={{
          width: drawerWidth,
          flexShrink: 0,
          [`& .MuiDrawer-paper`]: { width: drawerWidth, boxSizing: 'border-box' },
        }}
      >
        <Toolbar />
        <Divider />
        <List>
          {navItems
            .filter((it) => !it.perm || hasPermission(it.perm))
            .map((it) => (
              <ListItemButton
                key={it.to}
                component={RouterLink}
                to={it.to}
                selected={location.pathname === it.to}
              >
                <ListItemIcon>{it.icon}</ListItemIcon>
                <ListItemText primary={it.label} />
              </ListItemButton>
            ))}
        </List>
      </Drawer>
      <Box component="main" sx={{ flexGrow: 1, p: 3 }}>
        <Toolbar />
        {children}
      </Box>
    </Box>
  )
}
