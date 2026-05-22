import { useEffect, useState } from 'react'
import { Typography, Box, Paper, Table, TableBody, TableCell, TableHead, TableRow, CircularProgress, Alert } from '@mui/material'
import { usersApi, type UserDto } from '../../api/endpoints'

export default function Users() {
  const [users, setUsers] = useState<UserDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    usersApi.list()
      .then(setUsers)
      .catch((e) => setError(e?.message ?? 'Failed to load users'))
      .finally(() => setLoading(false))
  }, [])

  return (
    <Box>
      <Typography variant="h4" gutterBottom>Users</Typography>
      <Paper sx={{ p: 2 }}>
        {loading && <CircularProgress />}
        {error && <Alert severity="error">{error}</Alert>}
        {!loading && !error && (
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Email</TableCell>
                <TableCell>Name</TableCell>
                <TableCell>Roles</TableCell>
                <TableCell>Active</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {users.map((u) => (
                <TableRow key={u.id}>
                  <TableCell>{u.email}</TableCell>
                  <TableCell>{u.firstName} {u.lastName}</TableCell>
                  <TableCell>{u.roles.join(', ')}</TableCell>
                  <TableCell>{u.isActive ? 'Yes' : 'No'}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Paper>
    </Box>
  )
}
