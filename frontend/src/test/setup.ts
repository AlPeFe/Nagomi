import '@testing-library/jest-dom/vitest'
import { afterEach, beforeEach, vi } from 'vitest'
import { cleanup } from '@testing-library/react'

beforeEach(() => {
  // Authenticated session so guarded routes render in component tests.
  localStorage.setItem('nagomi_token', 'test-token')
  sessionStorage.setItem('nagomi_roles', JSON.stringify(['admin']))
})

afterEach(() => {
  cleanup()
  localStorage.clear()
  sessionStorage.clear()
  vi.restoreAllMocks()
})
