import { useEffect, useEffectEvent, useRef, useState } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import { MapPin, Truck, X } from '@phosphor-icons/react'
import type { Journey } from '../types'
import { statusLabel } from '../utils'
import { geocodeAddress } from '../geocode'

const OSM_TILES = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png'
const OSM_ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
const DEFAULT_VIEW: [number, number] = [41.3851, 2.1734]

type Placement = { kind: 'origin' | 'destination' | 'vehicle'; latitude: number; longitude: number; label: string }

function markerHtml(kind: Placement['kind']) {
  const style = kind === 'origin'
    ? 'background:#fff;color:#0d7a75;border:2px solid #0d7a75'
    : kind === 'destination'
      ? 'background:#0d7a75;color:#fff;border:2px solid #0d7a75'
      : 'background:#E86F2D;color:#fff;border:2px solid #fff'
  return `<div style="${style};border-radius:50%;width:26px;height:26px;display:flex;align-items:center;justify-content:center;font-size:11px;font-weight:700;box-shadow:0 1px 4px rgba(0,0,0,.35)">${kind === 'vehicle' ? '&#9650;' : ''}</div>`
}

/**
 * Mapa de un traslado: origen, destino y el vehículo con las posiciones que haya
 * reportado en sus estados. Origen y destino no guardan coordenadas, así que se
 * geocodifican al abrir (Nominatim/OSM, igual que el resto de mapas de la app).
 */
export function JourneyMapModal({ journey, onClose }: { journey: Journey; onClose: () => void }) {
  const [placements, setPlacements] = useState<Placement[]>([])
  const [pending, setPending] = useState(true)
  const containerRef = useRef<HTMLDivElement>(null)
  const mapRef = useRef<L.Map | null>(null)

  async function locate() {
    setPending(true)
    const found: Placement[] = []
    const vehiclePoints = (journey.statusEvents ?? []).filter((event) =>
      typeof event.latitude === 'number' && typeof event.longitude === 'number' && event.latitude !== 0 && event.longitude !== 0)

    // Las posiciones del vehículo son datos propios: nunca dependen de un servicio externo.
    for (const point of vehiclePoints) {
      found.push({ kind: 'vehicle', latitude: point.latitude!, longitude: point.longitude!, label: `${statusLabel(point.status)} · ${new Date(point.occurredAt).toLocaleString('es-ES')}` })
    }

    const ends: Array<{ kind: 'origin' | 'destination'; query: string; label: string }> = [
      { kind: 'origin', query: [journey.origin.name, journey.origin.address, journey.origin.municipality, 'España'].filter(Boolean).join(', '), label: `Origen: ${journey.origin.name}` },
      { kind: 'destination', query: [journey.destination.name, journey.destination.address, journey.destination.municipality, 'España'].filter(Boolean).join(', '), label: `Destino: ${journey.destination.name}` },
    ]
    for (const end of ends) {
      try {
        const hit = await geocodeAddress(end.query)
        if (hit) found.push({ kind: end.kind, latitude: hit.latitude, longitude: hit.longitude, label: end.label })
      } catch {
        // Si el geocoder no responde, el mapa sigue mostrando lo que sí conocemos.
      }
    }
    setPlacements(found)
    setPending(false)
  }
  const locateEffect = useEffectEvent(locate)

  useEffect(() => { void locateEffect() }, [journey.id])

  useEffect(() => {
    function onKey(event: KeyboardEvent) { if (event.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  useEffect(() => {
    if (!containerRef.current || mapRef.current) return
    const map = L.map(containerRef.current).setView(DEFAULT_VIEW, 12)
    L.tileLayer(OSM_TILES, { attribution: OSM_ATTRIBUTION, maxZoom: 19 }).addTo(map)
    mapRef.current = map
    return () => { map.remove(); mapRef.current = null }
  }, [])

  useEffect(() => {
    const map = mapRef.current
    if (!map || !placements.length) return
    const markers = placements.map((placement) => {
      const icon = L.divIcon({ className: 'journey-map-marker', html: markerHtml(placement.kind), iconSize: [26, 26], iconAnchor: [13, 13] })
      return L.marker([placement.latitude, placement.longitude], { icon }).bindPopup(placement.label).addTo(map)
    })
    const bounds = L.latLngBounds(placements.map((p) => [p.latitude, p.longitude] as [number, number]))
    if (placements.length === 1) map.setView([placements[0].latitude, placements[0].longitude], 14)
    else map.fitBounds(bounds, { padding: [36, 36], maxZoom: 15 })
    return () => { markers.forEach((marker) => marker.remove()) }
  }, [placements])

  const vehicleCount = placements.filter((placement) => placement.kind === 'vehicle').length

  return <>
    <div className="qv-scrim" onClick={onClose} aria-hidden="true" />
    <div className="map-modal" role="dialog" aria-modal="true" aria-label={`Mapa de ${journey.publicId}`}>
      <header className="map-modal-head">
        <div>
          <h2>{journey.publicId}</h2>
          <p className="muted">{journey.origin.name} → {journey.destination.name}</p>
        </div>
        <button className="icon-button" onClick={onClose} aria-label="Cerrar mapa"><X size={15} aria-hidden="true" /></button>
      </header>
      <div ref={containerRef} className="map-modal-canvas" role="application" aria-label="Mapa del traslado" />
      <footer className="map-modal-foot">
        <ul className="map-legend">
          <li><span className="map-dot map-dot-origin" aria-hidden="true">◯</span> Origen: {journey.origin.name}</li>
          <li><span className="map-dot map-dot-destination" aria-hidden="true">●</span> Destino: {journey.destination.name}</li>
          <li><span className="map-dot map-dot-vehicle" aria-hidden="true"><Truck size={11} aria-hidden="true" /></span>
            {journey.vehicleName ? `Vehículo ${journey.vehicleName}` : 'Sin vehículo asignado'} · {vehicleCount ? `${vehicleCount} posición${vehicleCount > 1 ? 'es' : ''} reportada${vehicleCount > 1 ? 's' : ''}` : 'sin posiciones reportadas'}
          </li>
        </ul>
        <p className="muted">
          {pending ? 'Localizando origen y destino…' : 'Si el origen o el destino no aparecen, el geocoder no ha resuelto la dirección; los nombres están en la leyenda.'}
          {' '}<MapPin size={11} aria-hidden="true" /> OpenStreetMap
        </p>
      </footer>
    </div>
  </>
}