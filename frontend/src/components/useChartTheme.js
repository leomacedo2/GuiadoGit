import { useEffect, useState } from 'react';

function colors() {
  const css = getComputedStyle(document.documentElement);
  const value = name => css.getPropertyValue(`--bs-${name}`).trim();
  return { text: value('body-color'), border: value('border-color'), background: value('body-bg'),
    primary: value('primary'), series: ['primary', 'success', 'warning', 'info', 'danger',
      'primary-text-emphasis', 'success-text-emphasis', 'warning-text-emphasis', 'info-text-emphasis', 'danger-text-emphasis'].map(value) };
}
export default function useChartTheme() {
  const [palette, setPalette] = useState(colors);
  useEffect(() => {
    const observer = new MutationObserver(() => setPalette(colors()));
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-bs-theme'] });
    return () => observer.disconnect();
  }, []);
  return palette;
}
