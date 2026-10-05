export function FieldErrors({ messages }: { messages: string[] }) {
  if (messages.length === 0) return null
  return (
    <ul className="field-errors">
      {messages.map((message) => <li key={message}>{message}</li>)}
    </ul>
  )
}
