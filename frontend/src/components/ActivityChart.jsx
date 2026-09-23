import { Chart as ChartJS, CategoryScale, LinearScale, BarElement, Tooltip, Legend } from 'chart.js';
import { Bar } from 'react-chartjs-2';
import useChartTheme from './useChartTheme';
import { monthLabel } from '../analysis/activityData';
ChartJS.register(CategoryScale, LinearScale, BarElement, Tooltip, Legend);

export default function ActivityChart({ months, series }) {
  const palette = useChartTheme();
  const data = { labels: months.map(monthLabel), datasets: series.map((item, i) => ({
    label: item.name, data: item.counts, backgroundColor: palette.series[i], borderRadius: 3,
  })) };
  const options = {
    responsive: true, maintainAspectRatio: false, animation: false,
    plugins: { legend: { position: 'bottom', labels: { color: palette.text, boxWidth: 12 } },
      tooltip: { backgroundColor: palette.background, titleColor: palette.text, bodyColor: palette.text,
        borderColor: palette.border, borderWidth: 1, callbacks: { label: item => `${item.dataset.label} · ${item.label}: ${item.raw} commits` } } },
    scales: { x: { ticks: { color: palette.text, maxRotation: 60 }, grid: { display: false } },
      y: { beginAtZero: true, title: { display: true, text: 'Commits', color: palette.text }, ticks: { precision: 0, color: palette.text }, grid: { color: palette.border } } },
  };
  return <div className="evidence-chart" style={{ height: 320 }}><Bar data={data} options={options} role="img" aria-label="Commits por tecnologia e mês. Dados na tabela abaixo." /></div>;
}
