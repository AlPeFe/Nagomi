import { useEffect, useRef, useState } from 'react'
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
  const f = (n: string) => `${prefix}${cap(n)}`

  const [type, setType] = useState<'HealthcareFacility' | 'PrivateAddress'>('HealthcareFacility')
  const [provinces, setProvinces] = useState<Option[]>([])
  const [provinceCode, setProvinceCode] = useState('')
  const [municipalities, setMunicipalities] = useState<Option[]>([])
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

  // App writes the type into a hidden input the form submit reads
  const typeField = f('Type')

  // load all provinces once
  useEffect(() => {
    let alive = true
    api.listProvinces().then((p) => { if (alive) setProvinces(p) }).catch(() => setProvinces([]))
    return () => { alive = false }
  }, [])

  // on type change reflect to hidden field
  useEffect(() => {
    const el = document.querySelector<HTMLInputElement>(`input[name="${typeField}"]`)
    if (el) el.value = type
  }, [type, typeField])

  // load municipalities filtered by province
  useEffect(() => {
    let alive = true
    if (!provinceCode) { setMunicipalities([]); return }
    api.listMunicipalities(provinceCode).then((m) => { if (alive) setMunicipalities(m) }).catch(() => setMunicipalities([]))
    return () => { alive = false }
  }, [provinceCode])

  // debounced facility search
  useEffect(() => {
    if (type !== 'HealthcareFacility' || !facilityQuery.trim()) { setFacilities([]); return }
    let alive = true
    setMsg('Buscando centros…')
    const t = setTimeout(() => {
      api.searchHealthcareFacilities(facilityQuery.trim(), undefined, 30)
        .then((f) => { if (alive) { setFacilities(f); setMsg('') } })
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

  function pickFacility(x: Facility) {
    setFacilityQuery(x.name); setFacilities([]); setFacilityOpen(false)
    setHidden(f('Name'), x.name)
    setHidden(f('Address'), x.officialAddressText ?? '')
    setHidden(f('Municipality'), x.address?.municipalityCode ?? '')
    setHidden(f('Phone'), x.phone ?? '')
    setHidden(f('Observations'), '')
  }

  function pickMunicipality(code: string) {
    const m = municipalities.find((x) => x.code === code)
    setHidden(f('Name'), m?.name ?? '')
    setHidden(f('Municipality'), m?.name ?? code)
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

      {type === 'HealthcareFacility' ? (
        <>
          <label>
            <span>Centro sanitario (catálogo nacional) *</span>
            <input
              ref={searchRef}
              name={f('Name')}
              required
              autoComplete="off"
              placeholder="Escribe un hospital o centro…"
              value={facilityQuery}
              onChange={(e) => { setFacilityQuery(e.target.value); setFacilityOpen(true) }}
              onFocus={() => setFacilityOpen(true)}
            />
            {msg && <small>{msg}</small>}
          </label>
          {facilityOpen && (facilityQuery.trim().length >= 3) && (
            <div className="facility-dropdown" ref={listRef}>
              {facilities.length === 0 && <div className="facility-empty">Sin coincidencias: escribe más letras</div>}
              {facilities.map((x) => (
                <button type="button" key={x.publicId} className="facility-option" onClick={() => pickFacility(x)}>
                  <strong>{x.name}</strong>
                  {x.officialAddressText && <span>{x.officialAddressText}</span>}
                </button>
              ))}
            </div>
          )}
          <label><span>Municipio</span><input name={f('Municipality')} placeholder="Se asigna del centro" readOnly /></label>
          <label><span>Dirección</span><input name={f('Address')} placeholder="Se autocompleta" readOnly /></label>
          <label><span>Indicaciones</span><textarea name={f('Observations')} rows={2} /></label>
        </>
      ) : (
        <>
          <label>
            <span>Provincia *</span>
            <select name={f('Province')} required defaultValue="" onChange={(e) => setProvinceCode(e.target.value)}>
              <option value="" disabled>Selecciona provincia</option>
              {provinces.map((p) => <option key={p.code} value={p.code}>{p.name}</option>)}
            </select>
          </label>
          <label>
            <span>Población *</span>
            <select name={f('MunicipalitySelect')} required defaultValue="" onChange={(e) => pickMunicipality(e.target.value)}>
              <option value="" disabled>{provinceCode ? 'Selecciona población' : 'Primero elige provincia'}</option>
              {municipalities.map((m) => <option key={m.code} value={m.code}>{m.name}</option>)}
            </select>
          </label>
          <input type="hidden" name={f('Municipality')} />
          <input type="hidden" name={f('Name')} />
          <label><span>Dirección</span><input name={f('Address')} placeholder="Calle, número" /></label>
          <label><span>Indicaciones</span><textarea name={f('Observations')} rows={2} /></label>
        </>
      )}
    </fieldset>
  )
}
