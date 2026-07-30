export function LoadingState({ label = 'Cargando información' }: { label?: string }) {
  return <div className="state-panel" role="status" aria-live="polite"><span className="loader" aria-hidden="true" />{label}…</div>
}

export function ErrorState({ message, retry }: { message: string; retry?: () => void }) {
  return <div className="state-panel error-state" role="alert"><strong>No se han podido cargar los datos</strong><p>{message}</p>{retry && <button className="button button-secondary" onClick={retry}>Reintentar</button>}</div>
}

export function EmptyState({ title, message }: { title: string; message: string }) {
  return <div className="state-panel"><strong>{title}</strong><p>{message}</p></div>
}

export function PageHeader({ eyebrow, title, description, actions }: { eyebrow: string; title: string; description?: string; actions?: React.ReactNode }) {
  return <header className="page-header"><div><span className="eyebrow">{eyebrow}</span><h1>{title}</h1>{description && <p>{description}</p>}</div>{actions && <div className="page-actions">{actions}</div>}</header>
}
