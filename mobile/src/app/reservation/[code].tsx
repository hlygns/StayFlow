import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useLocalSearchParams } from 'expo-router'
import { useEffect, useState } from 'react'
import { Alert, Linking, Platform, Pressable, ScrollView, Text, View } from 'react-native'
import { publicApi } from '../../api/client'
import type { GuestReservation } from '../../api/types'
import { Button, Card, Empty, ErrorBox, Field, Loading, StatusPill, styles } from '../../components/ui'
import { loadBookings } from '../../lib/bookings'
import { formatCardNumber, formatExpiry, formatMoney, formatRange } from '../../lib/format'
import { registerForPush } from '../../lib/push'
import { colors, radius, space } from '../../lib/theme'

/** Dogal onay kutusu; web'de Alert.alert butonlari desteklenmedigi icin window.confirm. */
function confirmAction(message: string, onConfirm: () => void) {
  if (Platform.OS === 'web') {
    if (window.confirm(message)) onConfirm()
    return
  }
  Alert.alert('Emin misiniz?', message, [
    { text: 'Vazgeç', style: 'cancel' },
    { text: 'Evet', style: 'destructive', onPress: onConfirm },
  ])
}

function Countdown({ deadline }: { deadline: string }) {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(timer)
  }, [])
  const left = Math.max(0, new Date(deadline).getTime() - now)
  const mm = Math.floor(left / 60_000)
  const ss = Math.floor((left % 60_000) / 1000)
  return (
    <Text accessibilityLiveRegion="polite" style={{ color: left < 3 * 60_000 ? colors.danger : colors.warning, fontWeight: '700' }}>
      {left > 0 ? `Ödeme için kalan süre ${mm}:${String(ss).padStart(2, '0')}` : 'Ödeme süresi doldu'}
    </Text>
  )
}

function PaymentForm({ reservation, email, onPaid }: { reservation: GuestReservation; email: string; onPaid: (r: GuestReservation) => void }) {
  const [cardHolder, setCardHolder] = useState('')
  const [cardNumber, setCardNumber] = useState('')
  const [expiry, setExpiry] = useState('')
  const [cvc, setCvc] = useState('')

  const pay = useMutation({
    mutationFn: () => publicApi.pay(reservation.confirmationCode, { email, cardHolder, cardNumber, expiry, cvc }),
    onSuccess: onPaid,
  })

  return (
    <Card style={{ gap: space(3) }}>
      <Text style={styles.h2}>Ödeme</Text>
      {reservation.paymentDeadline && <Countdown deadline={reservation.paymentDeadline} />}
      <View style={{ backgroundColor: colors.brandSoft, borderRadius: radius.md, padding: space(3) }}>
        <Text style={{ color: colors.brandDark, fontSize: 13 }}>
          Demo ödeme: gerçek kart çekimi yapılmaz. Test kartı 4242 4242 4242 4242, herhangi bir gelecek tarih ve CVC.
        </Text>
      </View>
      <Field label="Kart üzerindeki isim" value={cardHolder} onChangeText={setCardHolder} autoCapitalize="characters" autoComplete="cc-name" />
      <Field
        label="Kart numarası"
        value={cardNumber}
        onChangeText={(t) => setCardNumber(formatCardNumber(t))}
        keyboardType="number-pad"
        autoComplete="cc-number"
        placeholder="0000 0000 0000 0000"
      />
      <View style={{ flexDirection: 'row', gap: space(3) }}>
        <View style={{ flex: 1 }}>
          <Field label="Son kullanma" value={expiry} onChangeText={(t) => setExpiry(formatExpiry(t))} keyboardType="number-pad" placeholder="AA/YY" autoComplete="cc-exp" />
        </View>
        <View style={{ flex: 1 }}>
          <Field label="CVC" value={cvc} onChangeText={(t) => setCvc(t.replace(/\D/g, '').slice(0, 4))} keyboardType="number-pad" secureTextEntry autoComplete="cc-csc" />
        </View>
      </View>
      <ErrorBox error={pay.error} />
      <Button title={`${formatMoney(reservation.totalPrice)} öde`} loading={pay.isPending} onPress={() => pay.mutate()} />
    </Card>
  )
}

function ReviewForm({ reservation, email, onDone }: { reservation: GuestReservation; email: string; onDone: (r: GuestReservation) => void }) {
  const [rating, setRating] = useState(0)
  const [comment, setComment] = useState('')
  const send = useMutation({ mutationFn: () => publicApi.review(reservation.confirmationCode, email, rating, comment.trim()), onSuccess: onDone })

  return (
    <Card style={{ gap: space(3) }}>
      <Text style={styles.h2}>Konaklamanız nasıldı?</Text>
      <View style={{ flexDirection: 'row', gap: space(2) }} accessibilityRole="radiogroup">
        {[1, 2, 3, 4, 5].map((n) => (
          <Pressable key={n} accessibilityRole="radio" accessibilityState={{ checked: rating === n }} accessibilityLabel={`${n} yıldız`} onPress={() => setRating(n)} hitSlop={8}>
            <Text style={{ fontSize: 36, color: n <= rating ? colors.star : '#CBD5E1' }}>★</Text>
          </Pressable>
        ))}
      </View>
      <Field label="Yorumunuz" value={comment} onChangeText={setComment} multiline style={[styles.input, { height: 110, paddingTop: space(3), textAlignVertical: 'top' }]} hint="En az 10 karakter. Otel yanıt verebilir." />
      <ErrorBox error={send.error} />
      <Button title="Yorumu gönder" loading={send.isPending} disabled={rating === 0 || comment.trim().length < 10} onPress={() => send.mutate()} />
    </Card>
  )
}

