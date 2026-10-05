import { Link } from 'react-router-dom'
import { listResources } from '../api/resources.ts'
import { ErrorBanner } from '../components/ErrorBanner.tsx'
import { useLoad } from '../hooks/useLoad.ts'

export function Resources() {
  const { data: resources, error, isLoading, reload } = useLoad(listResources)

  return (
    <section>
      <h1>Resources</h1>
      {isLoading && <p className="status" role="status">Loading...</p>}
      <ErrorBanner message={error?.message ?? null} />
      {error && <button type="button" className="secondary" onClick={reload}>Retry</button>}
      {resources && resources.length === 0 && <p className="status">No resources are available yet.</p>}
      {resources && resources.length > 0 && (
        <ul className="card-list">
          {resources.map((resource) => (
            <li key={resource.id}>
              <h2>{resource.name}</h2>
              {resource.description && <p className="grow">{resource.description}</p>}
              {resource.providerName && <p className="status">Provided by {resource.providerName}</p>}
              <Link className="push" to={`/resources/${resource.id}`}>View slots</Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
