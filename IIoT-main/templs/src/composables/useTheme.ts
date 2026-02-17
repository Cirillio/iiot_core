import { useColorMode } from '@vueuse/core'

export const useTheme = () => {
  const mode = useColorMode({
    emitAuto: true,
  })

  const cycleTheme = () => {
    if (mode.value === 'light') {
      mode.value = 'dark'
    } else if (mode.value === 'dark') {
      mode.value = 'auto'
    } else {
      mode.value = 'light'
    }
  }

  return {
    mode, // 'auto' | 'light' | 'dark'
    cycleTheme,
  }
}