export default function ReservationScreen() {
  const { code } = useLocalSearchParams<{ code: string }>()
  const queryClient = useQueryClient()

  // E-posta adres satirinda tasinmaz: telefonda kayitli rezervasyonlardan okunur
  const saved = useQuery({ queryKey: ['saved-booking', code], queryFn: async () => (await loadBookings()).find((b) => b.code === code) ?? null })
  const email = saved.data?.email

  const reservation = useQuery({
    queryKey: ['reservation', code],
    queryFn: () => publicApi.reservation(code, email!),
    enabled: !!email,
    // Odeme bekleniyorsa durum otomatik degisebilir (otel onayi, sure dolumu): 15 sn'de bir tazele
    refetchInterval: (query) => (query.state.data?.status === 'Pending' ? 15_000 : false),
  })

  const update = (r: GuestReservation) => {
    queryClient.setQueryData(['reservation', code], r)
    void queryClient.invalidateQueries({ queryKey: ['bookings'] })
  }

  const cancel = useMutation({ mutationFn: () => publicApi.cancel(code, email!), onSuccess: update })

  // Bu rezervasyonun durum degisiklikleri icin push bildirim izni iste (sadece telefonda)
  useEffect(() => {
    if (!email || !reservation.data) return
    if (reservation.data.status === 'Pending' || reservation.data.status === 'Confirmed') {
      void registerForPush().then((token) => token && publicApi.registerPush(code, email, token).catch(() => {}))
    }
  }, [code, email, reservation.data])

  if (saved.isLoading || reservation.isLoading) return <Loading />
  if (!saved.data) return <Empty title="Rezervasyon bu cihazda kayıtlı değil" text="Rezervasyonlarım sekmesinden kod ve e-posta ile ekleyin." />
  if (reservation.error || !reservation.data) return <View style={{ padding: space(4) }}><ErrorBox error={reservation.error} /></View>
  const r = reservation.data

  return (
    <ScrollView contentContainerStyle={{ padding: space(4), gap: space(4), paddingBottom: space(12) }} keyboardShouldPersistTaps="handled">
      <Card style={{ gap: space(2), alignItems: 'center' }}>
        <StatusPill status={r.status} />
        <Text style={styles.muted}>Rezervasyon kodu</Text>
        <Text selectable style={{ fontSize: 30, fontWeight: '800', letterSpacing: 4, color: colors.text }}>
          {r.confirmationCode}
        </Text>
        <Text style={[styles.muted, { textAlign: 'center' }]}>Otele girişte bu kodu gösterin.</Text>
      </Card>

      <Card style={{ gap: space(2) }}>
        <Text style={styles.h2}>{r.hotelName}</Text>
        <Text style={styles.body}>
          {r.roomTypeName} · {r.guestCount} kişi
        </Text>
        <Text style={styles.muted}>
          {formatRange(r.checkIn, r.checkOut)} · {r.nights} gece
        </Text>
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginTop: space(1) }}>
          <Text style={styles.body}>Toplam</Text>
          <Text style={{ fontWeight: '700', fontSize: 18, color: colors.text }}>{formatMoney(r.totalPrice)}</Text>
        </View>
        {r.paymentMethod && <Text style={styles.muted}>Ödeme: {r.paymentMethod}</Text>}
        <Text style={styles.muted}>{r.hotelAddress}</Text>
        {r.hotelPhone ? (
          <Pressable accessibilityRole="link" onPress={() => Linking.openURL(`tel:${r.hotelPhone.replace(/\s/g, '')}`)}>
            <Text style={{ color: colors.brand, fontWeight: '600' }}>Oteli ara: {r.hotelPhone}</Text>
          </Pressable>
        ) : null}
      </Card>

      {r.canPay && <PaymentForm reservation={r} email={email!} onPaid={update} />}
      {r.canReview && <ReviewForm reservation={r} email={email!} onDone={update} />}
      {r.myRating && (
        <Card>
          <Text style={styles.body}>Yorumunuz için teşekkürler ({r.myRating} ★).</Text>
        </Card>
      )}

      {r.canCancel && (
        <>
          <ErrorBox error={cancel.error} />
          <Button
            title="Rezervasyonu iptal et"
            variant="danger"
            loading={cancel.isPending}
            onPress={() => confirmAction('Rezervasyonunuz iptal edilecek. Ödeme yaptıysanız iade edilir.', () => cancel.mutate())}
          />
        </>
      )}
    </ScrollView>
  )
}
