import Constants from 'expo-constants'
import * as Device from 'expo-device'
import * as Notifications from 'expo-notifications'
import { Platform } from 'react-native'

// Uygulama acikken gelen bildirim de gosterilsin
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldPlaySound: true,
    shouldSetBadge: false,
    shouldShowBanner: true,
    shouldShowList: true,
  }),
})

/**
 * Push bildirim izni ister ve Expo push token'i dondurur (Expo dokumanindaki onerilen akis).
 * Web'de, emulatorde ya da izin verilmezse null: uygulama bildirimsiz de tam calisir.
 */
export async function registerForPush(): Promise<string | null> {
  if (Platform.OS === 'web' || !Device.isDevice) return null

  if (Platform.OS === 'android') {
    await Notifications.setNotificationChannelAsync('reservations', {
      name: 'Rezervasyonlar',
      importance: Notifications.AndroidImportance.HIGH,
    })
  }

  const current = await Notifications.getPermissionsAsync()
  const status = current.status === 'granted' ? current.status : (await Notifications.requestPermissionsAsync()).status
  if (status !== 'granted') return null

  const projectId = Constants?.expoConfig?.extra?.eas?.projectId ?? Constants?.easConfig?.projectId
  if (!projectId) return null // EAS projesi olusturulmadan (eas init) push token alinamaz

  try {
    return (await Notifications.getExpoPushTokenAsync({ projectId })).data
  } catch {
    return null
  }
}
