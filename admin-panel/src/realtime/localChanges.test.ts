import { describe, expect, it } from 'vitest'
import { markChangedLocally, wasChangedLocally } from './localChanges'

describe('localChanges', () => {
  it('bu sekmede degistirilen rezervasyon kisa sure boyunca taninir, sonra unutulur', () => {
    const now = Date.now()
    markChangedLocally('r1')

    expect(wasChangedLocally('r1', now)).toBe(true)
    expect(wasChangedLocally('baska', now)).toBe(false)
    expect(wasChangedLocally('r1', now + 60_000)).toBe(false)
  })
})
