import { useState, type ReactNode } from 'react'
import {
  AppBar,
  Box,
  Collapse,
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
import AccountTreeIcon from '@mui/icons-material/AccountTree'
import GroupsIcon from '@mui/icons-material/Groups'
import ExpandLessIcon from '@mui/icons-material/ExpandLess'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import AccountCircleIcon from '@mui/icons-material/AccountCircle'
import { Link as RouterLink, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { TimeBar } from '../features/time/TimeBar'

const drawerWidth = 240
const timeBarHeight = 72 // approximate; gives content enough bottom padding

interface NavLeaf {
  label: string
  to: string
  icon: ReactNode
  perm?: string
}
interface NavGroup {
  label: string
  icon: ReactNode
  perm?: string
  children: NavLeaf[]
}
type NavEntry = NavLeaf | NavGroup
const isGroup = (e: NavEntry): e is NavGroup => 'children' in e

const navItems: NavEntry[] = [
  { label: 'Dashboard', to: '/', icon: <DashboardIcon /> },
  { label: 'Data Sources', to: '/data-sources', icon: <StorageIcon /> },
  { label: 'Data Entries', to: '/data-entries', icon: <TableChartIcon /> },
  { label: 'Map', to: '/map', icon: <MapIcon />, perm: 'geo:read' },
  {
    label: 'Sports',
    icon: <SportsBaseballIcon />,
    perm: 'sports:read',
    children: [
      { label: 'MLB Teams', to: '/sports/mlb', icon: <GroupsIcon /> },
      { label: 'Taxonomy', to: '/admin/taxonomy/sports', icon: <AccountTreeIcon /> },
    ],
  },
  { label: 'Users', to: '/admin/users', icon: <PeopleIcon />, perm: 'users:read' },
]

export function Layout({ children }: { children: ReactNode }) {
  const { user, logout, hasPermission } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)
  // Sports group is the only collapsible group today; default open so users see MLB at first glance.
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({ Sports: true })

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
            .map((it) => {
              if (isGroup(it)) {
                const open = !!openGroups[it.label]
                return (
                  <Box key={it.label}>
                    <ListItemButton onClick={() => setOpenGroups((s) => ({ ...s, [it.label]: !open }))}>
                      <ListItemIcon>{it.icon}</ListItemIcon>
                      <ListItemText primary={it.label} />
                      {open ? <ExpandLessIcon /> : <ExpandMoreIcon />}
                    </ListItemButton>
                    <Collapse in={open} timeout="auto" unmountOnExit>
                      <List component="div" disablePadding>
                        {it.children.map((c) => (
                          <ListItemButton
                            key={c.to}
                            component={RouterLink}
                            to={c.to}
                            selected={location.pathname === c.to}
                            sx={{ pl: 4 }}
                          >
                            <ListItemIcon>{c.icon}</ListItemIcon>
                            <ListItemText primary={c.label} />
                          </ListItemButton>
                        ))}
                      </List>
                    </Collapse>
                  </Box>
                )
              }
              return (
                <ListItemButton
                  key={it.to}
                  component={RouterLink}
                  to={it.to}
                  selected={location.pathname === it.to}
                >
                  <ListItemIcon>{it.icon}</ListItemIcon>
                  <ListItemText primary={it.label} />
                </ListItemButton>
              )
            })}
        </List>
      </Drawer>
      <Box component="main" sx={{ flexGrow: 1, p: 3, pb: `${timeBarHeight + 24}px` }}>
        <Toolbar />
        {children}
      </Box>
      <TimeBar />
    </Box>
  )
}
