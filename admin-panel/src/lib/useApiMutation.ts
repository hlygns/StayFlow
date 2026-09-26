import { useMutation, useQueryClient, type QueryKey } from '@tanstack/react-query'
import { useToast } from '../components/ui/Toast'

interface Options<TData> {
  /** Basarili olunca yenilenecek listeler (ör. [['rooms'], ['room-types']]) */
  invalidate?: QueryKey[]
  successMessage?: string | ((data: TData) => string)
  onSuccess?: (data: TData) => void
  /** true: hatayi toast ile gosterme - form kendi icinde gosteriyor */
  silentError?: boolean
}

/**
 * Her yazma isleminde tekrar eden kalip: istegi at -> basariliysa bildirim goster ve
 * ilgili listeleri yeniden cek -> hata varsa bildirim goster.
 */
export function useApiMutation<TVariables, TData = unknown>(
  mutationFn: (variables: TVariables) => Promise<TData>,
  { invalidate = [], successMessage, onSuccess, silentError }: Options<TData> = {},
) {
  const queryClient = useQueryClient()
  const toast = useToast()

  return useMutation({
    mutationFn,
    onSuccess: async (data) => {
      await Promise.all(invalidate.map((queryKey) => queryClient.invalidateQueries({ queryKey })))
      if (successMessage) toast(typeof successMessage === 'function' ? successMessage(data) : successMessage)
      onSuccess?.(data)
    },
    onError: (error) => {
      if (!silentError) toast(error.message, 'error')
    },
  })
}
