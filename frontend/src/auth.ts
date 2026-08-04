import { ApiError } from './api'

const TOKEN_KEY = 'nagomi_token'

export type CurrentUser = {
  id: string
  name: string
  email?: string
  displayName?: string
  roles: string[]
}

type MeResponse = {
  id: string
  name: string
  email?: string
  displayName?: string
  roles: string[]
}

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function isAuthenticated(): boolean {
  return !!getToken()
}

export function hasRole(role: string): boolean {
  const stored = sessionStorage.getItem('nagomi_roles')
  if (stored) return (JSON.parse(stored) as string[]).includes(role)
  return false
}

export function rememberUser(user: CurrentUser) {
  sessionStorage.setItem('nagomi_roles', JSON.stringify(user.roles))
}

export async function login(username: string, password: string): Promise<CurrentUser> {
  const form = new URLSearchParams({ grant_type: 'password', username, password })
  const response = await fetch('/connect/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: form,
  })
  if (!response.ok) {
    let detail = 'Credenciales incorrectas o usuario desactivado.'
    try {
      const body = (await response.json()) as { error_description?: string }
      if (body.error_description) detail = body.error_description
    } catch {
      /* keep default */
    }
    throw new ApiError(detail, response.status)
  }
  const body = (await response.json()) as { access_token: string }
  localStorage.setItem(TOKEN_KEY, body.access_token)
  const user = await me()
  rememberUser(user)
  return user
}

export async function me(): Promise<CurrentUser> {
  const response = await fetch('/api/auth/me', {
    headers: { Authorization: `Bearer ${getToken() ?? ''}` },
  })
  if (!response.ok) throw new ApiError('La sesión no es válida.', response.status)
  const body = (await response.json()) as MeResponse
  const user: CurrentUser = {
    id: body.id,
    name: body.name,
    email: body.email,
    displayName: body.displayName,
    roles: body.roles ?? [],
  }
  rememberUser(user)
  return user
}

export function logout() {
  localStorage.removeItem(TOKEN_KEY)
  sessionStorage.removeItem('nagomi_roles')
}
