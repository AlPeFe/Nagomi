import { useEffect, useRef } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import type { StatusPoint } from '../types'
import { statusLabel } from '../utils'

const OSM_TILES = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png'
const OSM_ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
const DEFAULT_VIEW: [number, number] = [41.3851, 2.1734]

interface StatusMapProps {
  points: StatusPoint[]
  className?: string
}

function validPoint(point: StatusPoint): point is StatusPoint & { latitude: number; longitude: number } {
  return point.latitude != null && point.longitude != null
    && Number.isFinite(point.latitude) && Number.isFinite(point.longitude)
    && point.latitude !== 0 && point.longitude !== 0
}

const palette = ['#4caf50', '#2196f3', '#ff9800', '#e91e63', '#9c27b0', '#795548', '#009688', '#f44336']

/** Read-only Leaflet map plotting the position where each status event was reported (0/0 or absent positions are skipped). */
export function StatusMap({ points, className }: StatusMapProps) {
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

  const rendered = points.filter(validPoint)

  useEffect(() => {
    const map = mapRef.current
    if (!map) return
    markersRef.current.forEach((marker) => marker.remove())
    markersRef.current = []
    if (rendered.length === 0) return

    rendered.forEach((point, index) => {
      const color = palette[index % palette.length]
      const icon = L.divIcon({
        className: 'status-marker',
        html: `<div style="background:${color};color:#fff;border-radius:50%;width:22px;height:22px;display:flex;align-items:center;justify-content:center;font-size:11px;font-weight:700;border:2px solid #fff;box-shadow:0 1px 3px rgba(0,0,0,.4)">${index + 1}</div>`,
        iconSize: [22, 22],
        iconAnchor: [11, 11],
      })
      const marker = L.marker([point.latitude, point.longitude], { icon })
      marker.bindPopup(`<strong>${statusLabel(point.status)}</strong><br/>${point.externalResourceCode ? `Vehículo: ${point.externalResourceCode}<br/>` : ''}${point.actor ? `Actor: ${point.actor}<br/>` : ''}${new Date(point.occurredAt).toLocaleString('es-ES')}`)
      marker.addTo(map)
      markersRef.current.push(marker)
    })

    if (rendered.length === 1) {
      map.setView([rendered[0].latitude, rendered[0].longitude], 14)
    } else {
      const bounds = L.latLngBounds(rendered.map((point) => [point.latitude, point.longitude] as [number, number]))
      map.fitBounds(bounds, { padding: [32, 32], maxZoom: 15 })
    }
  }, [rendered])

  if (rendered.length === 0) {
    return <div className="status-map-empty">Sin posiciones registradas para este trayecto.</div>
  }

  return <div ref={containerRef} className={`status-map ${className ?? ''}`} role="application" aria-label="Mapa de posiciones de los estados" />
}
