import { createTheme, MantineColorsTuple } from '@mantine/core';

// Cyber Mint Primary (from Stitch DESIGN.md #3cd69b / #60f3b5)
const cyberMint: MantineColorsTuple = [
  '#e6fff4',
  '#c0fde0',
  '#8ffbc6',
  '#60f3b5',
  '#3cd69b', // primary
  '#2bc48b',
  '#1fa774',
  '#15855b',
  '#0d6443',
  '#05442c',
];

// Dark Obsidian Canvas & Surfaces (from Stitch DESIGN.md #090a0c / #121418 / #171920)
const darkObsidian: MantineColorsTuple = [
  '#f8fafc',
  '#e2e2e8',
  '#bbcac0',
  '#85948b',
  '#475569',
  '#333539',
  '#282a2e',
  '#1e2024',
  '#171920',
  '#090a0c', // deepest background
];

// Neon Cyan Secondary (from Stitch DESIGN.md #38bdf8 / #7bd0ff)
const neonCyan: MantineColorsTuple = [
  '#e0f6ff',
  '#bae7ff',
  '#7bd0ff',
  '#38bdf8',
  '#0ea5e9',
  '#0284c7',
  '#0369a1',
  '#075985',
  '#0c4a6e',
  '#082f49',
];

export const theme = createTheme({
  primaryColor: 'cyberMint',
  colors: {
    cyberMint,
    darkObsidian,
    neonCyan,
  },
  fontFamily: 'Inter, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
  headings: {
    fontFamily: '"Plus Jakarta Sans", Inter, system-ui, sans-serif',
    fontWeight: '700',
  },
  fontFamilyMonospace: '"JetBrains Mono", ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace',
  defaultRadius: 'md',
  cursorType: 'pointer',
  components: {
    Button: {
      defaultProps: {
        radius: 'md',
      },
    },
    Badge: {
      defaultProps: {
        radius: 'sm',
      },
    },
    Card: {
      defaultProps: {
        radius: 'lg',
        bg: '#121418',
      },
    },
    Modal: {
      defaultProps: {
        radius: 'xl',
        overlayProps: {
          backgroundOpacity: 0.8,
          blur: 16,
        },
      },
    },
  },
});

