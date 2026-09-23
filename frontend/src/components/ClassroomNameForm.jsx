import { useId, useState } from 'react';
export default function ClassroomNameForm({ initialName, busy, onSave, onCancel }) {
  const id = useId();
  const [name, setName] = useState(initialName);
  return <form className="card mb-4" onSubmit={event => { event.preventDefault(); if (!busy && name.trim().length >= 2) onSave(name.trim()); }}><div className="card-body">
    <label className="form-label" htmlFor={id}>Nome da turma</label>
    <input className="form-control mb-3" id={id} value={name} onChange={e => setName(e.target.value)} required minLength={2} maxLength={100} disabled={busy} />
    <div className="d-flex flex-wrap gap-2"><button className="btn btn-primary" disabled={busy || name.trim().length < 2}>Salvar nome</button>
      <button type="button" className="btn btn-outline-secondary" onClick={onCancel} disabled={busy}>Cancelar</button></div>
  </div></form>;
}
