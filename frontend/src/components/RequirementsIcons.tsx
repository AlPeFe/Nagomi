import {
  Bed,
  FirstAidKit,
  PersonSimpleWalk,
  Scales,
  Stairs,
  UsersThree,
  Virus,
  Wheelchair,
  Wind,
} from '@phosphor-icons/react'
import type { Icon } from '@phosphor-icons/react'
import type { Requirements } from '../types'

/**
 * Movilidad y requisitos del paciente en iconografía (no texto): el coordinador lee
 * el tipo de traslado de un vistazo. Cada icono lleva `title` y texto para lectores
 * de pantalla, así que la información sigue siendo accesible y exportable.
 */
type RequirementBadge = { key: keyof Requirements; icon: Icon; label: string; short: string }

const MOBILITY: Record<Requirements['mobility'], RequirementBadge> = {
  Autonomous: { key: 'mobility', icon: PersonSimpleWalk, label: 'Va sentado o camina', short: 'Sentado' },
  Wheelchair: { key: 'mobility', icon: Wheelchair, label: 'Silla de ruedas', short: 'Silla' },
  Stretcher: { key: 'mobility', icon: Bed, label: 'Camilla', short: 'Camilla' },
}

const EXTRAS: RequirementBadge[] = [
  { key: 'oxygen', icon: Wind, label: 'Oxígeno', short: 'O₂' },
  { key: 'companion', icon: UsersThree, label: 'Acompañante', short: 'Acompañante' },
  { key: 'medicalStaff', icon: FirstAidKit, label: 'Personal sanitario', short: 'Sanitario' },
  { key: 'isolation', icon: Virus, label: 'Aislamiento', short: 'Aislamiento' },
  { key: 'bariatric', icon: Scales, label: 'Bariátrica', short: 'Bariátrica' },
  { key: 'stairsAssistance', icon: Stairs, label: 'Ayuda en escaleras', short: 'Escaleras' },
]

// Interno: exportarlo rompía el fast refresh del fichero de componentes.
function requirementBadges(requirements: Requirements): RequirementBadge[] {
  const badges = [MOBILITY[requirements.mobility] ?? MOBILITY.Autonomous]
  for (const extra of EXTRAS) if (requirements[extra.key]) badges.push(extra)
  return badges
}

export function RequirementsIcons({ requirements, withText = false, className }: {
  requirements: Requirements
  /** En el detalle del panel rápido se añade el texto corto junto al icono. */
  withText?: boolean
  className?: string
}) {
  const badges = requirementBadges(requirements)
  return <ul className={`req-icons${className ? ` ${className}` : ''}`}>
    {badges.map((badge) => {
      const Icon = badge.icon
      return <li key={`${badge.key}-${badge.label}`} className="req-icon" title={badge.label}>
        <Icon size={14} weight="duotone" aria-hidden="true" />
        <span className="sr-only">{badge.label}</span>
        {withText && <span className="req-icon-text">{badge.short}</span>}
      </li>
    })}
  </ul>
}