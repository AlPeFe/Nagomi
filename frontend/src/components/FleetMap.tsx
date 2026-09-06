import { useEffect, useRef } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import type { CoordinationRow, JourneyStatus } from '../types'
import { statusLabel } from '../utils'

const OSM_TILES = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png'
const OSM_ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
const DEFAULT_VIEW: [number, number] = [41.3851, 2.1734]

/** Semantic color per journey status — the fleet map reads at a glance who is moving. */
function statusColor(status: JourneyStatus): string {
  switch (status) {
    case 'Scheduled': return '#94a3b8'
    case 'Activated':
    case 'EnRouteToOrigin': return '#f59e0b'
    case 'ArrivedAtOrigin':
    case 'PatientOnBoard': return '#f97316'
    case 'EnRouteToDestination': return '#3b82f6'
    case 'ArrivedAtDestination': return '#10b981'
    case 'Completed': return '#22c55e'
    case 'Cancelled': return '#ef4444'
    default: return '#94a3b8'
  }
}

function lastPosition(row: CoordinationRow): { latitude: number; longitude: number } | null {
  for (let i = row.statusPoints.length - 1; i >= 0; i--) {
    const point = row.statusPoints[i]
    if (point.latitude != null && point.longitude != null
      && Number.isFinite(point.latitude) && Number.isFinite(point.longitude)
      && point.latitude !== 0 && point.longitude !== 0) {
      return { latitude: point.latitude, longitude: point.longitude }
    }
  }
  return null
}

interface FleetMapProps {
  rows: CoordinationRow[]
  className?: string
}

/**
 * Live fleet map: one marker per active journey at its latest reported position,
 * colored by status. Popup shows the journey, patient, vehicle, driver and last event.
 * The coordination page polls /api/coordination every 30s, so this stays live.
 */
export function FleetMap({ rows, className }: FleetMapProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const mapRef = useRef<L.Map | null>(null)
  const markersRef = useRef<L.Marker[]>([])

  useEffect(() => {
    if (!containerRef.current || mapRef.current) return
    const map = L.map(containerRef.current).setView(DEFAULT_VIEW, 13)
    L.tileLayer(OSM_TILES, { attribution: OSM_ATTRIBUTION, maxZoom: 19 }).addTo(map)
    mapRef.current = map
    return () => { map.remove(); mapRef.current = null; markersRef.current = [] }
  }, [])

  const positioned = rows
    .map((row) => ({ row, position: lastPosition(row) }))
    .filter((entry): entry is { row: CoordinationRow; position: { latitude: number; longitude: number } } => entry.position !== null)

  useEffect(() => {
    const map = mapRef.current
    if (!map) return
    markersRef.current.forEach((marker) => marker.remove())
    markersRef.current = []
    if (positioned.length === 0) return

    positioned.forEach(({ row, position }) => {
      const color = statusColor(row.status)
      const icon = L.divIcon({
        className: 'fleet-marker',
        html: `<div style="background:${color};color:#fff;border-radius:50%;width:24px;height:24px;display:flex;align-items:center;justify-content:center;font-size:11px;font-weight:700;border:2px solid #fff;box-shadow:0 1px 4px rgba(0,0,0,.45)">${row.vehiclePublicId ? row.vehiclePublicId.replace(/[^A-Za-z0-9]/g, '').slice(-2).toUpperCase() : '·'}</div>`,
        iconSize: [24, 24],
        iconAnchor: [12, 12],
      })
      const vehicle = row.vehicleName ? `${row.vehicleName}${row.vehiclePublicId ? ` (${row.vehiclePublicId})` : ''}` : 'Sin vehículo'
      const driver = row.driverName ? ` · ${row.driverName}` : ''
      const last = row.statusPoints[row.statusPoints.length - 1]
      const marker = L.marker([position.latitude, position.longitude], { icon })
      marker.bindPopup(
        `<strong>${row.journeyPublicId}</strong> — ${statusLabel(row.status)}<br/>` +
        `${row.patientName || 'Sin identificar'}<br/>` +
        `${row.origin} → ${row.destination}<br/>` +
        `${vehicle}${driver}<br/>` +
        `<small>${last ? new Date(last.occurredAt).toLocaleString('es-ES') : ''}</small>`,
      )
      marker.addTo(map)
      markersRef.current.push(marker)
    })

    if (positioned.length === 1) {
      map.setView([positioned[0].position.latitude, positioned[0].position.longitude], 14)
    } else {
      const bounds = L.latLngBounds(positioned.map((entry) => [entry.position.latitude, entry.position.longitude] as [number, number]))
      map.fitBounds(bounds, { padding: [40, 40], maxZoom: 14 })
    }
  }, [positioned])

  if (positioned.length === 0) {
    return <div className="fleet-map-empty">Sin posiciones registradas. Los puntos aparecen cuando el vehículo reporta estados con ubicación.</div>
  }

  return <div ref={containerRef} className={`fleet-map ${className ?? ''}`} role="application" aria-label="Mapa de flota en movimiento" />
}
