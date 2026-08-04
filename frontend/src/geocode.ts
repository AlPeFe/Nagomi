import type { IncidentLocation } from './types'

interface NominatimAddress {
  road?: string
  house_number?: string
  city?: string
  town?: string
  village?: string
  municipality?: string
}

interface NominatimResult {
  lat: string
  lon: string
  display_name?: string
  address?: NominatimAddress
}

/** Directa: dirección → coordenadas (Nominatim/OSM). */
export async function geocodeAddress(query: string): Promise<IncidentLocation | null> {
  const url = `https://nominatim.openstreetmap.org/search?format=jsonv2&limit=1&accept-language=es&q=${encodeURIComponent(query)}`
  const response = await fetch(url)
  if (!response.ok) return null
  const results = await response.json() as NominatimResult[]
  const first = results[0]
  if (!first) return null
  return {
    latitude: Number(first.lat),
    longitude: Number(first.lon),
    address: first.display_name?.split(',').slice(0, 2).join(','),
  }
}

/** Inversa: coordenadas → dirección (Nominatim/OSM). */
export async function reverseGeocode(latitude: number, longitude: number): Promise<IncidentLocation | null> {
  const url = `https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=${latitude}&lon=${longitude}&accept-language=es&zoom=18`
  const response = await fetch(url)
  if (!response.ok) return null
  const data = await response.json() as NominatimResult
  const address = data.address
  const street = [address?.road, address?.house_number].filter(Boolean).join(' ')
  return {
    latitude,
    longitude,
    address: street || data.display_name?.split(',').slice(0, 2).join(',') || undefined,
    municipality: address?.city ?? address?.town ?? address?.village ?? address?.municipality,
  }
}
