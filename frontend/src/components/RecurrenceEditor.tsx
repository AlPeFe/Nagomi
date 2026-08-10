import type { RecurrencePattern } from '../types'

const weekdays = [['monday', 'Lunes'], ['tuesday', 'Martes'], ['wednesday', 'Miércoles'], ['thursday', 'Jueves'], ['friday', 'Viernes'], ['saturday', 'Sábado'], ['sunday', 'Domingo']] as const
const dayNumbers: Record<string, number> = { sunday: 0, monday: 1, tuesday: 2, wednesday: 3, thursday: 4, friday: 5, saturday: 6 }

type DayKey = (typeof weekdays)[number][0]
interface DayConfig {
  direction: 'outbound' | 'roundTrip'
  appointmentTime: string
  returnTime: string
}

function utcOffset() {
  const minutes = -new Date().getTimezoneOffset(); const sign = minutes >= 0 ? '' : '-'
  return `${sign}${String(Math.floor(Math.abs(minutes) / 60)).padStart(2, '0')}:${String(Math.abs(minutes) % 60).padStart(2, '0')}:00`
}

const emptyDayConfig = (): DayConfig => ({ direction: 'outbound', appointmentTime: '', returnTime: '' })

// RecurrencePattern → state del editor (día → dirección/horas)
function patternToConfigs(value: RecurrencePattern | undefined): Partial<Record<DayKey, DayConfig>> {
  const configs: Partial<Record<DayKey, DayConfig>> = {}
  for (const schedule of value?.weekdaySchedules ?? []) {
    const day = Object.entries(dayNumbers).find(([, number]) => number === schedule.dayOfWeek)?.[0] as DayKey | undefined
    if (!day) continue
    const hasReturn = schedule.returnPickupTime !== undefined || schedule.returnPickupTimePending === true
    configs[day] = {
      direction: hasReturn ? 'roundTrip' : 'outbound',
      appointmentTime: schedule.outboundAppointmentTime ? schedule.outboundAppointmentTime.slice(0, 5) : '',
      returnTime: hasReturn && !schedule.returnPickupTimePending && schedule.returnPickupTime ? schedule.returnPickupTime.slice(0, 5) : '',
    }
  }
  return configs
}

// State del editor → RecurrencePattern
function configsToPattern(configs: Partial<Record<DayKey, DayConfig>>, current: RecurrencePattern | undefined): RecurrencePattern {
  const selectedDays = Object.keys(configs) as DayKey[]
  return {
    startDate: current?.startDate ?? '',
    endDate: current?.endDate ?? '',
    utcOffset: current?.utcOffset ?? utcOffset(),
    weekdaySchedules: selectedDays.map((day) => {
      const cfg = configs[day] ?? emptyDayConfig()
      const hasReturn = cfg.direction === 'roundTrip'
      const pending = hasReturn && !cfg.returnTime
      return {
        dayOfWeek: dayNumbers[day],
        outboundAppointmentTime: cfg.appointmentTime ? `${cfg.appointmentTime}:00` : undefined!,
        returnPickupTime: hasReturn ? (pending ? '23:59:00' : `${cfg.returnTime}:00`) : undefined,
        returnPickupNextDay: false,
        returnPickupTimePending: pending,
      }
    }),
  }
}

export function RecurrenceEditor({ value, onChange }: { value: RecurrencePattern | undefined; onChange: (next: RecurrencePattern) => void }) {
  const configs = patternToConfigs(value)
  const selectedDays = Object.keys(configs) as DayKey[]
  const commit = (next: Partial<Record<DayKey, DayConfig>>) => onChange(configsToPattern(next, value))
  const setBase = (patch: Partial<Pick<RecurrencePattern, 'startDate' | 'endDate' | 'utcOffset'>>) => onChange({ ...configsToPattern(configs, value), ...patch })
  const toggleDay = (day: DayKey, on: boolean) => {
    const next = { ...configs }
    if (on) next[day] = next[day] ?? emptyDayConfig()
    else delete next[day]
    commit(next)
  }
  const setDay = (day: DayKey, patch: Partial<DayConfig>) => commit({ ...configs, [day]: { ...(configs[day] ?? emptyDayConfig()), ...patch } })

  return <div className="recurrence-editor">
    <div className="field-grid">
      <label><span>Desde *</span><input type="date" value={value?.startDate ?? ''} onChange={(e) => setBase({ startDate: e.target.value })} /></label>
      <label><span>Hasta * (máximo 6 meses)</span><input type="date" value={value?.endDate ?? ''} onChange={(e) => setBase({ endDate: e.target.value })} /></label>
    </div>
    <fieldset className="weekday-picker"><legend>Días de servicio *</legend>{weekdays.map(([day, label]) => <label key={day}><input type="checkbox" aria-label={label} checked={selectedDays.includes(day)} onChange={(e) => toggleDay(day, e.target.checked)} /><span>{label.slice(0, 2)}</span><small>{label}</small></label>)}</fieldset>
    {selectedDays.length > 0 && (
      <div className="recurrence-days">
        <div className="recurrence-days-head"><span>Día</span><span>Tipo</span><span>Hora de cita</span><span>Vuelta</span></div>
        {selectedDays.map((day) => {
          const cfg = configs[day] ?? emptyDayConfig()
          const meta = weekdays.find(([value]) => value === day)!
          return (
            <div className="recurrence-day" key={day}>
              <strong>{meta[1]}</strong>
              <select value={cfg.direction} aria-label={`Tipo ${meta[1]}`} onChange={(e) => setDay(day, { direction: e.target.value as 'outbound' | 'roundTrip' })}>
                <option value="outbound">Solo ida</option>
                <option value="roundTrip">Ida y vuelta</option>
              </select>
              <input type="time" aria-label={`Hora de cita ${meta[1]}`} value={cfg.appointmentTime} onChange={(e) => setDay(day, { appointmentTime: e.target.value })} />
              {cfg.direction === 'roundTrip' ? (
                <div className="return-options">
                  <select aria-label={`Vuelta ${meta[1]}`} value={cfg.returnTime ? 'custom' : 'default'} onChange={(e) => {
                    if (e.target.value === 'custom') setDay(day, { returnTime: cfg.returnTime || '12:00' })
                    else setDay(day, { returnTime: '' })
                  }}>
                    <option value="default">Por defecto</option>
                    <option value="custom">Hora específica</option>
                  </select>
                  {cfg.returnTime && <input type="time" aria-label={`Hora de vuelta ${meta[1]}`} value={cfg.returnTime} onChange={(e) => setDay(day, { returnTime: e.target.value })} />}
                </div>
              ) : <span className="return-none">—</span>}
            </div>
          )
        })}
      </div>
    )}
  </div>
}
