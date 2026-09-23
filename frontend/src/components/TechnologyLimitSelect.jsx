import { useId } from 'react';

export default function TechnologyLimitSelect({ value, onChange }) {
  const id = useId();
  return <div>
    <label htmlFor={id} className="form-label small mb-1">Tecnologias exibidas:</label>
    <select id={id} className="form-select form-select-sm" value={value} onChange={event => onChange(event.target.value)}>
      <option value="5">Top 5</option><option value="10">Top 10</option><option value="all">Todas</option>
    </select>
  </div>;
}
