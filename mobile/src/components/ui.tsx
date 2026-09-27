import type { ReactNode } from 'react'
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
  type StyleProp,
  type TextInputProps,
  type ViewStyle,
} from 'react-native'
import type { ReservationStatus } from '../api/types'
import { statusLabels } from '../lib/format'
import { colors, radius, space } from '../lib/theme'

export function Card({ children, style }: { children: ReactNode; style?: StyleProp<ViewStyle> }) {
  return <View style={[styles.card, style]}>{children}</View>
}

type ButtonVariant = 'primary' | 'secondary' | 'danger'

export function Button({
  title,
  onPress,
  variant = 'primary',
  loading,
  disabled,
  style,
}: {
  title: string
  onPress: () => void
  variant?: ButtonVariant
  loading?: boolean
  disabled?: boolean
  style?: StyleProp<ViewStyle>
}) {
  const inactive = disabled || loading
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled: inactive, busy: loading }}
      onPress={onPress}
      disabled={inactive}
      style={({ pressed }) => [
        styles.button,
        variant === 'primary' && styles.buttonPrimary,
        variant === 'secondary' && styles.buttonSecondary,
        variant === 'danger' && styles.buttonDanger,
        pressed && { opacity: 0.85 },
        inactive && { opacity: 0.5 },
        style,
      ]}
    >
      {loading ? (
        <ActivityIndicator color={variant === 'primary' ? '#fff' : colors.brand} />
      ) : (
        <Text style={[styles.buttonText, variant !== 'primary' && { color: variant === 'danger' ? colors.danger : colors.text }]}>{title}</Text>
      )}
    </Pressable>
  )
}

export function Field({ label, error, hint, ...props }: TextInputProps & { label: string; error?: string; hint?: string }) {
  return (
    <View style={{ gap: space(1.5) }}>
      <Text style={styles.label}>{label}</Text>
      <TextInput
        accessibilityLabel={label}
        placeholderTextColor={colors.textMuted}
        style={[styles.input, error ? { borderColor: colors.danger } : null]}
        {...props}
      />
      {error ? <Text style={styles.error}>{error}</Text> : hint ? <Text style={styles.hint}>{hint}</Text> : null}
    </View>
  )
}

export function Stars({ rating, size = 14 }: { rating: number; size?: number }) {
  return (
    <Text accessibilityLabel={`5 üzerinden ${rating} yıldız`} style={{ fontSize: size, letterSpacing: 1 }}>
      <Text style={{ color: colors.star }}>{'★'.repeat(Math.round(rating))}</Text>
      <Text style={{ color: '#CBD5E1' }}>{'★'.repeat(5 - Math.round(rating))}</Text>
    </Text>
  )
}

const statusTone: Record<ReservationStatus, { bg: string; fg: string }> = {
  Pending: { bg: colors.warningSoft, fg: colors.warning },
  Confirmed: { bg: colors.successSoft, fg: colors.success },
  CheckedIn: { bg: '#F0F9FF', fg: '#0369A1' },
  CheckedOut: { bg: '#F1F5F9', fg: '#475569' },
  Cancelled: { bg: colors.dangerSoft, fg: colors.danger },
}

export function StatusPill({ status }: { status: ReservationStatus }) {
  const tone = statusTone[status]
  return (
    <View style={[styles.pill, { backgroundColor: tone.bg }]}>
      <Text style={{ color: tone.fg, fontSize: 12, fontWeight: '600' }}>{statusLabels[status]}</Text>
    </View>
  )
}

export function ErrorBox({ error }: { error: unknown }) {
  if (!error) return null
  return (
    <View accessibilityRole="alert" style={styles.errorBox}>
      <Text style={{ color: colors.danger }}>{error instanceof Error ? error.message : 'Bir şeyler ters gitti.'}</Text>
    </View>
  )
}

export function Loading() {
  return (
    <View style={{ padding: space(10), alignItems: 'center' }}>
      <ActivityIndicator color={colors.brand} size="large" />
    </View>
  )
}

export function Empty({ title, text, action }: { title: string; text?: string; action?: ReactNode }) {
  return (
    <View style={{ alignItems: 'center', padding: space(8), gap: space(2) }}>
      <Text style={{ fontSize: 16, fontWeight: '600', color: colors.text, textAlign: 'center' }}>{title}</Text>
      {text && <Text style={{ color: colors.textMuted, textAlign: 'center' }}>{text}</Text>}
      {action}
    </View>
  )
}

export const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.surface,
    borderRadius: radius.lg,
    padding: space(4),
    borderWidth: 1,
    borderColor: colors.border,
  },
  button: { height: 48, borderRadius: radius.md, alignItems: 'center', justifyContent: 'center', paddingHorizontal: space(4) },
  buttonPrimary: { backgroundColor: colors.brand },
  buttonSecondary: { backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border },
  buttonDanger: { backgroundColor: colors.surface, borderWidth: 1, borderColor: '#FECACA' },
  buttonText: { color: '#fff', fontWeight: '600', fontSize: 16 },
  label: { fontSize: 14, fontWeight: '600', color: colors.text },
  input: {
    height: 48,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surface,
    paddingHorizontal: space(3),
    fontSize: 16,
    color: colors.text,
  },
  error: { color: colors.danger, fontSize: 13 },
  hint: { color: colors.textMuted, fontSize: 13 },
  pill: { alignSelf: 'flex-start', borderRadius: 999, paddingHorizontal: space(2.5), paddingVertical: space(1) },
  errorBox: { backgroundColor: colors.dangerSoft, borderRadius: radius.md, padding: space(3), borderWidth: 1, borderColor: '#FECACA' },
  h1: { fontSize: 26, fontWeight: '700', color: colors.text },
  h2: { fontSize: 18, fontWeight: '700', color: colors.text },
  muted: { color: colors.textMuted, fontSize: 14 },
  body: { color: colors.text, fontSize: 15, lineHeight: 22 },
})
