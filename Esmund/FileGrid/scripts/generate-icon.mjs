import { writeFileSync } from 'fs'
import { dirname, join } from 'path'
import { fileURLToPath } from 'url'
import pngToIco from 'png-to-ico'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')
const png = join(root, 'build', 'icon.png')
const ico = join(root, 'build', 'icon.ico')

const buf = await pngToIco(png)
writeFileSync(ico, buf)
console.log('Wrote', ico)
