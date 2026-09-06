import { useEffect, useState } from 'react'

/**
 * True when the viewport is narrower than the breakpoint (default 768px).
 * Lazy-initialized so SSR/jsdom (innerWidth 1024) renders the desktop branch.
 */
export function useIsMobile(breakpoint = 768): boolean {
  const [isMobile, setIsMobile] = useState(
    () => typeof window !== 'undefined' && window.innerWidth < breakpoint,
  )

  useEffect(() => {
    const onResize = () => setIsMobile(window.innerWidth < breakpoint)
    window.addEventListener('resize', onResize)
    return () => window.removeEventListener('resize', onResize)
  }, [breakpoint])

  return isMobile
}
