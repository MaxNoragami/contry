import { APP_LIMITS, APP_TIMINGS } from '../config/app'

export type ToastTone = 'error'

export type ToastItem = {
  id: string
  message: string
  tone: ToastTone
}

export function createToastStore() {
  let items = $state<ToastItem[]>([])

  function push(message: string, tone: ToastTone = 'error', durationMs = APP_TIMINGS.toastDurationMs) {
    const toast: ToastItem = {
      id: crypto.randomUUID(),
      message,
      tone,
    }

    items = [toast, ...items].slice(0, APP_LIMITS.toastVisibleCount)

    const timeout = setTimeout(() => {
      remove(toast.id)
      clearTimeout(timeout)
    }, durationMs)

    return toast.id
  }

  function remove(id: string) {
    items = items.filter((toast) => toast.id !== id)
  }

  return {
    get items() { return items },
    push,
    remove,
  }
}

export const toastStore = createToastStore()
