export type ToastTone = 'error'

export type ToastItem = {
  id: string
  message: string
  tone: ToastTone
}

export function createToastStore() {
  let items = $state<ToastItem[]>([])

  function push(message: string, tone: ToastTone = 'error', durationMs = 3000) {
    const toast: ToastItem = {
      id: crypto.randomUUID(),
      message,
      tone,
    }

    items = [toast, ...items].slice(0, 3)

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
