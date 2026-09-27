import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { reservationsApi } from '../api/endpoints'
import { getSession } from '../api/session'
import { useToast } from '../components/ui/Toast'
import { wasChangedLocally } from './localChanges'

type ReservationChange = 'Created' | 'Confirmed' | 'CheckedIn' | 'CheckedOut' | 'Cancelled' | 'AutoCancelled'

interface ReservationChangedMessage {
  reservationId: string
  change: ReservationChange
}

export type LiveStatus = 'connecting' | 'live' | 'offline'

/**
 * Panelin canli baglantisi. Otelde bir rezervasyon degisince (baska bir resepsiyon calisani,
 * ya da odeme suresi dolunca sunucu) ilgili listeler yeniden cekilir ve kisa bir bildirim gosterilir.
 */
export function useReservationEvents(): LiveStatus {
  const queryClient = useQueryClient()
  const toast = useToast()
  const [status, setStatus] = useState<LiveStatus>('connecting')

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      // Token her (yeniden) baglanmada taze okunur: access token 15 dk'da bir yenileniyor
      .withUrl(`${import.meta.env.VITE_API_URL ?? ''}/hubs/reservations`, { accessTokenFactory: () => getSession()?.accessToken ?? '' })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('reservationChanged', async ({ reservationId, change }: ReservationChangedMessage) => {
      await queryClient.invalidateQueries({ queryKey: ['reservations'] })
      await queryClient.invalidateQueries({ queryKey: ['availability'] })

      // Bu sekmede kullanicinin kendi yaptigi degisiklik icin ikinci bir bildirim gosterme
      if (wasChangedLocally(reservationId)) return
      const message = await describe(reservationId, change)
      if (message) toast(message, change === 'AutoCancelled' ? 'error' : 'success')
    })

    connection.onreconnecting(() => setStatus('connecting'))
    connection.onreconnected(() => {
      setStatus('live')
      // Kopukken kacirilmis olabilecek degisiklikler icin her seyi tazele
      void queryClient.invalidateQueries({ queryKey: ['reservations'] })
    })
    connection.onclose(() => setStatus('offline'))

    let cancelled = false
    connection
      .start()
      .then(() => !cancelled && setStatus('live'))
      .catch(() => !cancelled && setStatus('offline'))

    return () => {
      cancelled = true
      if (connection.state !== HubConnectionState.Disconnected) void connection.stop()
    }
  }, [queryClient, toast])

  return status
}

async function describe(reservationId: string, change: ReservationChange): Promise<string | null> {
  if (change !== 'Created' && change !== 'Cancelled' && change !== 'AutoCancelled') return null
  try {
    const r = await reservationsApi.get(reservationId)
    const who = `${r.guest.fullName} · Oda ${r.roomNumber}`
    if (change === 'Created') return `Yeni rezervasyon: ${who}`
    if (change === 'AutoCancelled') return `Ödeme süresi doldu, otomatik iptal edildi: ${who}`
    return `Rezervasyon iptal edildi: ${who}`
  } catch {
    return null
  }
}
