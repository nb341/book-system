import { useCallback, useEffect, useState } from 'react'
import { ApiError } from '../api/client.ts'

export interface LoadState<T> {
  data: T | null
  error: ApiError | null
  isLoading: boolean
  reload: () => void
}

/**
 * Loads data on mount and whenever `reload` is called. `load` must be stable
 * (module-level or wrapped in useCallback). Stale data stays visible during a reload.
 */
export function useLoad<T>(load: (signal: AbortSignal) => Promise<T>): LoadState<T> {
  const [result, setResult] = useState<{ data: T | null; error: ApiError | null; done: boolean }>({
    data: null,
    error: null,
    done: false,
  })
  const [tick, setTick] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    load(controller.signal).then(
      (data) => {
        if (!controller.signal.aborted) setResult({ data, error: null, done: true })
      },
      (caught: unknown) => {
        if (controller.signal.aborted) return
        const error = caught instanceof ApiError ? caught : new ApiError(0, 'Unexpected error', 'Something went wrong.')
        setResult((prev) => ({ data: prev.data, error, done: true }))
      },
    )
    return () => controller.abort()
  }, [load, tick])

  const reload = useCallback(() => setTick((n) => n + 1), [])
  return { data: result.data, error: result.error, isLoading: !result.done, reload }
}
