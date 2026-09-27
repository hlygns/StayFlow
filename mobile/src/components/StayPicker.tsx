import { useMemo } from 'react'
import { Pressable, ScrollView, Text, View } from 'react-native'
import type { IsoDate } from '../api/types'
import { addDays, formatRange, formatShort, formatWeekday, today } from '../lib/format'
import { colors, radius, space } from '../lib/theme'
import { styles } from './ui'

const DAYS_AHEAD = 60
const MAX_NIGHTS = 30 // backend: StayDates.MaxNights

function Stepper({ label, value, min, max, onChange }: { label: string; value: number; min: number; max: number; onChange: (v: number) => void }) {
  const button = (text: string, next: number, a11y: string) => (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={a11y}
      disabled={next < min || next > max}
      onPress={() => onChange(next)}
      style={{
        width: 40,
        height: 40,
        borderRadius: 20,
        borderWidth: 1,
        borderColor: colors.border,
        alignItems: 'center',
        justifyContent: 'center',
        opacity: next < min || next > max ? 0.35 : 1,
      }}
    >
      <Text style={{ fontSize: 20, color: colors.text }}>{text}</Text>
    </Pressable>
  )
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
      <Text style={styles.label}>{label}</Text>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: space(3) }}>
        {button('−', value - 1, `${label} azalt`)}
        <Text accessibilityLiveRegion="polite" style={{ minWidth: 24, textAlign: 'center', fontSize: 16, fontWeight: '600' }}>
          {value}
        </Text>
        {button('+', value + 1, `${label} artır`)}
      </View>
    </View>
  )
}

/**
 * Giris gunu (yatay gun seridi) + gece sayisi + kisi sayisi. Yerel takvim bileseni yerine bu secildi:
 * iOS, Android ve web'de ayni gorunur, tek elle kullanilir ve ek native paket gerektirmez.
 */
export function StayPicker({
  checkIn,
  nights,
  guests,
  onChange,
}: {
  checkIn: IsoDate
  nights: number
  guests: number
  onChange: (value: { checkIn: IsoDate; nights: number; guests: number }) => void
}) {
  const days = useMemo(() => Array.from({ length: DAYS_AHEAD }, (_, i) => addDays(today(), i)), [])

  return (
    <View style={{ gap: space(4) }}>
      <View style={{ gap: space(2) }}>
        <Text style={styles.label}>Giriş günü</Text>
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: space(2) }}>
          {days.map((day) => {
            const selected = day === checkIn
            return (
              <Pressable
                key={day}
                accessibilityRole="button"
                accessibilityState={{ selected }}
                accessibilityLabel={`${formatWeekday(day)} ${formatShort(day)}`}
                onPress={() => onChange({ checkIn: day, nights, guests })}
                style={{
                  width: 56,
                  paddingVertical: space(2),
                  borderRadius: radius.md,
                  alignItems: 'center',
                  backgroundColor: selected ? colors.brand : colors.surface,
                  borderWidth: 1,
                  borderColor: selected ? colors.brand : colors.border,
                }}
              >
                <Text style={{ fontSize: 12, color: selected ? '#fff' : colors.textMuted }}>{formatWeekday(day)}</Text>
                <Text style={{ fontSize: 15, fontWeight: '700', color: selected ? '#fff' : colors.text }}>{formatShort(day).split(' ')[0]}</Text>
                <Text style={{ fontSize: 11, color: selected ? '#fff' : colors.textMuted }}>{formatShort(day).split(' ')[1]}</Text>
              </Pressable>
            )
          })}
        </ScrollView>
      </View>
      <Stepper label="Gece" value={nights} min={1} max={MAX_NIGHTS} onChange={(n) => onChange({ checkIn, nights: n, guests })} />
      <Stepper label="Kişi" value={guests} min={1} max={8} onChange={(g) => onChange({ checkIn, nights, guests: g })} />
      <Text style={styles.muted}>{formatRange(checkIn, addDays(checkIn, nights))}</Text>
    </View>
  )
}
