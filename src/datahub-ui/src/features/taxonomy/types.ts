// Generic taxonomy primitives. A "taxonomy" is any strictly hierarchical
// editable tree: Sport→Level→League→Conference→Team is the first consumer,
// Geography (Country→State→County) will be the second.
//
// The model intentionally allows a single level to expose MULTIPLE child
// groupings (e.g. a League has both Conferences and Teams), and allows a
// level to be its own child (Conference→Conference), because those two
// shapes are required by the real Sports schema and we don't want to
// special-case them in the renderer.

import type { ReactNode } from 'react'

/** Opaque pointer into the tree. `raw` is the underlying DTO. */
export interface TaxonomyNode<T = unknown> {
  /** Stable id used as React key and as the API id when calling update/delete. */
  id: string
  /** Which level this node belongs to (key into TaxonomySchema.levels). */
  levelId: string
  /** Display label. */
  label: string
  /** Original DTO (used to seed the detail form). */
  raw: T
  /** Parent node, or null for roots. */
  parent: TaxonomyNode | null
}

// ── Field descriptors for the detail panel ─────────────────────────────────
export type FieldType = 'text' | 'number' | 'boolean' | 'select'

export interface FieldDescriptor {
  name: string
  label: string
  type: FieldType
  required?: boolean
  helperText?: string
  /** For `select`; ignored otherwise. */
  options?: { value: string; label: string }[]
  /** Optional column span (1 or 2) inside the 2-col detail grid. Defaults to 1. */
  colSpan?: 1 | 2
}

// ── Level descriptor ───────────────────────────────────────────────────────

/**
 * A "child grouping" is one bucket of children under a parent. A level may
 * declare more than one (e.g. League → [Conferences, Teams]). They render
 * as named sub-folders under the parent so the user always knows which
 * bucket they're inserting into.
 */
export interface ChildGrouping {
  /** Child level id. */
  childLevelId: string
  /** Label shown for the synthetic folder, e.g. "Conferences" / "Teams". */
  label: string
  /** Loads the children of `parent` that belong in this grouping. */
  load: (parent: TaxonomyNode) => Promise<TaxonomyNode[]>
}

export interface LevelDescriptor<T = any> {
  /** Singular display name, e.g. "Sport", "League". */
  singular: string
  /** Plural display name, e.g. "Sports", "Leagues". */
  plural: string
  /** Optional icon. */
  icon?: ReactNode
  /**
   * Form fields rendered in the detail panel. Same descriptor is used for
   * both create and update; `isActive` is only injected on update.
   */
  fields: FieldDescriptor[]
  /**
   * Buckets of children. Order matters — that's the order folders render
   * in. Empty array means this level is a leaf.
   */
  children: ChildGrouping[]
  /**
   * CRUD. `create` receives the parent node (null for roots) plus the form
   * values; return the new node so the tree can insert without a refetch.
   * `update` and `remove` receive the existing node.
   */
  create?: (parent: TaxonomyNode | null, values: Record<string, any>) => Promise<TaxonomyNode>
  update?: (node: TaxonomyNode<T>, values: Record<string, any>) => Promise<TaxonomyNode<T>>
  remove?: (node: TaxonomyNode<T>) => Promise<void>
}

export interface TaxonomySchema {
  /** Display title for the admin page. */
  title: string
  /** Loads the root nodes. */
  loadRoots: () => Promise<TaxonomyNode[]>
  /** Level id of the roots (used to gate create-at-root button). */
  rootLevelId: string
  /** All level descriptors keyed by level id. */
  levels: Record<string, LevelDescriptor>
}
