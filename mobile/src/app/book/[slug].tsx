import { useMutation } from '@tanstack/react-query'
import { router, useLocalSearchParams } from 'expo-router'
import { useState } from 'react'
import { KeyboardAvoidingView, Platform, ScrollView, Text, View } from 'react-native'
import { publicApi } from '../../api/client'
import { Button, Card, ErrorBox, Field, styles } from '../../components/ui'
import { saveBooking } from '../../lib/bookings'
import { formatMoney, formatRange, nightsBetween } from '../../lib/format'
import { colors, space } from '../../lib/theme'

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export default function BookScreen() {
  const p = useLocalSearchParams<{
    slug: string
    roomTypeId: string
    roomTypeName: string
    hotelName: string
    checkIn: string
    checkOut: string
    guests: string
    total: string
  }>()
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')
  const [touched, setTouched] = useState(false)

  const errors = {
    fullName: fullName.trim().length < 2 ? 'Ad soyad gerekli.' : undefined,
    email: !EMAIL.test(email.trim()) ? 'Geçerli bir e-posta girin. Rezervasyon kodunuz bu adrese gider.' : undefined,
  }
  const valid = !errors.fullName && !errors.email

  const book = useMutation({
    mutationFn: () =>
      publicApi.book(p.slug, {
        roomTypeId: p.roomTypeId,
        checkIn: p.checkIn,
        checkOut: p.checkOut,
        guestCount: Number(p.guests),
        guest: { fullName: fullName.trim(), email: email.trim(), phone: phone.trim() || undefined },
      }),
    onSuccess: async (reservation) => {
      // Kod + e-posta telefonda saklanir: "Rezervasyonlarim"da gorunur, odeme ekraninda kullanilir
      await saveBooking({ code: reservation.confirmationCode, email: email.trim(), hotelName: reservation.hotelName, checkIn: reservation.checkIn })
      router.replace({ pathname: '/reservation/[code]', params: { code: reservation.confirmationCode } })
    },
  })

  return (
    <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView contentContainerStyle={{ padding: space(4), gap: space(4) }} keyboardShouldPersistTaps="handled">
        <Card style={{ gap: space(1.5) }}>
          <Text style={styles.h2}>{p.hotelName}</Text>
          <Text style={styles.body}>
            {p.roomTypeName} · {p.guests} kişi
          </Text>
          <Text style={styles.muted}>
            {formatRange(p.checkIn, p.checkOut)} · {nightsBetween(p.checkIn, p.checkOut)} gece
          </Text>
          <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginTop: space(2) }}>
            <Text style={styles.body}>Toplam</Text>
            <Text style={{ fontSize: 20, fontWeight: '700', color: colors.text }}>{formatMoney(Number(p.total))}</Text>
          </View>
        </Card>

        <Card style={{ gap: space(4) }}>
          <Text style={styles.h2}>Misafir bilgileri</Text>
          <Field label="Ad soyad" value={fullName} onChangeText={setFullName} autoComplete="name" textContentType="name" error={touched ? errors.fullName : undefined} />
          <Field
            label="E-posta"
            value={email}
            onChangeText={setEmail}
            autoCapitalize="none"
            keyboardType="email-address"
            autoComplete="email"
            textContentType="emailAddress"
            error={touched ? errors.email : undefined}
          />
          <Field label="Telefon (isteğe bağlı)" value={phone} onChangeText={setPhone} keyboardType="phone-pad" autoComplete="tel" textContentType="telephoneNumber" />
        </Card>

        <ErrorBox error={book.error} />
        <Button
          title="Rezervasyon yap"
          loading={book.isPending}
          onPress={() => {
            setTouched(true)
            if (valid) book.mutate()
          }}
        />
        <Text style={[styles.muted, { textAlign: 'center' }]}>
          Sonraki adımda ödemeyi tamamlayın. Ödeme 15 dakika içinde yapılmazsa oda tekrar satışa açılır.
        </Text>
      </ScrollView>
    </KeyboardAvoidingView>
  )
}
