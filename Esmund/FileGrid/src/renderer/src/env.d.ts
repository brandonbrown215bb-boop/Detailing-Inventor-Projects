import type { FileGridApi } from '../../preload/index'

declare global {
  interface Window {
    fileGrid: FileGridApi
  }
}

export {}
