import { useEffect, useId, useState } from 'react';
import { Link } from 'react-router-dom';
import { errorMessage, getSavedProfiles } from '../services/api';

export default function ClassroomMembersForm({ existingIds = [], creating = false, busy, onSubmit, onCancel }) {
  const prefix = useId();
  const [profiles, setProfiles] = useState(null);
  const [error, setError] = useState('');
  const [attempt, retry] = useState(0);
  const [name, setName] = useState('');
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState([]);
  useEffect(() => {
    let active = true;
    setError('');
    getSavedProfiles().then(data => { if (active) setProfiles(data); }).catch(e => { if (active) setError(errorMessage(e)); });
    return () => { active = false; };
  }, [attempt]);
  const available = (profiles || []).filter(p => !existingIds.includes(p.gitHubProfileId));
  const term = search.trim().toLocaleLowerCase('pt-BR');
  const filtered = available.filter(p => `${p.nome || ''} ${p.username}`.toLocaleLowerCase('pt-BR').includes(term));
  function toggle(id) { setSelected(ids => ids.includes(id) ? ids.filter(value => value !== id) : [...ids, id]); }
  return <section className="card mb-4" aria-label={creating ? 'Criar turma' : 'Adicionar alunos'}><div className="card-body">
    <h2 className="h4">{creating ? 'Nova turma' : 'Adicionar alunos'}</h2>
    {error && <p className="alert alert-danger" role="alert">{error} <button className="btn btn-sm btn-outline-danger" onClick={() => retry(value => value + 1)}>Tentar novamente</button></p>}
    {!profiles && !error && <p role="status">Carregando Minhas análises…</p>}
    {profiles?.length === 0 && <><p>Você precisa salvar pelo menos uma análise antes de criar uma turma.</p><Link className="btn btn-primary me-2" to="/analisar">Analisar um perfil</Link></>}
    {profiles?.length > 0 && <form onSubmit={event => { event.preventDefault(); if (selected.length && !busy) onSubmit(name.trim(), selected); }}>
      {creating && <div className="mb-3"><label className="form-label" htmlFor={`${prefix}-name`}>Nome da turma</label>
        <input className="form-control" id={`${prefix}-name`} value={name} onChange={e => setName(e.target.value)} minLength={2} maxLength={100} required disabled={busy} />
      </div>}
      <h3 className="h6">Selecione os alunos</h3>
      <p className="small text-body-secondary">Perfis salvos na sua conta. A seleção não inicia análises nem consulta o GitHub. Máximo de 200 alunos por turma.</p>
      <label className="form-label" htmlFor={`${prefix}-search`}>Buscar por nome ou username</label>
      <input className="form-control mb-3" id={`${prefix}-search`} type="search" value={search} onChange={e => setSearch(e.target.value)} />
      {!available.length && <p>Todos os perfis salvos já estão nesta turma.</p>}
      {available.length > 0 && !filtered.length && <p>Nenhum perfil corresponde à busca.</p>}
      <div className="member-picker border rounded p-2 mb-3">{filtered.map(profile => <label className="d-flex align-items-start gap-3 p-2 border-bottom member-choice" key={profile.gitHubProfileId}>
        <input className="form-check-input flex-shrink-0" type="checkbox" aria-label={`Selecionar @${profile.username}`} checked={selected.includes(profile.gitHubProfileId)}
          onChange={() => toggle(profile.gitHubProfileId)} disabled={busy || (!selected.includes(profile.gitHubProfileId) && existingIds.length + selected.length >= 200)} />
        <img src={profile.avatarUrl} width="40" height="40" className="rounded-circle flex-shrink-0" alt="" />
        <span className="profile-details text-break"><span className="fw-semibold d-block">{profile.nome || profile.username}</span><span className="small d-block">@{profile.username}</span>
          <span className="d-flex flex-wrap gap-1 mt-1">{profile.latest?.skills.map(skill => <span key={skill} className="badge text-bg-secondary text-wrap">{skill}</span>)}</span>
        </span>
      </label>)}</div>
      <p className="small" role="status">{selected.length} aluno(s) selecionado(s)</p>
      <div className="d-flex flex-wrap gap-2">
        <button className="btn btn-primary" disabled={busy || !selected.length || (creating && name.trim().length < 2)}>{busy ? 'Salvando…' : creating ? 'Criar turma' : 'Adicionar selecionados'}</button>
        <button className="btn btn-outline-secondary" type="button" disabled={busy} onClick={onCancel}>Cancelar</button>
      </div>
    </form>}
    {(!profiles || !profiles.length) && <button className="btn btn-outline-secondary" onClick={onCancel}>Voltar</button>}
  </div></section>;
}
