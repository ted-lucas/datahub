import { TaxonomyAdmin } from '../../features/taxonomy/TaxonomyAdmin'
import { sportsSchema } from '../../features/taxonomy/sportsSchema'
import { useAuth } from '../../auth/AuthContext'

export default function SportsTaxonomy() {
  const { hasPermission } = useAuth()
  return <TaxonomyAdmin schema={sportsSchema} canManage={hasPermission('sports:manage')} />
}
