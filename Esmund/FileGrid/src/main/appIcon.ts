import { app, nativeImage } from 'electron'
import { existsSync } from 'fs'
import { join } from 'path'

/** Resolve build/icon.png relative to project root (dev + electron-vite preview). */
export function resolveAppIconPath(): string | undefined {
  const candidates = [
    join(process.cwd(), 'build', 'icon.png'),
    join(app.getAppPath(), 'build', 'icon.png'),
    join(__dirname, '../../build/icon.png')
  ]
  for (const p of candidates) {
    if (existsSync(p)) return p
  }
  return undefined
}

export function loadAppIcon() {
  const path = resolveAppIconPath()
  if (!path) return undefined
  const image = nativeImage.createFromPath(path)
  return image.isEmpty() ? undefined : image
}
