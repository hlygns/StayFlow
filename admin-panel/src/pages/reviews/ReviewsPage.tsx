import { useQuery } from '@tanstack/react-query'
import clsx from 'clsx'
import { format, parseISO } from 'date-fns'
import { tr } from 'date-fns/locale'
import { useState } from 'react'
import { reviewsApi } from '../../api/endpoints'
import type { Review, ReviewSummary } from '../../api/types'
import { useAuth } from '../../auth/AuthContext'
import { StarIcon } from '../../components/icons'
import { Button } from '../../components/ui/Button'
import { Card, EmptyState, ErrorAlert, PageHeader } from '../../components/ui/Card'
import { Textarea } from '../../components/ui/Field'
import { PageLoader } from '../../components/ui/Spinner'
import { useApiMutation } from '../../lib/useApiMutation'

const invalidate = [['reviews']]

export function Stars({ rating, size = 'size-4' }: { rating: number; size?: string }) {
  return (
    <span className="flex" role="img" aria-label={`5 üzerinden ${rating} yıldız`}>
      {[1, 2, 3, 4, 5].map((i) => (
        <StarIcon key={i} className={clsx(size, i <= rating ? 'fill-amber-400 text-amber-400' : 'text-slate-300')} />
      ))}
    </span>
  )
}

/** Ortalama + puan dagilimi. Tek seri yatay cubuklar; degerler cubugun yaninda yazili (renk tek basina bilgi tasimaz). */
function SummaryCard({ summary }: { summary: ReviewSummary }) {
  const max = Math.max(1, ...summary.distribution)
  return (
    <Card className="grid gap-6 p-5 sm:grid-cols-[auto_1fr]">
      <div className="text-center sm:pr-6 sm:text-left">
        <p className="text-5xl font-semibold text-slate-900">{summary.averageRating?.toLocaleString('tr-TR') ?? '—'}</p>
        <div className="mt-2 flex justify-center sm:justify-start">
          <Stars rating={Math.round(summary.averageRating ?? 0)} />
        </div>
        <p className="mt-1 text-sm text-slate-500">{summary.count} yayındaki yorum</p>
        {summary.unanswered > 0 && <p className="mt-1 text-sm font-medium text-amber-700">{summary.unanswered} yorum yanıt bekliyor</p>}
      </div>
      <figure>
        <figcaption className="sr-only">Puan dağılımı</figcaption>
        <ul className="space-y-1.5">
          {[5, 4, 3, 2, 1].map((stars) => {
            const count = summary.distribution[stars - 1]
            return (
              <li key={stars} className="flex items-center gap-3 text-sm">
                <span className="w-10 shrink-0 text-slate-600 tabular-nums">{stars} ★</span>
                <span className="h-2.5 flex-1 overflow-hidden rounded-full bg-slate-100">
                  <span className="block h-full rounded-full bg-brand-500" style={{ width: `${(count / max) * 100}%` }} />
                </span>
                <span className="w-6 shrink-0 text-right text-slate-600 tabular-nums">{count}</span>
              </li>
            )
          })}
        </ul>
      </figure>
    </Card>
  )
}

