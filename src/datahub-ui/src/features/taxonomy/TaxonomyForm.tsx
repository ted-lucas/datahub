// Schema-driven form used by both the detail panel (edit existing) and the
// create dialog (new under parent). Keeps `values` as a flat dictionary;
// the level descriptor's `create`/`update` callbacks decide how to map it
// onto the actual DTO shape.

import { Box, Checkbox, FormControlLabel, MenuItem, TextField } from '@mui/material'
import type { FieldDescriptor } from './types'

export interface TaxonomyFormProps {
  fields: FieldDescriptor[]
  values: Record<string, any>
  onChange: (next: Record<string, any>) => void
  /** When editing, also render an `isActive` switch at the bottom. */
  showIsActive?: boolean
  isActive?: boolean
  onIsActiveChange?: (v: boolean) => void
  disabled?: boolean
}

export function TaxonomyForm(props: TaxonomyFormProps) {
  const { fields, values, onChange, showIsActive, isActive, onIsActiveChange, disabled } = props

  const setField = (name: string, v: any) => onChange({ ...values, [name]: v })

  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2 }}>
      {fields.map((f) => {
        const sx = { gridColumn: f.colSpan === 2 ? '1 / -1' : 'auto' }
        const v = values[f.name] ?? (f.type === 'boolean' ? false : '')
        if (f.type === 'boolean') {
          return (
            <Box key={f.name} sx={sx}>
              <FormControlLabel
                control={
                  <Checkbox
                    checked={!!v}
                    onChange={(e) => setField(f.name, e.target.checked)}
                    disabled={disabled}
                  />
                }
                label={f.label}
              />
            </Box>
          )
        }
        if (f.type === 'select') {
          return (
            <TextField
              key={f.name}
              select
              label={f.label}
              value={v ?? ''}
              onChange={(e) => setField(f.name, e.target.value || null)}
              required={f.required}
              helperText={f.helperText}
              disabled={disabled}
              size="small"
              sx={sx}
            >
              <MenuItem value="">(none)</MenuItem>
              {(f.options ?? []).map((o) => (
                <MenuItem key={o.value} value={o.value}>
                  {o.label}
                </MenuItem>
              ))}
            </TextField>
          )
        }
        return (
          <TextField
            key={f.name}
            label={f.label}
            value={v ?? ''}
            onChange={(e) => {
              const raw = e.target.value
              if (f.type === 'number') {
                setField(f.name, raw === '' ? null : Number(raw))
              } else {
                setField(f.name, raw === '' ? null : raw)
              }
            }}
            required={f.required}
            helperText={f.helperText}
            type={f.type === 'number' ? 'number' : 'text'}
            disabled={disabled}
            size="small"
            sx={sx}
          />
        )
      })}
      {showIsActive && (
        <Box sx={{ gridColumn: '1 / -1' }}>
          <FormControlLabel
            control={
              <Checkbox
                checked={!!isActive}
                onChange={(e) => onIsActiveChange?.(e.target.checked)}
                disabled={disabled}
              />
            }
            label="Active"
          />
        </Box>
      )}
    </Box>
  )
}

/** Strips `null`/empty for the initial form values from a DTO. */
export function valuesFromRaw(fields: FieldDescriptor[], raw: any): Record<string, any> {
  const out: Record<string, any> = {}
  for (const f of fields) {
    out[f.name] = raw?.[f.name] ?? (f.type === 'boolean' ? false : '')
  }
  return out
}

/** Empty defaults for create form. */
export function emptyValues(fields: FieldDescriptor[]): Record<string, any> {
  const out: Record<string, any> = {}
  for (const f of fields) out[f.name] = f.type === 'boolean' ? false : ''
  return out
}
