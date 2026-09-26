import clsx from 'clsx'
import { createContext, useCallback, useContext, useState, type ReactNode } from 'react'

type ToastKind = 'success' | 'error'
interface ToastItem {
  id: number
  kind: ToastKind
  message: string
}

const ToastContext = createContext<(message: string, kind?: ToastKind) => void>(() => {})

let nextId = 1

/** Islem sonrasi kisa bildirimler ("Rezervasyon oluşturuldu"). 4 sn sonra kendiliginden kapanir. */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([])

  const show = useCallback((message: string, kind: ToastKind = 'success') => {
    const id = nextId++
    setToasts((current) => [...current, { id, kind, message }])
    setTimeout(() => setToasts((current) => current.filter((t) => t.id !== id)), 4000)
  }, [])

  return (
    <ToastContext.Provider value={show}>
      {children}
      <div className="pointer-events-none fixed inset-x-4 bottom-4 z-50 flex flex-col items-end gap-2" aria-live="polite">
        {toasts.map((toast) => (
          <div
            key={toast.id}
            className={clsx(
              'pointer-events-auto max-w-sm rounded-lg px-4 py-3 text-sm font-medium shadow-lg',
              toast.kind === 'success' ? 'bg-slate-900 text-white' : 'bg-red-600 text-white',
            )}
          >
            {toast.message}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}

// oxlint-disable-next-line react/only-export-components
export const useToast = () => useContext(ToastContext)
