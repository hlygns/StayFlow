import type { ReactNode } from 'react'

/** Giris ve kayit sayfalarinin ortak cercevesi. */
export function AuthShell({ title, subtitle, children, footer }: { title: string; subtitle: string; children: ReactNode; footer: ReactNode }) {
  return (
    <div className="flex min-h-dvh items-center justify-center bg-gradient-to-br from-brand-900 via-brand-800 to-brand-700 px-4 py-10">
      <div className="w-full max-w-md">
        <p className="mb-6 text-center text-2xl font-semibold tracking-tight text-white">StayFlow</p>
        <div className="rounded-2xl bg-surface p-6 shadow-xl sm:p-8">
          <h1 className="text-xl font-semibold text-slate-900">{title}</h1>
          <p className="mt-1 text-sm text-slate-500">{subtitle}</p>
          <div className="mt-6">{children}</div>
        </div>
        <p className="mt-6 text-center text-sm text-brand-100">{footer}</p>
      </div>
    </div>
  )
}
