export interface FontOption {
  label: string
  value: string
  hint?: string
}

export interface FontOptionGroup {
  label: string
  options: FontOption[]
}

export const FONT_OPTION_GROUPS: FontOptionGroup[] = [
  {
    label: 'Standard',
    options: [
      { label: 'Segoe UI', value: "'Segoe UI', system-ui, sans-serif" },
      { label: 'Arial', value: 'Arial, sans-serif' },
      { label: 'Tahoma', value: 'Tahoma, sans-serif' },
      { label: 'Georgia', value: 'Georgia, serif' },
      { label: 'Consolas', value: 'Consolas, monospace' }
    ]
  },
  {
    label: 'Easier reading',
    options: [
      { label: 'Lexend', value: "'Lexend', sans-serif", hint: 'Wide spacing, low visual stress' },
      { label: 'Atkinson Hyperlegible', value: "'Atkinson Hyperlegible', sans-serif", hint: 'Braille Institute / low vision' },
      { label: 'OpenDyslexic', value: "'OpenDyslexic', sans-serif", hint: 'Weighted letterforms' },
      { label: 'Andika', value: "'Andika', sans-serif", hint: 'Literacy / learning' },
      { label: 'Verdana', value: 'Verdana, sans-serif', hint: 'Large x-height' },
      { label: 'Comic Neue', value: "'Comic Neue', cursive", hint: 'Informal, distinct glyphs' }
    ]
  },
  {
    label: 'Calibre (install on PC)',
    options: [
      { label: 'Calibre', value: "'Calibre', 'Segoe UI', sans-serif" },
      { label: 'Calibre Light', value: "'Calibre Light', 'Calibre', sans-serif" },
      { label: 'Calibre Medium', value: "'Calibre Medium', 'Calibre', sans-serif" },
      { label: 'Calibre Semibold', value: "'Calibre Semibold', 'Calibre', sans-serif" }
    ]
  }
]
