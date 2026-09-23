export function activityWindow(activity, count = 12) {
  const months = (activity?.months || []).slice(-count);
  // Rank on twelve months before slicing, keeping the same series in both filters.
  const series = [...(activity?.series || [])]
    .sort((a, b) => b.counts.reduce((sum, n) => sum + (n ?? 0), 0) - a.counts.reduce((sum, n) => sum + (n ?? 0), 0) || a.name.localeCompare(b.name))
    .slice(0, 5).map(item => ({ ...item, counts: item.counts.slice(-count) }));
  return { months, series };
}
export function monthLabel(value) {
  const [year, month] = value.split('-');
  return `${month}/${year}`;
}
