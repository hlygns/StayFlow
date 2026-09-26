import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'
import { setSession } from '../api/session'

afterEach(() => {
  cleanup()
  setSession(null) // oturum modul seviyesinde bellekte - testler arasinda sifirla
})
