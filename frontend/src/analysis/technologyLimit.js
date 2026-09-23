// Input is already ranked by the chart's existing rule. Never reorder it here.
export function limitTechnologies(items, limit = 5) {
  return limit === 'all' ? items : items.slice(0, Number(limit));
}
