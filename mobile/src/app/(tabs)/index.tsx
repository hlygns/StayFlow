import { useQuery } from '@tanstack/react-query'
import { Link } from 'expo-router'
import { useEffect, useState } from 'react'
import { FlatList, Pressable, Text, TextInput, View } from 'react-native'
import { publicApi } from '../../api/client'
import type { HotelSummary } from '../../api/types'
import { Empty, ErrorBox, Loading, Stars, styles } from '../../components/ui'
import { formatMoney } from '../../lib/format'
import { colors, radius, space } from '../../lib/theme'

function HotelCard({ hotel }: { hotel: HotelSummary }) {
  return (
    <Link href={{ pathname: '/hotel/[slug]', params: { slug: hotel.slug } }} asChild>
      <Pressable accessibilityRole="link" style={({ pressed }) => [styles.card, { gap: space(2) }, pressed && { opacity: 0.9 }]}>
        {/* Fotograf yuklemesi olmadigi icin marka renginde bir kapak: otelin bas harfi */}
        <View
          style={{
            height: 110,
            borderRadius: radius.md,
            backgroundColor: colors.brandDark,
            justifyContent: 'flex-end',
            padding: space(3),
          }}
        >
          <Text style={{ color: '#fff', fontSize: 20, fontWeight: '700' }} numberOfLines={1}>
            {hotel.name}
          </Text>
          <Text style={{ color: '#B0EBE8', fontSize: 13 }}>{hotel.city}</Text>
        </View>
        {hotel.description ? (
          <Text style={styles.body} numberOfLines={2}>
            {hotel.description}
          </Text>
        ) : null}
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
          {hotel.averageRating ? (
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: space(1.5) }}>
              <Stars rating={hotel.averageRating} />
              <Text style={styles.muted}>
                {hotel.averageRating.toLocaleString('tr-TR')} ({hotel.reviewCount})
              </Text>
            </View>
          ) : (
            <Text style={styles.muted}>Henüz yorum yok</Text>
          )}
          {hotel.minPricePerNight !== null && (
            <Text style={{ color: colors.text }}>
              <Text style={{ fontWeight: '700', fontSize: 16 }}>{formatMoney(hotel.minPricePerNight)}</Text>
              <Text style={styles.muted}> / gece’den</Text>
            </Text>
          )}
        </View>
      </Pressable>
    </Link>
  )
}

export default function ExploreScreen() {
  const [search, setSearch] = useState('')
  const [city, setCity] = useState('')

  // Her tus vurusunda istek atma: yazmayi birakinca (400 ms) ara
  useEffect(() => {
    const timer = setTimeout(() => setCity(search.trim()), 400)
    return () => clearTimeout(timer)
  }, [search])

  const hotels = useQuery({ queryKey: ['hotels', city], queryFn: () => publicApi.hotels(city || undefined) })

  return (
    <FlatList
      data={hotels.data ?? []}
      keyExtractor={(h) => h.slug}
      renderItem={({ item }) => <HotelCard hotel={item} />}
      contentContainerStyle={{ padding: space(4), gap: space(4) }}
      refreshing={hotels.isRefetching}
      onRefresh={() => hotels.refetch()}
      ListHeaderComponent={
        <View style={{ gap: space(3) }}>
          <Text style={styles.h1}>Nereye gidiyorsunuz?</Text>
          <TextInput
            accessibilityLabel="Şehir ara"
            placeholder="Şehir ara (ör. Antalya)"
            placeholderTextColor={colors.textMuted}
            value={search}
            onChangeText={setSearch}
            style={styles.input}
            returnKeyType="search"
          />
          <ErrorBox error={hotels.error} />
        </View>
      }
      ListEmptyComponent={
        hotels.isLoading ? <Loading /> : !hotels.error ? <Empty title="Otel bulunamadı" text="Başka bir şehir deneyin." /> : null
      }
    />
  )
}
