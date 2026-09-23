import { useEffect, useState } from 'react';
import { Chart as ChartJS, CategoryScale, LinearScale, BarElement, Tooltip } from 'chart.js';
import { Bar } from 'react-chartjs-2';

ChartJS.register(CategoryScale, LinearScale, BarElement, Tooltip);
function colors() {
  const css = getComputedStyle(document.documentElement);
  return { text: css.getPropertyValue('--bs-body-color').trim(), border: css.getPropertyValue('--bs-border-color').trim(),
    primary: css.getPropertyValue('--bs-primary').trim(), background: css.getPropertyValue('--bs-body-bg').trim() };
}
export default function EvidenceChart({ rows, title }) {
  const [palette, setPalette] = useState(colors);
  useEffect(() => {
    const observer = new MutationObserver(() => setPalette(colors()));
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-bs-theme'] });
    return () => observer.disconnect();
  }, []);
  const data = { labels: rows.map(row => row.label), datasets: [{ label: 'Repositórios com evidência', data: rows.map(row => row.count), backgroundColor: palette.primary, borderRadius: 4 }] };
  const options = {
    indexAxis: 'y', responsive: true, maintainAspectRatio: false, animation: false,
    plugins: { tooltip: { backgroundColor: palette.background, titleColor: palette.text, bodyColor: palette.text, borderColor: palette.border, borderWidth: 1 } },
    scales: {
      x: { beginAtZero: true, ticks: { precision: 0, color: palette.text, maxTicksLimit: 6 }, grid: { color: palette.border } },
      y: { ticks: { color: palette.text, font: { size: 11 } }, grid: { display: false } }
    }
  };
  return <div className="evidence-chart" style={{ height: Math.max(200, rows.length * 38) }}>
    <Bar data={data} options={options} role="img" aria-label={`${title}. Contagens de repositórios; dados também disponíveis na tabela abaixo.`} />
  </div>;
}
