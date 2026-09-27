import { useQuery } from '@tanstack/react-query'
import { router, Stack, useLocalSearchParams } from 'expo-router'
import { useState } from 'react'
import { ScrollView, Text, View } from 'react-native'
import { publicApi } from '../../api/client'
import type { Availability } from '../../api/types'
import { StayPicker } from '../../components/StayPicker'
import { Button, Card, Empty, ErrorBox, Loading, Stars, styles } from '../../components/ui'
import { addDays, formatMoney, today } from '../../lib/format'
import { colors, space } from '../../lib/theme'

function RoomOption({ option, onSelect }: { option: Availability; onSelect: () => void }) {
  return (
    <Card style={{ gap: space(2) }}>
      <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: space(3) }}>
        <View style={{ flex: 1 }}>
          <Text style={styles.h2}>{option.name}</Text>
          <Text style={styles.muted}>{option.capacity} kişiye kadar</Text>
        </View>
        <View style={{ alignItems: 'flex-end' }}>
          <Text style={{ fontSize: 18, fontWeight: '700', color: colors.text }}>{formatMoney(option.totalPrice)}</Text>
          <Text style={styles.muted}>{option.nights} gece toplam</Text>
        </View>
      </View>
      {option.description ? <Text style={styles.body}>{option.description}</Text> : null}
      {option.roomsLeft <= 2 && (
        <Text style={{ color: colors.warning, fontWeight: '600' }}>Bu tarihler için son {option.roomsLeft} oda</Text>
      )}
      <Button title="Seç" onPress={onSelect} />
    </Card>
  )
}

export default function HotelScreen() {
  const { slug } = useLocalSearchParams<{ slug: string }>()
  const [stay, setStay] = useState({ checkIn: today(), nights: 2, guests: 2 })
  const checkOut = addDays(stay.checkIn, stay.nights)

  const hotel = useQuery({ queryKey: ['hotel', slug], queryFn: () => publicApi.hotel(slug) })
  const reviews = useQuery({ queryKey: ['reviews', slug], queryFn: () => publicApi.reviews(slug) })
  const availability = useQuery({
    queryKey: ['availability', slug, stay.checkIn, checkOut, stay.guests],
    queryFn: () => publicApi.availability(slug, stay.checkIn, checkOut, stay.guests),
    placeholderData: (previous) => previous, // tarih degisirken liste bosalip ziplamasin
  })

  if (hotel.isLoading) return <Loading />
  if (hotel.error || !hotel.data) return <View style={{ padding: space(4) }}><ErrorBox error={hotel.error} /></View>
  const h = hotel.data

  return (
    <ScrollView contentContainerStyle={{ padding: space(4), gap: space(5), paddingBottom: space(12) }}>
      <Stack.Screen options={{ title: h.name }} />

      <View style={{ gap: space(2) }}>
        <Text style={styles.h1}>{h.name}</Text>
        <Text style={styles.muted}>{[h.address, h.city].filter(Boolean).join(', ')}</Text>
        {h.averageRating && (
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: space(2) }}>
            <Stars rating={h.averageRating} size={16} />
            <Text style={styles.muted}>
              {h.averageRating.toLocaleString('tr-TR')} · {h.reviewCount} yorum
            </Text>
          </View>
        )}
        {h.description ? <Text style={styles.body}>{h.description}</Text> : null}
      </View>

      <Card>
        <StayPicker checkIn={stay.checkIn} nights={stay.nights} guests={stay.guests} onChange={setStay} />
      </Card>

      <View style={{ gap: space(3) }}>
        <Text style={styles.h2}>Müsait odalar</Text>
        <ErrorBox error={availability.error} />
        {availability.isLoading ? (
          <Loading />
        ) : availability.data?.length === 0 ? (
          <Card>
            <Empty title="Bu tarihlerde müsait oda yok" text="Farklı bir tarih ya da kişi sayısı deneyin." />
          </Card>
        ) : (
          availability.data?.map((option) => (
            <RoomOption
              key={option.roomTypeId}
              option={option}
              onSelect={() =>
                router.push({
                  pathname: '/book/[slug]',
                  params: {
                    slug,
                    roomTypeId: option.roomTypeId,
                    roomTypeName: option.name,
                    hotelName: h.name,
                    checkIn: stay.checkIn,
                    checkOut,
                    guests: String(stay.guests),
                    total: String(option.totalPrice),
                  },
                })
              }
            />
          ))
        )}
      </View>

      {reviews.data && reviews.data.length > 0 && (
        <View style={{ gap: space(3) }}>
          <Text style={styles.h2}>Misafir yorumları</Text>
          {reviews.data.slice(0, 5).map((review, i) => (
            <Card key={i} style={{ gap: space(1.5) }}>
              <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                <Text style={{ fontWeight: '600', color: colors.text }}>{review.guestName}</Text>
                <Stars rating={review.rating} />
              </View>
              <Text style={styles.body}>{review.comment}</Text>
              {review.ownerReply && (
                <View style={{ backgroundColor: colors.canvas, borderRadius: 8, padding: space(3), marginTop: space(1) }}>
                  <Text style={{ fontSize: 12, fontWeight: '600', color: colors.textMuted }}>Otelin yanıtı</Text>
                  <Text style={styles.body}>{review.ownerReply}</Text>
                </View>
              )}
            </Card>
          ))}
        </View>
      )}
    </ScrollView>
  )
}
