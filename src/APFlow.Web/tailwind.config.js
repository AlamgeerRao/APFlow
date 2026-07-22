/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        // Neutral, professional palette for the AP Flow finance-ops shell.
        // Ink: primary text/nav surface. Slate: secondary surfaces & borders.
        // Accent: used sparingly for active nav state and focus rings only.
        ink: {
          900: '#111827',
          800: '#1F2937',
          700: '#374151',
        },
        slate: {
          50: '#F8FAFC',
          100: '#F1F5F9',
          200: '#E2E8F0',
          400: '#94A3B8',
          600: '#475569',
        },
        accent: {
          600: '#2563EB',
          700: '#1D4ED8',
        },
      },
      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', 'sans-serif'],
      },
    },
  },
  plugins: [],
};
