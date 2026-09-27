import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { router, useFocusEffect } from 'expo-router'
import { useCallback, useState } from 'react'
import { FlatList, Pressable, Text, View } from 'react-native'
import { publicApi } from '../../api/client'
import type { GuestReservation } from '../../api/types'
import { Button, Card, Empty, ErrorBox, Field, Loading, StatusPill, styles } from '../../components/ui'
import { loadBookings, saveBooking, type SavedBooking } from '../../lib/bookings'
import { formatRange } from '../../lib/format'
import { colors, space } from '../../lib/theme'

function BookingRow({ booking, live }: { booking: SavedBooking; live?: GuestReservation }) {
  return (
    <Pressable
      accessibilityRole="button"
      onPress={() => router.push({ pathname: '/reservation/[code]', params: { code: booking.code } })}
      style={({ pressed }) => [styles.card, { gap: space(1.5) }, pressed && { opacity: 0.9 }]}
    >
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', gap: space(2) }}>
          <Text style={[styles.h2, { flex: 1 }]} numberOfLines={1}>
            {booking.hotelName}
          </Text>
          {live && <StatusPill status={live.status} />}
        </View>
        <Text style={styles.muted}>{live ? `${formatRange(live.checkIn, live.checkOut)} · ${live.roomTypeName}` : booking.checkIn}</Text>
        <Text style={{ fontFamily: 'monospace', letterSpacing: 2, color: colors.textMuted }}>{booking.code}</Text>
    </Pressable>
  )
}

/** Baska bir cihazda (ya da web'de) yapilmis rezervasyonu kod + e-posta ile bu telefona ekler. */
function FindByCode({ onFound }: { onFound: () => void }) {
  const [code, setCode] = useState('')
  const [email, setEmail] = useState('')
  const find = useMutation({
    mutationFn: () => publicApi.reservation(code.trim(), email.trim()),
    onSuccess: async (r) => {
      await saveBooking({ code: r.confirmationCode, email: email.trim(), hotelName: r.hotelName, checkIn: r.checkIn })
      setCode('')
      setEmail('')
      onFound()
    },
  })

  return (
    <Card style={{ gap: space(3) }}>
      <Text style={styles.h2}>Kod ile rezervasyon ekle</Text>
      <Field label="Rezervasyon kodu" value={code} onChangeText={(t) => setCode(t.toUpperCase())} autoCapitalize="characters" autoCorrect={false} placeholder="K7M2QX9A" />
      <Field label="E-posta" value={email} onChangeText={setEmail} autoCapitalize="none" keyboardType="email-address" />
      <ErrorBox error={find.error} />
      <Button title="Ekle" variant="secondary" loading={find.isPending} disabled={code.trim().length < 8 || !email.includes('@')} onPress={() => find.mutate()} />
    </Card>
  )
}

export default function BookingsScreen() {
  const queryClient = useQueryClient()
  const bookings = useQuery({
    queryKey: ['bookings'],
    queryFn: async () => {
      const saved = await loadBookings()
      // Her kaydin guncel durumunu sunucudan al (biri basarisizsa digerleri yine gorunsun)
      const live = await Promise.all(saved.map((b) => publicApi.reservation(b.code, b.email).catch(() => undefined)))
      return saved.map((booking, i) => ({ booking, live: live[i] }))
    },
  })

  // Sekmeye her donuste tazele (yeni rezervasyon yapilmis ya da durum degismis olabilir)
  useFocusEffect(
    useCallback(() => {
      void queryClient.invalidateQueries({ queryKey: ['bookings'] })
    }, [queryClient]),
  )

  return (
    <FlatList
      data={bookings.data ?? []}
      keyExtractor={(item) => item.booking.code}
      renderItem={({ item }) => <BookingRow booking={item.booking} live={item.live} />}
      contentContainerStyle={{ padding: space(4), gap: space(3) }}
      refreshing={bookings.isRefetching}
      onRefresh={() => bookings.refetch()}
      ListHeaderComponent={<ErrorBox error={bookings.error} />}
      ListEmptyComponent={bookings.isLoading ? <Loading /> : <Empty title="Henüz rezervasyonunuz yok" text="Keşfet sekmesinden bir otel seçin." />}
      ListFooterComponent={<View style={{ marginTop: space(4) }}><FindByCode onFound={() => void bookings.refetch()} /></View>}
    />
  )
}
