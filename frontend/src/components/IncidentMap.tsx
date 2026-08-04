import { useEffect, useRef } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import { reverseGeocode } from '../geocode'
import type { IncidentLocation } from '../types'

const OSM_TILES = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png'
const OSM_ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
const DEFAULT_VIEW: [number, number] = [41.3851, 2.1734]

interface IncidentMapProps {
  value?: IncidentLocation
  onChange: (location: IncidentLocation | null) => void
}

export function IncidentMap({ value, onChange }: IncidentMapProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const mapRef = useRef<L.Map | null>(null)
  const markerRef = useRef<L.Marker | null>(null)
  const onChangeRef = useRef(onChange)
  onChangeRef.current = onChange

  useEffect(() => {
    if (!containerRef.current || mapRef.current) return
    const map = L.map(containerRef.current).setView(DEFAULT_VIEW, 13)
    L.tileLayer(OSM_TILES, { attribution: OSM_ATTRIBUTION, maxZoom: 19 }).addTo(map)
    const marker = L.marker(DEFAULT_VIEW, { draggable: true }).addTo(map)
    mapRef.current = map
    markerRef.current = marker

    const place = (latitude: number, longitude: number) => {
      marker.setLatLng([latitude, longitude])
      map.panTo([latitude, longitude])
      void reverseGeocode(latitude, longitude)
        .then((location) => onChangeRef.current(location ?? { latitude, longitude }))
        .catch(() => onChangeRef.current({ latitude, longitude }))
    }

    map.on('click', (event: L.LeafletMouseEvent) => place(event.latlng.lat, event.latlng.lng))
    marker.on('dragend', () => {
      const position = marker.getLatLng()
      onChangeRef.current({ latitude: position.lat, longitude: position.lng })
    })

    return () => {
      map.remove()
      mapRef.current = null
      markerRef.current = null
    }
  }, [])

  const latitude = value?.latitude
  const longitude = value?.longitude
  useEffect(() => {
    if (!mapRef.current || !markerRef.current || latitude === undefined || longitude === undefined) return
    markerRef.current.setLatLng([latitude, longitude])
    mapRef.current.panTo([latitude, longitude])
  }, [latitude, longitude])

  return <div ref={containerRef} className="incident-map" role="application" aria-label="Mapa del punto de incidencia" />
}
