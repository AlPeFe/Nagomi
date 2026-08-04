import { Children, createContext, isValidElement, useContext, useEffect, useState } from 'react'
import type { AnchorHTMLAttributes, MouseEvent, ReactNode } from 'react'

type NavigateOptions = { replace?: boolean }
type Navigator = (to: string | number, options?: NavigateOptions) => void
type RouterValue = { location: string; navigate: Navigator; params: Record<string, string> }

const RouterContext = createContext<RouterValue | null>(null)

function useRouter() {
  const router = useContext(RouterContext)
  if (!router) throw new Error('Router components must be rendered inside a router')
  return router
}

function browserLocation() {
  return `${window.location.pathname}${window.location.search}${window.location.hash}`
}

export function BrowserRouter({ children }: { children: ReactNode }) {
  const [location, setLocation] = useState(browserLocation)
  useEffect(() => {
    const update = () => setLocation(browserLocation())
    window.addEventListener('popstate', update)
    return () => window.removeEventListener('popstate', update)
  }, [])
  const navigate: Navigator = (to, options) => {
    if (typeof to === 'number') window.history.go(to)
    else {
      window.history[options?.replace ? 'replaceState' : 'pushState'](null, '', to)
      setLocation(browserLocation())
    }
  }
  return <RouterContext value={{ location, navigate, params: {} }}>{children}</RouterContext>
}

export function MemoryRouter({ children, initialEntries = ['/'] }: { children: ReactNode; initialEntries?: string[] }) {
  const [history] = useState(() => ({ entries: [...initialEntries], index: initialEntries.length - 1 }))
  const [location, setLocation] = useState(history.entries[history.index] ?? '/')
  const navigate: Navigator = (to, options) => {
    if (typeof to === 'number') history.index = Math.max(0, Math.min(history.entries.length - 1, history.index + to))
    else if (options?.replace) history.entries[history.index] = to
    else {
      history.entries.splice(++history.index, Infinity, to)
    }
    setLocation(history.entries[history.index])
  }
  return <RouterContext value={{ location, navigate, params: {} }}>{children}</RouterContext>
}

type LinkProps = Omit<AnchorHTMLAttributes<HTMLAnchorElement>, 'href'> & { to: string }

function followLink(event: MouseEvent<HTMLAnchorElement>, navigate: Navigator, to: string) {
  if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || (event.currentTarget.target && event.currentTarget.target !== '_self')) return
  event.preventDefault()
  navigate(to)
}

export function Link({ to, onClick, ...props }: LinkProps) {
  const { navigate } = useRouter()
  return <a {...props} href={to} onClick={(event) => { onClick?.(event); followLink(event, navigate, to) }} />
}

export function NavLink({ to, ...props }: LinkProps) {
  const { location } = useRouter()
  const pathname = location.split(/[?#]/, 1)[0].replace(/\/$/, '') || '/'
  const target = to.replace(/\/$/, '') || '/'
  const active = pathname === target || (target !== '/' && pathname.startsWith(`${target}/`))
  return <Link {...props} to={to} aria-current={active ? 'page' : props['aria-current']} />
}

type RouteProps = { path: string; element: ReactNode }

export function Route(_props: RouteProps) {
  return null
}

function match(pattern: string, pathname: string) {
  if (pattern === '*') return {}
  const patternParts = pattern.split('/').filter(Boolean)
  const pathParts = pathname.split('/').filter(Boolean)
  if (patternParts.length !== pathParts.length) return null
  const params: Record<string, string> = {}
  for (let index = 0; index < patternParts.length; index++) {
    const part = patternParts[index]
    if (part.startsWith(':')) {
      try { params[part.slice(1)] = decodeURIComponent(pathParts[index]) } catch { params[part.slice(1)] = pathParts[index] }
    } else if (part !== pathParts[index]) return null
  }
  return params
}

export function Routes({ children }: { children: ReactNode }) {
  const router = useRouter()
  const pathname = router.location.split(/[?#]/, 1)[0].replace(/\/$/, '') || '/'
  for (const child of Children.toArray(children)) {
    if (!isValidElement<RouteProps>(child)) continue
    const params = match(child.props.path, pathname)
    if (params) return <RouterContext value={{ ...router, params }}>{child.props.element}</RouterContext>
  }
  return null
}

export function Navigate({ to, replace = false }: { to: string; replace?: boolean }) {
  const { navigate } = useRouter()
  useEffect(() => navigate(to, { replace }), [navigate, replace, to])
  return null
}

// Hooks intentionally live with their private context and router components.
// eslint-disable-next-line react/only-export-components
export function useNavigate() {
  return useRouter().navigate
}

// eslint-disable-next-line react/only-export-components
export function useLocation() {
  return useRouter().location
}

// eslint-disable-next-line react/only-export-components
export function useParams<T extends Record<string, string | undefined> = Record<string, string>>() {
  return useRouter().params as T
}
