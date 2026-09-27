import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { Stack } from 'expo-router'
import { StatusBar } from 'expo-status-bar'
import { useState } from 'react'
import { colors } from '../lib/theme'

export default function RootLayout() {
  const [queryClient] = useState(
    () => new QueryClient({ defaultOptions: { queries: { staleTime: 30_000, retry: 1 } } }),
  )

  return (
    <QueryClientProvider client={queryClient}>
      <StatusBar style="dark" />
      <Stack
        screenOptions={{
          headerTintColor: colors.brand,
          headerTitleStyle: { color: colors.text, fontWeight: '700' },
          contentStyle: { backgroundColor: colors.canvas },
          headerBackTitle: 'Geri',
        }}
      >
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
        <Stack.Screen name="hotel/[slug]" options={{ title: '' }} />
        <Stack.Screen name="book/[slug]" options={{ title: 'Rezervasyon' }} />
        <Stack.Screen name="reservation/[code]" options={{ title: 'Rezervasyonum' }} />
      </Stack>
    </QueryClientProvider>
  )
}
