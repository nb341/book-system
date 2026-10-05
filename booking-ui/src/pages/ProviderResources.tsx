import { useState, type SyntheticEvent } from 'react'
import { Link } from 'react-router-dom'
import { ApiError, fieldMessages } from '../api/client.ts'
import { createResource, listMyResources } from '../api/provider.ts'
import { ErrorBanner } from '../components/ErrorBanner.tsx'
import { FieldErrors } from '../components/FieldErrors.tsx'
import { useLoad } from '../hooks/useLoad.ts'

export function ProviderResources() {
  const { data: resources, error: loadError, isLoading, reload } = useLoad(listMyResources)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [error, setError] = useState<ApiError | null>(null)
  const [isPending, setIsPending] = useState(false)

  async function handleSubmit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault()
    if (isPending) return
    setError(null)
    setIsPending(true)
    try {
      await createResource({ name: name.trim(), description: description.trim() })
      setName('')
      setDescription('')
      reload()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught : new ApiError(0, 'Unexpected error', 'Something went wrong.'))
    } finally {
      setIsPending(false)
    }
  }

  const hasFieldErrors = error !== null && Object.keys(error.fieldErrors).length > 0
  const nameErrors = fieldMessages(error, 'name')
  const descriptionErrors = fieldMessages(error, 'description')

  return (
    <section>
      <h1>My resources</h1>
      {isLoading && <p className="status" role="status">Loading...</p>}
      <ErrorBanner message={loadError?.message ?? null} />
      {resources && resources.length === 0 && <p className="status">You have no resources yet. Add one below.</p>}
      {resources && resources.length > 0 && (
        <ul className="card-list">
          {resources.map((resource) => (
            <li key={resource.id}>
              <h2>{resource.name}</h2>
              {resource.description && <p>{resource.description}</p>}
              <Link to={`/provider/resources/${resource.id}`}>Manage slots</Link>
            </li>
          ))}
        </ul>
      )}

      <h2>Add resource</h2>
      <form onSubmit={handleSubmit} noValidate>
        <ErrorBanner message={error && !hasFieldErrors ? error.message : null} />
        <label>
          Name
          <input
            type="text"
            required
            maxLength={200}
            value={name}
            onChange={(e) => setName(e.target.value)}
            aria-invalid={nameErrors.length > 0}
          />
        </label>
        <FieldErrors messages={nameErrors} />
        <label>
          Description
          <textarea
            maxLength={2000}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            aria-invalid={descriptionErrors.length > 0}
          />
        </label>
        <FieldErrors messages={descriptionErrors} />
        <button type="submit" disabled={isPending}>{isPending ? 'Adding...' : 'Add resource'}</button>
      </form>
    </section>
  )
}
