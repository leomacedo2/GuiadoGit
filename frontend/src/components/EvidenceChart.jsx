import useChartTheme from './useChartTheme';
import { Chart as ChartJS, CategoryScale, LinearScale, BarElement, Tooltip } from 'chart.js';
import { Bar } from 'react-chartjs-2';

ChartJS.register(CategoryScale, LinearScale, BarElement, Tooltip);
export default function EvidenceChart({ rows, title, unit = 'Repositórios' }) {
  const palette = useChartTheme();
  const data = { labels: rows.map(row => row.label), datasets: [{ label: `${unit} com evidência`, data: rows.map(row => row.count), backgroundColor: palette.primary, borderRadius: 4, maxBarThickness: 36 }] };
  const options = {
    indexAxis: 'y', responsive: true, maintainAspectRatio: false, animation: false,
    plugins: { legend: { display: false }, tooltip: { backgroundColor: palette.background, titleColor: palette.text, bodyColor: palette.text, borderColor: palette.border, borderWidth: 1 } },
    scales: {
      x: { beginAtZero: true, ticks: { precision: 0, color: palette.text, maxTicksLimit: 6 }, grid: { color: palette.border } },
      y: { ticks: { color: palette.text, font: { size: 11 } }, grid: { display: false } }
    }
  };
  return <div className="evidence-chart" style={{ height: Math.max(200, rows.length * 38) }}>
    <Bar data={data} options={options} role="img" aria-label={`${title}. Contagens de ${unit.toLowerCase()}; dados também disponíveis na tabela abaixo.`} />
  </div>;
}
