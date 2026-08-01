import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from '../api'

interface Option { code: string; name: string }
interface Facility {
  publicId: string
  name: string
  officialAddressText?: string
  phone?: string
  address?: { municipalityCode?: string; provinceCode?: string; autonomousCommunityCode?: string }
}

const cap = (s: string) => s.charAt(0).toUpperCase() + s.slice(1)

export function LocationFieldset({ prefix, legend }: { prefix: 'origin' | 'destination'; legend: string }) {
  const f = useCallback((n: string) => `${prefix}${cap(n)}`, [prefix])

  const [type, setType] = useState<'HealthcareFacility' | 'PrivateAddress'>('HealthcareFacility')
  const [provinces, setProvinces] = useState<Option[]>([])
  const [provinceCode, setProvinceCode] = useState('')
  const [municipalities, setMunicipalities] = useState<Option[]>([])
  const [municipalityCode, setMunicipalityCode] = useState('')
  const [facilityQuery, setFacilityQuery] = useState('')
  const [facilities, setFacilities] = useState<Facility[]>([])
  const [facilityOpen, setFacilityOpen] = useState(false)
  const [msg, setMsg] = useState('')

  const searchRef = useRef<HTMLInputElement>(null)
  const listRef = useRef<HTMLDivElement>(null)

  const setHidden = (name: string, value: string) => {
    const el = document.querySelector<HTMLInputElement>(`input[name="${name}"]`)
    if (el) el.value = value
  }

  const typeField = f('Type')

  // load all provinces once
  useEffect(() => {
    let alive = true
    api.listProvinces().then((p) => { if (alive) setProvinces(p) }).catch(() => setProvinces([]))
    return () => { alive = false }
  }, [])

  // reflect type to hidden field
  useEffect(() => {
    const el = document.querySelector<HTMLInputElement>(`input[name="${typeField}"]`)
    if (el) el.value = type
  }, [type, typeField])

  // load municipalities filtered by province
  useEffect(() => {
    let alive = true
    if (!provinceCode) { setMunicipalities([]); setMunicipalityCode(''); return }
    api.listMunicipalities(provinceCode).then((m) => { if (alive) setMunicipalities(m) }).catch(() => setMunicipalities([]))
    return () => { alive = false }
  }, [provinceCode])

  // when a facility is picked, keep the municipality select in sync once municipalities load
  useEffect(() => {
    if (municipalityCode) {
      const m = municipalities.find((x) => x.code === municipalityCode)
      if (m) setHidden(f('Municipality'), m.name)
    }
  }, [municipalities, municipalityCode, f])

  // debounced facility search (only in center mode)
  useEffect(() => {
    if (type !== 'HealthcareFacility' || !facilityQuery.trim() || facilityQuery.trim().length < 3) { setFacilities([]); return }
    let alive = true
    setMsg('Buscando centros…')
    const t = setTimeout(() => {
      api.searchHealthcareFacilities(facilityQuery.trim(), undefined, 30)
        .then((list) => { if (alive) { setFacilities(list); setMsg('') } })
        .catch(() => { if (alive) { setFacilities([]); setMsg('Error al buscar centros') } })
    }, 350)
    return () => { alive = false; clearTimeout(t) }
  }, [facilityQuery, type])

  // outside click closes dropdown
  useEffect(() => {
    function onClick(e: MouseEvent) {
      if (searchRef.current && !searchRef.current.contains(e.target as Node) &&
          listRef.current && !listRef.current.contains(e.target as Node)) setFacilityOpen(false)
    }
    document.addEventListener('mousedown', onClick)
    return () => document.removeEventListener('mousedown', onClick)
  }, [])

  // pick a center from the catalog: fill name + autofill province/municipality
  function pickFacility(x: Facility) {
    setFacilityQuery(x.name); setFacilities([]); setFacilityOpen(false)
    setHidden(f('Name'), x.name)
    setHidden(f('Address'), x.officialAddressText ?? '')
    setHidden(f('Phone'), x.phone ?? '')
    if (x.address?.provinceCode) setProvinceCode(x.address.provinceCode)
    if (x.address?.municipalityCode) setMunicipalityCode(x.address.municipalityCode)
    else setHidden(f('Municipality'), '')
  }

  // keep the typed center name in the hidden field so free entry works without picking from the list
  function onCenterInput(value: string) {
    setFacilityQuery(value); setFacilityOpen(true)
    setHidden(f('Name'), value)
  }

  function onProvinceChange(code: string) {
    setProvinceCode(code); setMunicipalityCode(''); setHidden(f('Municipality'), '')
  }

  function onMunicipalityChange(code: string) {
    setMunicipalityCode(code)
    const m = municipalities.find((x) => x.code === code)
    setHidden(f('Municipality'), m?.name ?? '')
    if (type === 'PrivateAddress') setHidden(f('Name'), m?.name ?? '')
  }

  return (
    <fieldset>
      <legend>{legend}</legend>

      <label>
        <span>Tipo *</span>
        <select defaultValue="HealthcareFacility" onChange={(e) => setType(e.target.value as 'HealthcareFacility' | 'PrivateAddress')}>
          <option value="HealthcareFacility">Centro sanitario</option>
          <option value="PrivateAddress">Domicilio</option>
        </select>
      </label>
      <input type="hidden" name={typeField} value={type} />
      <input type="hidden" name={f('Phone')} />
      <input type="hidden" name={f('Municipality')} />

      <label>
        <span>Provincia *</span>
        <select name={f('Province')} required value={provinceCode} onChange={(e) => onProvinceChange(e.target.value)}>
          <option value="" disabled>Selecciona provincia</option>
          {provinces.map((p) => <option key={p.code} value={p.code}>{p.name}</option>)}
        </select>
      </label>

      <label>
        <span>Población *</span>
        <select name={f('MunicipalitySelect')} required value={municipalityCode} onChange={(e) => onMunicipalityChange(e.target.value)}>
          <option value="" disabled>{provinceCode ? 'Selecciona población' : 'Primero elige provincia'}</option>
          {municipalities.map((m) => <option key={m.code} value={m.code}>{m.name}</option>)}
        </select>
      </label>

      {type === 'HealthcareFacility' ? (
        <>
          <label>
            <span>Centro sanitario (catálogo nacional) *</span>
            <input
              ref={searchRef}
              name={f('Name')}
              required
              autoComplete="off"
              placeholder="Busca un hospital o escribe uno manualmente…"
              value={facilityQuery}
              onChange={(e) => onCenterInput(e.target.value)}
              onFocus={() => setFacilityOpen(true)}
            />
            {msg && <small>{msg}</small>}
          </label>
          <small className="hint">Puedes escribir un centro a mano aunque no esté en el listado.</small>
          {facilityOpen && (facilityQuery.trim().length >= 3) && (
            <div className="facility-dropdown" ref={listRef}>
              {facilities.length === 0 && <div className="facility-empty">Sin coincidencias en el catálogo (puedes seguir escribiendo)</div>}
              {facilities.map((x) => (
                <button type="button" key={x.publicId} className="facility-option" onClick={() => pickFacility(x)}>
                  <strong>{x.name}</strong>
                  {x.officialAddressText && <span>{x.officialAddressText}</span>}
                </button>
              ))}
            </div>
          )}
          <label><span>Dirección</span><input name={f('Address')} placeholder="Se autocompleta del centro" /></label>
        </>
      ) : (
        <>
          <input type="hidden" name={f('Name')} />
          <label><span>Dirección</span><input name={f('Address')} placeholder="Calle, número" /></label>
        </>
      )}

      <label><span>Indicaciones</span><textarea name={f('Observations')} rows={2} /></label>
    </fieldset>
  )
}
