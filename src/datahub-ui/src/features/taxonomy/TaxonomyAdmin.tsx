// Composes TaxonomyTree + a detail panel + a create dialog. Stateless wrt
// the schema's domain shape: everything domain-specific is in the schema.

import { useState } from 'react'
import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Paper,
  Stack,
  Typography,
} from '@mui/material'
import DeleteIcon from '@mui/icons-material/Delete'
import SaveIcon from '@mui/icons-material/Save'
import { TaxonomyTree } from './TaxonomyTree'
import { TaxonomyForm, emptyValues, valuesFromRaw } from './TaxonomyForm'
import type { TaxonomyNode, TaxonomySchema } from './types'

interface CreateRequest {
  parent: TaxonomyNode | null
  childLevelId: string
}

export interface TaxonomyAdminProps {
  schema: TaxonomySchema
  canManage: boolean
}

export function TaxonomyAdmin({ schema, canManage }: TaxonomyAdminProps) {
  const [selected, setSelected] = useState<TaxonomyNode | null>(null)
  const [editValues, setEditValues] = useState<Record<string, any>>({})
  const [editIsActive, setEditIsActive] = useState<boolean>(true)
  const [saving, setSaving] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [err, setErr] = useState<string | null>(null)
  const [reloadToken, setReloadToken] = useState(0)

  const [createReq, setCreateReq] = useState<CreateRequest | null>(null)
  const [createValues, setCreateValues] = useState<Record<string, any>>({})
  const [creating, setCreating] = useState(false)
  const [createErr, setCreateErr] = useState<string | null>(null)

  const onSelect = (node: TaxonomyNode) => {
    setSelected(node)
    setErr(null)
    const level = schema.levels[node.levelId]
    setEditValues(valuesFromRaw(level.fields, node.raw))
    setEditIsActive((node.raw as any)?.isActive ?? true)
  }

  const onSave = async () => {
    if (!selected) return
    const level = schema.levels[selected.levelId]
    if (!level.update) return
    setSaving(true)
    setErr(null)
    try {
      const updated = await level.update(selected, { ...editValues, isActive: editIsActive })
      setSelected(updated)
      setReloadToken((t) => t + 1)
    } catch (e: any) {
      setErr(e?.response?.data?.error ?? e?.message ?? 'Save failed')
    } finally {
      setSaving(false)
    }
  }

  const onDelete = async () => {
    if (!selected) return
    const level = schema.levels[selected.levelId]
    if (!level.remove) return
    if (!window.confirm(`Delete ${level.singular} "${selected.label}"?`)) return
    setDeleting(true)
    setErr(null)
    try {
      await level.remove(selected)
      setSelected(null)
      setReloadToken((t) => t + 1)
    } catch (e: any) {
      setErr(e?.response?.data?.error ?? e?.message ?? 'Delete failed')
    } finally {
      setDeleting(false)
    }
  }

  const onCreateUnder = (parent: TaxonomyNode | null, childLevelId: string) => {
    const childLevel = schema.levels[childLevelId]
    if (!childLevel) return
    setCreateReq({ parent, childLevelId })
    setCreateValues(emptyValues(childLevel.fields))
    setCreateErr(null)
  }

  const onCreateSubmit = async () => {
    if (!createReq) return
    const level = schema.levels[createReq.childLevelId]
    if (!level.create) return
    setCreating(true)
    setCreateErr(null)
    try {
      await level.create(createReq.parent, createValues)
      setCreateReq(null)
      setReloadToken((t) => t + 1)
    } catch (e: any) {
      setCreateErr(e?.response?.data?.error ?? e?.message ?? 'Create failed')
    } finally {
      setCreating(false)
    }
  }

  const selectedLevel = selected ? schema.levels[selected.levelId] : null
  const createLevel = createReq ? schema.levels[createReq.childLevelId] : null

  return (
    <Box>
      <Typography variant="h4" gutterBottom>{schema.title}</Typography>
      <Box sx={{ display: 'grid', gridTemplateColumns: 'minmax(280px, 360px) 1fr', gap: 2, height: 'calc(100vh - 180px)' }}>
        <Paper sx={{ overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
          <TaxonomyTree
            schema={schema}
            selectedId={selected?.id ?? null}
            onSelect={onSelect}
            onCreateUnder={canManage ? onCreateUnder : () => {}}
            reloadToken={reloadToken}
          />
        </Paper>

        <Paper sx={{ p: 3, overflow: 'auto' }}>
          {!selected && (
            <Typography variant="body2" color="text.secondary">
              Select a node on the left to view or edit.
            </Typography>
          )}
          {selected && selectedLevel && (
            <Stack spacing={2}>
              <Stack direction="row" spacing={2} sx={{ alignItems: 'baseline' }}>
                <Typography variant="h6">{selected.label}</Typography>
                <Typography variant="caption" color="text.secondary">{selectedLevel.singular}</Typography>
              </Stack>
              <Divider />
              {err && <Alert severity="error" onClose={() => setErr(null)}>{err}</Alert>}
              <TaxonomyForm
                fields={selectedLevel.fields}
                values={editValues}
                onChange={setEditValues}
                showIsActive
                isActive={editIsActive}
                onIsActiveChange={setEditIsActive}
                disabled={!canManage || saving || deleting}
              />
              <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end' }}>
                <Button
                  color="error"
                  startIcon={<DeleteIcon />}
                  onClick={onDelete}
                  disabled={!canManage || saving || deleting || !selectedLevel.remove}
                >
                  Delete
                </Button>
                <Button
                  variant="contained"
                  startIcon={<SaveIcon />}
                  onClick={onSave}
                  disabled={!canManage || saving || deleting || !selectedLevel.update}
                >
                  {saving ? 'Saving…' : 'Save'}
                </Button>
              </Stack>
            </Stack>
          )}
        </Paper>
      </Box>

      <Dialog open={!!createReq} onClose={() => !creating && setCreateReq(null)} maxWidth="sm" fullWidth>
        <DialogTitle>
          New {createLevel?.singular}
          {createReq?.parent && (
            <Typography variant="caption" sx={{ display: 'block', color: 'text.secondary' }}>
              under {createReq.parent.label}
            </Typography>
          )}
        </DialogTitle>
        <DialogContent dividers>
          {createErr && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setCreateErr(null)}>{createErr}</Alert>}
          {createLevel && (
            <TaxonomyForm
              fields={createLevel.fields}
              values={createValues}
              onChange={setCreateValues}
              disabled={creating}
            />
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCreateReq(null)} disabled={creating}>Cancel</Button>
          <Button variant="contained" onClick={onCreateSubmit} disabled={creating}>
            {creating ? 'Creating…' : 'Create'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}
