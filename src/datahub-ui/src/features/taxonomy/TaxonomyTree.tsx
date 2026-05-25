// Recursive, lazy-loading tree. Each TreeNode renders one TaxonomyNode plus,
// when expanded, one synthetic "folder" per ChildGrouping declared by the
// node's level. The folder loads its children on first expand and caches
// the result on the folder state.
//
// We deliberately don't use MUI X TreeView: the grouping/leaf distinction +
// per-node action menus + selection model are simpler to express directly
// than to bend TreeView around.

import { useCallback, useEffect, useState } from 'react'
import {
  Box,
  CircularProgress,
  IconButton,
  List,
  ListItemButton,
  ListItemText,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import FolderOpenIcon from '@mui/icons-material/FolderOpen'
import AddIcon from '@mui/icons-material/Add'
import RefreshIcon from '@mui/icons-material/Refresh'
import type { ChildGrouping, TaxonomyNode, TaxonomySchema } from './types'

interface GroupingState {
  loaded: boolean
  loading: boolean
  error: string | null
  children: TaxonomyNode[]
  expanded: boolean
}

interface NodeState {
  expanded: boolean
  /** Keyed by ChildGrouping.childLevelId. */
  groupings: Record<string, GroupingState>
}

type NodeStateMap = Record<string, NodeState>

function emptyGroupingState(): GroupingState {
  return { loaded: false, loading: false, error: null, children: [], expanded: false }
}

export interface TaxonomyTreeProps {
  schema: TaxonomySchema
  selectedId: string | null
  onSelect: (node: TaxonomyNode) => void
  onCreateUnder: (parent: TaxonomyNode | null, childLevelId: string) => void
  /** Bumped by the parent after an external mutation to force a reload. */
  reloadToken: number
}

export function TaxonomyTree(props: TaxonomyTreeProps) {
  const { schema, selectedId, onSelect, onCreateUnder, reloadToken } = props
  const [roots, setRoots] = useState<TaxonomyNode[]>([])
  const [loadingRoots, setLoadingRoots] = useState(true)
  const [rootError, setRootError] = useState<string | null>(null)
  const [state, setState] = useState<NodeStateMap>({})

  const loadRoots = useCallback(() => {
    setLoadingRoots(true)
    setRootError(null)
    schema
      .loadRoots()
      .then((rs) => {
        setRoots(rs)
        setState({})
      })
      .catch((e: any) => setRootError(e?.message ?? 'Failed to load'))
      .finally(() => setLoadingRoots(false))
  }, [schema])

  useEffect(() => {
    loadRoots()
  }, [loadRoots, reloadToken])

  const ensureNodeState = (id: string): NodeState => state[id] ?? { expanded: false, groupings: {} }

  const toggleNode = (node: TaxonomyNode) => {
    setState((prev) => {
      const cur = prev[node.id] ?? { expanded: false, groupings: {} }
      return { ...prev, [node.id]: { ...cur, expanded: !cur.expanded } }
    })
  }

  const toggleGrouping = (nodeId: string, grouping: ChildGrouping, parent: TaxonomyNode) => {
    setState((prev) => {
      const cur = prev[nodeId] ?? { expanded: true, groupings: {} }
      const g = cur.groupings[grouping.childLevelId] ?? emptyGroupingState()
      const next: NodeState = {
        ...cur,
        expanded: true,
        groupings: { ...cur.groupings, [grouping.childLevelId]: { ...g, expanded: !g.expanded } },
      }
      // Lazy-load on first expand.
      if (!g.loaded && !g.loading && !g.expanded) {
        next.groupings[grouping.childLevelId] = { ...next.groupings[grouping.childLevelId], loading: true }
        grouping
          .load(parent)
          .then((kids) => {
            setState((p) => {
              const c = p[nodeId] ?? { expanded: true, groupings: {} }
              return {
                ...p,
                [nodeId]: {
                  ...c,
                  groupings: {
                    ...c.groupings,
                    [grouping.childLevelId]: {
                      loaded: true,
                      loading: false,
                      error: null,
                      children: kids,
                      expanded: true,
                    },
                  },
                },
              }
            })
          })
          .catch((e: any) => {
            setState((p) => {
              const c = p[nodeId] ?? { expanded: true, groupings: {} }
              const cg = c.groupings[grouping.childLevelId] ?? emptyGroupingState()
              return {
                ...p,
                [nodeId]: {
                  ...c,
                  groupings: {
                    ...c.groupings,
                    [grouping.childLevelId]: { ...cg, loading: false, error: e?.message ?? 'Failed to load' },
                  },
                },
              }
            })
          })
      }
      return { ...prev, [nodeId]: next }
    })
  }

  const reloadGrouping = (nodeId: string, grouping: ChildGrouping, parent: TaxonomyNode) => {
    setState((prev) => {
      const cur = prev[nodeId] ?? { expanded: true, groupings: {} }
      const g = cur.groupings[grouping.childLevelId] ?? emptyGroupingState()
      return {
        ...prev,
        [nodeId]: {
          ...cur,
          groupings: { ...cur.groupings, [grouping.childLevelId]: { ...g, loading: true, error: null } },
        },
      }
    })
    grouping
      .load(parent)
      .then((kids) =>
        setState((prev) => {
          const cur = prev[nodeId] ?? { expanded: true, groupings: {} }
          return {
            ...prev,
            [nodeId]: {
              ...cur,
              groupings: {
                ...cur.groupings,
                [grouping.childLevelId]: { loaded: true, loading: false, error: null, children: kids, expanded: true },
              },
            },
          }
        }),
      )
      .catch((e: any) =>
        setState((prev) => {
          const cur = prev[nodeId] ?? { expanded: true, groupings: {} }
          const cg = cur.groupings[grouping.childLevelId] ?? emptyGroupingState()
          return {
            ...prev,
            [nodeId]: {
              ...cur,
              groupings: {
                ...cur.groupings,
                [grouping.childLevelId]: { ...cg, loading: false, error: e?.message ?? 'Failed to load' },
              },
            },
          }
        }),
      )
  }

  const renderNode = (node: TaxonomyNode, depth: number) => {
    const ns = ensureNodeState(node.id)
    const level = schema.levels[node.levelId]
    const hasChildren = level && level.children.length > 0
    return (
      <Box key={`${node.levelId}:${node.id}`} sx={{ pl: depth * 2 }}>
        <ListItemButton
          dense
          selected={selectedId === node.id}
          onClick={() => onSelect(node)}
          sx={{ borderRadius: 1 }}
        >
          {hasChildren ? (
            <IconButton
              size="small"
              onClick={(e) => {
                e.stopPropagation()
                toggleNode(node)
              }}
              sx={{ mr: 0.5 }}
            >
              {ns.expanded ? <ExpandMoreIcon fontSize="small" /> : <ChevronRightIcon fontSize="small" />}
            </IconButton>
          ) : (
            <Box sx={{ width: 28 }} />
          )}
          <ListItemText
            primary={node.label}
            secondary={level?.singular}
            slotProps={{
              primary: { sx: { fontWeight: selectedId === node.id ? 600 : 400 } },
              secondary: { variant: 'caption' },
            }}
          />
        </ListItemButton>
        {hasChildren && ns.expanded && (
          <Box>
            {level.children.map((g) => {
              const gs = ns.groupings[g.childLevelId] ?? emptyGroupingState()
              return (
                <Box key={g.childLevelId} sx={{ pl: 3 }}>
                  <ListItemButton dense onClick={() => toggleGrouping(node.id, g, node)} sx={{ borderRadius: 1 }}>
                    {gs.expanded ? (
                      <ExpandMoreIcon fontSize="small" sx={{ mr: 0.5 }} />
                    ) : (
                      <ChevronRightIcon fontSize="small" sx={{ mr: 0.5 }} />
                    )}
                    <FolderOpenIcon fontSize="small" sx={{ mr: 1, color: 'text.secondary' }} />
                    <ListItemText
                      primary={g.label}
                      secondary={gs.loaded ? `${gs.children.length}` : undefined}
                      slotProps={{
                        primary: { variant: 'body2' },
                        secondary: { variant: 'caption' },
                      }}
                    />
                    <Tooltip title={`New ${schema.levels[g.childLevelId]?.singular ?? ''}`}>
                      <IconButton
                        size="small"
                        onClick={(e) => {
                          e.stopPropagation()
                          onCreateUnder(node, g.childLevelId)
                        }}
                      >
                        <AddIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    {gs.loaded && (
                      <Tooltip title="Reload">
                        <IconButton
                          size="small"
                          onClick={(e) => {
                            e.stopPropagation()
                            reloadGrouping(node.id, g, node)
                          }}
                        >
                          <RefreshIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    )}
                  </ListItemButton>
                  {gs.expanded && (
                    <Box sx={{ pl: 4 }}>
                      {gs.loading && <CircularProgress size={16} sx={{ ml: 2, my: 1 }} />}
                      {gs.error && (
                        <Typography variant="caption" color="error" sx={{ ml: 2 }}>
                          {gs.error}
                        </Typography>
                      )}
                      {gs.loaded && gs.children.length === 0 && (
                        <Typography variant="caption" color="text.secondary" sx={{ ml: 2 }}>
                          (empty)
                        </Typography>
                      )}
                      {gs.children.map((c) => renderNode(c, 0))}
                    </Box>
                  )}
                </Box>
              )
            })}
          </Box>
        )}
      </Box>
    )
  }

  return (
    <Box sx={{ height: '100%', overflow: 'auto', p: 1 }}>
      <Stack direction="row" spacing={1} sx={{ mb: 1, alignItems: 'center' }}>
        <Typography variant="subtitle2" sx={{ flexGrow: 1 }}>
          {schema.levels[schema.rootLevelId]?.plural ?? 'Roots'}
        </Typography>
        <Tooltip title={`New ${schema.levels[schema.rootLevelId]?.singular ?? ''}`}>
          <IconButton size="small" onClick={() => onCreateUnder(null, schema.rootLevelId)}>
            <AddIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Tooltip title="Reload">
          <IconButton size="small" onClick={loadRoots}>
            <RefreshIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Stack>
      {loadingRoots && <CircularProgress size={20} />}
      {rootError && (
        <Typography variant="caption" color="error">
          {rootError}
        </Typography>
      )}
      <List dense disablePadding>
        {roots.map((r) => renderNode(r, 0))}
      </List>
    </Box>
  )
}
