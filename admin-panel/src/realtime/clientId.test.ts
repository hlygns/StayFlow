import { describe, expect, it } from 'vitest'
import { CLIENT_ID, isOwnChange } from './clientId'

describe('clientId', () => {
  it('sadece bu sekmenin yaptigi degisiklik "kendi degisikligi" sayilir', () => {
    expect(isOwnChange(CLIENT_ID)).toBe(true)
    expect(isOwnChange('baska-sekme')).toBe(false)
    // Sunucunun kendi yaptigi degisiklik (odeme suresi doldu vb.) herkese gosterilir
    expect(isOwnChange(null)).toBe(false)
    expect(isOwnChange(undefined)).toBe(false)
  })
})