function ReviewCard({ review, canModerate }: { review: Review; canModerate: boolean }) {
  const [editing, setEditing] = useState(false)
  const [reply, setReply] = useState(review.ownerReply ?? '')

  const saveReply = useApiMutation((text: string) => reviewsApi.reply(review.id, text), {
    invalidate,
    successMessage: 'Yanıt kaydedildi; misafir uygulamasında görünecek.',
    onSuccess: () => setEditing(false),
  })
  const toggle = useApiMutation(() => reviewsApi.setVisibility(review.id, !review.isPublished), {
    invalidate,
    successMessage: review.isPublished ? 'Yorum gizlendi.' : 'Yorum tekrar yayında.',
  })

  return (
    <Card className={clsx('p-5', !review.isPublished && 'opacity-70')}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <Stars rating={review.rating} />
          <p className="mt-1 text-sm font-medium text-slate-900">
            {review.guestName}
            <span className="font-normal text-slate-500">
              {' '}
              · {review.roomTypeName} · {format(parseISO(review.createdAt), 'd MMM yyyy', { locale: tr })}
            </span>
          </p>
        </div>
        <div className="flex items-center gap-2">
          {!review.isPublished && <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-600">Gizli</span>}
          {canModerate && (
            <Button variant="ghost" size="sm" loading={toggle.isPending} onClick={() => toggle.mutate(undefined)}>
              {review.isPublished ? 'Gizle' : 'Yayınla'}
            </Button>
          )}
        </div>
      </div>

      <p className="mt-3 text-sm whitespace-pre-line text-slate-700">{review.comment}</p>

      {editing ? (
        <form
          className="mt-4 space-y-2"
          onSubmit={(e) => {
            e.preventDefault()
            if (reply.trim()) saveReply.mutate(reply.trim())
          }}
        >
          <Textarea label="Yanıtınız" value={reply} onChange={(e) => setReply(e.target.value)} maxLength={2000} />
          <div className="flex gap-2">
            <Button type="submit" size="sm" loading={saveReply.isPending} disabled={!reply.trim()}>
              Yanıtı kaydet
            </Button>
            <Button size="sm" variant="ghost" onClick={() => setEditing(false)}>
              Vazgeç
            </Button>
          </div>
        </form>
      ) : review.ownerReply ? (
        <div className="mt-4 rounded-lg bg-slate-50 p-3 text-sm">
          <p className="text-xs font-medium text-slate-500">Otelin yanıtı</p>
          <p className="mt-1 whitespace-pre-line text-slate-700">{review.ownerReply}</p>
          <button type="button" className="mt-2 text-xs font-medium text-brand-700 hover:underline" onClick={() => setEditing(true)}>
            Düzenle
          </button>
        </div>
      ) : (
        <Button className="mt-4" size="sm" variant="secondary" onClick={() => setEditing(true)}>
          Yanıtla
        </Button>
      )}
    </Card>
  )
}

type Filter = 'all' | 'unanswered' | 1 | 2 | 3 | 4 | 5

export function ReviewsPage() {
  const { isOwner } = useAuth()
  const [filter, setFilter] = useState<Filter>('all')

  const summary = useQuery({ queryKey: ['reviews', 'summary'], queryFn: reviewsApi.summary })
  const list = useQuery({
    queryKey: ['reviews', filter],
    queryFn: () => reviewsApi.list(filter === 'unanswered' ? { unanswered: true } : typeof filter === 'number' ? { rating: filter } : {}),
  })

  const filters: { value: Filter; label: string }[] = [
    { value: 'all', label: 'Tümü' },
    { value: 'unanswered', label: 'Yanıt bekleyen' },
    ...([5, 4, 3, 2, 1] as const).map((n) => ({ value: n, label: `${n} ★` })),
  ]

  return (
    <>
      <PageHeader title="Misafir yorumları" description="Konaklamasını tamamlayan misafirlerin yorumları. Yanıtlarınız misafir uygulamasında görünür." />

      {summary.data && <SummaryCard summary={summary.data} />}

      <div className="my-5 flex flex-wrap gap-2" role="group" aria-label="Filtre">
        {filters.map((f) => (
          <button
            key={String(f.value)}
            type="button"
            onClick={() => setFilter(f.value)}
            aria-pressed={filter === f.value}
            className={clsx(
              'rounded-full px-3 py-1 text-sm font-medium ring-1 transition-colors',
              filter === f.value ? 'bg-brand-600 text-white ring-brand-600' : 'bg-white text-slate-700 ring-slate-300 hover:bg-slate-50',
            )}
          >
            {f.label}
          </button>
        ))}
      </div>

      {list.isLoading ? (
        <PageLoader />
      ) : list.error ? (
        <ErrorAlert error={list.error} />
      ) : list.data!.length === 0 ? (
        <Card>
          <EmptyState title="Yorum yok" description={filter === 'all' ? 'Misafirler check-out sonrası uygulamadan yorum yazabilir.' : 'Bu filtreye uyan yorum yok.'} />
        </Card>
      ) : (
        <div className="space-y-4">
          {list.data!.map((review) => (
            <ReviewCard key={review.id} review={review} canModerate={isOwner} />
          ))}
        </div>
      )}
    </>
  )
}
