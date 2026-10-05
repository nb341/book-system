export function ErrorBanner({ message }: { message: string | null }) {
  if (!message) return null
  return <p className="error-banner" role="alert">{message}</p>
}
