import type { DeliveryState, JourneyStatus } from '../types'
import { deliveryLabel, statusLabel } from '../utils'

export function StatusBadge({ status }: { status: JourneyStatus }) {
  return <span className={`badge status-${status.toLowerCase()}`}><span aria-hidden="true" />{statusLabel(status)}</span>
}

export function DeliveryBadge({ state }: { state: DeliveryState }) {
  return <span className={`delivery delivery-${state.toLowerCase()}`}>{deliveryLabel(state)}</span>
}

export function Indicators({ external, cancelledBy, delivery }: { external?: boolean; cancelledBy?: string; delivery: DeliveryState }) {
  return <div className="indicators" aria-label="Indicadores">
    {external && <span title="Modificado por el proveedor">↗ Externo</span>}
    {cancelledBy === 'Provider' && <span className="indicator-alert">Cancelado por proveedor</span>}
    {delivery === 'Pending' && <span className="indicator-warning">No recibido</span>}
    {delivery === 'Dead' && <span className="indicator-alert">Notificación muerta</span>}
  </div>
}
