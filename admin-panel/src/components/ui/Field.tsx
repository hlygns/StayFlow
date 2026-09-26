import clsx from 'clsx'
import { forwardRef, useId, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react'

const controlClass =
  'block w-full rounded-lg border-0 bg-white px-3 py-2 text-sm text-slate-900 ring-1 ring-slate-300 ring-inset placeholder:text-slate-400 focus:ring-2 focus:ring-brand-500 focus:outline-none disabled:bg-slate-50 aria-invalid:ring-red-400'

interface FieldProps {
  label: string
  error?: string
  hint?: string
  children: (id: string, describedBy: string | undefined) => ReactNode
}

/** Etiket + kontrol + hata mesaji. Hata, ekran okuyucular icin kontrole baglanir (aria-describedby). */
export function Field({ label, error, hint, children }: FieldProps) {
  const id = useId()
  const messageId = `${id}-message`
  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="block text-sm font-medium text-slate-700">
        {label}
      </label>
      {children(id, error || hint ? messageId : undefined)}
      {error ? (
        <p id={messageId} className="text-xs text-red-600">
          {error}
        </p>
      ) : hint ? (
        <p id={messageId} className="text-xs text-slate-500">
          {hint}
        </p>
      ) : null}
    </div>
  )
}

type InputProps = InputHTMLAttributes<HTMLInputElement> & { label: string; error?: string; hint?: string }

export const Input = forwardRef<HTMLInputElement, InputProps>(({ label, error, hint, className, ...props }, ref) => (
  <Field label={label} error={error} hint={hint}>
    {(id, describedBy) => (
      <input
        ref={ref}
        id={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        className={clsx(controlClass, className)}
        {...props}
      />
    )}
  </Field>
))

type SelectProps = SelectHTMLAttributes<HTMLSelectElement> & { label: string; error?: string }

export const Select = forwardRef<HTMLSelectElement, SelectProps>(({ label, error, className, children, ...props }, ref) => (
  <Field label={label} error={error}>
    {(id, describedBy) => (
      <select
        ref={ref}
        id={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        className={clsx(controlClass, className)}
        {...props}
      >
        {children}
      </select>
    )}
  </Field>
))

type TextareaProps = TextareaHTMLAttributes<HTMLTextAreaElement> & { label: string; error?: string }

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaProps>(({ label, error, className, ...props }, ref) => (
  <Field label={label} error={error}>
    {(id, describedBy) => (
      <textarea
        ref={ref}
        id={id}
        rows={3}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        className={clsx(controlClass, className)}
        {...props}
      />
    )}
  </Field>
))
