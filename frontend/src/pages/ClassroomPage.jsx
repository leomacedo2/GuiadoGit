import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { addClassroomMembers, deleteClassroom, errorMessage, getClassroom, openSavedAnalysis, removeClassroomMember, renameClassroom } from '../services/api';
import ChartPanel from '../components/ChartPanel';
import ActivityPanel from '../components/ActivityPanel';
import ClassroomMembersForm from '../components/ClassroomMembersForm';
import ClassroomNameForm from '../components/ClassroomNameForm';

function age(date) {
  const days = Math.max(0, Math.floor((Date.now() - new Date(date).getTime()) / 86400000));
  return days === 0 ? 'Analisado hoje' : `Analisado há ${days} dia(s)`;
}
export default function ClassroomPage({ onOpen }) {
  const { id } = useParams();
  const navigate = useNavigate();
  const [classroom, setClassroom] = useState(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [attempt, reload] = useState(0);
  const [editing, setEditing] = useState(false);
  const [adding, setAdding] = useState(false);
  const [selectedId, setSelectedId] = useState('');
  const selectedStudent = classroom?.members.find(student => student.gitHubProfileId === selectedId) ?? null;
  useEffect(() => {
    setSelectedId(current => classroom?.members.some(student => student.gitHubProfileId === current) ? current : '');
  }, [classroom]);
  useEffect(() => {
    let active = true;
    getClassroom(id).then(data => { if (active) setClassroom(data); }).catch(e => {
      if (active) setError(e.response?.status === 404 ? 'Turma não encontrada ou indisponível para sua conta.' : errorMessage(e));
    });
    return () => { active = false; };
  }, [id, attempt]);
  async function action(operation) {
    if (busy) return;
    setBusy(true); setError('');
    try { await operation(); setEditing(false); setAdding(false); reload(value => value + 1); }
    catch (e) { setError(errorMessage(e)); }
    finally { setBusy(false); }
  }
  return <section>
    <Link className="d-inline-block mb-3" to="/turmas">← Minhas turmas</Link>
    {error && <div className="alert alert-danger" role="alert">{error} <button className="btn btn-sm btn-outline-danger" disabled={busy} onClick={() => { setError(''); reload(value => value + 1); }}>Tentar novamente</button></div>}
    {!classroom && !error && <p role="status">Carregando turma e snapshots…</p>}
    {classroom && <>
      <h1 className="h2 text-break">{classroom.name}</h1>
      <p>{classroom.members.length} aluno(s) · Dados dos últimos snapshots disponíveis</p>
      <p className="small text-body-secondary">Criada em {new Date(classroom.createdAt).toLocaleString('pt-BR')} · Turma atualizada em {new Date(classroom.updatedAt).toLocaleString('pt-BR')}</p>
      <div className="d-flex flex-wrap gap-2 mb-3">
        <button className="btn btn-outline-primary" disabled={busy} onClick={() => { setEditing(true); setAdding(false); }}>Editar nome</button>
        <button className="btn btn-primary" disabled={busy} onClick={() => { setAdding(true); setEditing(false); }}>Adicionar alunos</button>
        <a className="btn btn-outline-secondary" href="#classroom-students">Gerenciar alunos</a>
        <button className="btn btn-outline-danger" disabled={busy} onClick={() => {
          if (window.confirm(`Excluir a turma “${classroom.name}”? Os perfis e análises serão preservados.`)) action(async () => { await deleteClassroom(id); navigate('/turmas'); });
        }}>Excluir turma</button>
      </div>
      {busy && <p role="status">Aguarde…</p>}
      {editing && <ClassroomNameForm initialName={classroom.name} busy={busy} onCancel={() => setEditing(false)} onSave={name => action(() => renameClassroom(id, name))} />}
      {adding && <ClassroomMembersForm existingIds={classroom.members.map(m => m.gitHubProfileId)} busy={busy} onCancel={() => setAdding(false)} onSubmit={(_, ids) => action(() => addClassroomMembers(id, ids))} />}
      <div className="row align-items-end g-2 mb-3">
        <div className="col-12 col-md-6"><h2 className="h4 mb-0">Dashboard da turma</h2></div>
        <div className="col-12 col-md-6"><label htmlFor="dashboard-student" className="form-label">Visualizar:</label>
          <select id="dashboard-student" className="form-select" value={selectedStudent?.gitHubProfileId ?? ''} onChange={event => setSelectedId(event.target.value)}>
            <option value="">Todos os alunos</option>
            {classroom.members.map(student => <option key={student.gitHubProfileId} value={student.gitHubProfileId}>{student.name || student.username} (@{student.username})</option>)}
          </select></div>
      </div>
      {selectedStudent && <section className="card mb-3" aria-label="Resumo do aluno selecionado"><div className="card-body">
        <div className="d-flex align-items-center gap-3 mb-2">
          <img src={selectedStudent.avatarUrl} width="56" height="56" className="rounded-circle flex-shrink-0" alt="" />
          <div className="profile-details text-break"><h3 className="h5 mb-1">{selectedStudent.name || selectedStudent.username}</h3><span className="text-body-secondary">@{selectedStudent.username}</span></div>
        </div>
        <p className="small mb-2">{selectedStudent.analyzedAt
          ? `Última análise: ${new Date(selectedStudent.analyzedAt).toLocaleString('pt-BR')} · ${selectedStudent.isComplete ? 'Sem cortes nas regras de inspeção.' : 'Cobertura parcial.'}`
          : 'Sem análise disponível.'}</p>
        <div className="d-flex flex-wrap gap-2 mb-3">{selectedStudent.skills.map(skill => <span className="badge text-bg-secondary text-wrap" key={skill}>{skill}</span>)}</div>
        <button className="btn btn-outline-primary" disabled={busy || !selectedStudent.analyzedAt} onClick={() => action(async () => onOpen(await openSavedAnalysis(selectedStudent.gitHubProfileId)))}>Ver perfil completo</button>
      </div></section>}
      <p className="small text-body-secondary">{selectedStudent
        ? 'Tecnologias e categorias mostram repositórios com evidência deste aluno. Um repositório conta uma vez em cada categoria. Evidência não representa proficiência.'
        : 'Cada aluno conta no máximo uma vez por tecnologia ou categoria, independentemente do número de repositórios. Evidência não representa proficiência. Abrir a turma não atualiza análises.'}</p>
      {!selectedStudent && classroom.members.some(m => !m.analyzedAt || m.isComplete === false) && <p className="alert alert-warning">Há alunos sem snapshot ou com cobertura parcial. Os gráficos consideram somente as evidências disponíveis.</p>}
      <div className="row g-3 mb-4">
        <div className="col-12 col-lg-6"><ChartPanel title={selectedStudent ? 'Tecnologias do aluno' : 'Tecnologias mais presentes na turma'}
          technologySelection rows={selectedStudent ? selectedStudent.dashboard?.technologies ?? [] : classroom.technologies} unit={selectedStudent ? 'Repositórios' : 'Alunos'}
          description={selectedStudent ? 'Tecnologias, por número de repositórios com evidência no snapshot do aluno.' : 'Tecnologias, por número de alunos com evidência. React em sete repositórios do mesmo aluno conta como um aluno.'} /></div>
        <div className="col-12 col-lg-6"><ChartPanel title={selectedStudent ? 'Categorias do aluno' : 'Categorias da turma'}
          rows={selectedStudent ? selectedStudent.dashboard?.categories ?? [] : classroom.categories} unit={selectedStudent ? 'Repositórios' : 'Alunos'}
          description={selectedStudent ? 'Repositórios distintos com evidência em cada categoria do aluno.' : 'Alunos com pelo menos uma skill na categoria. São utilizadas apenas categorias encontradas nos snapshots.'} /></div>
      </div>
      <ActivityPanel activity={selectedStudent ? selectedStudent.dashboard?.commitActivity : classroom.commitActivity} classroom={!selectedStudent} />
      <h2 className="h4" id="classroom-students">Alunos da turma</h2>
      {classroom.members.length === 1 && <p className="small text-body-secondary">A turma precisa manter um aluno. Para remover o último, adicione outro ou exclua a turma.</p>}
      <div className="row g-3">{classroom.members.map(student => <div className="col-12 col-lg-6" key={student.gitHubProfileId}>
        <article className="card h-100" aria-label={`Aluno @${student.username}`}><div className="card-body">
          <div className="d-flex align-items-center gap-3 mb-3"><img src={student.avatarUrl} width="56" height="56" className="rounded-circle flex-shrink-0" alt="" />
            <div className="profile-details text-break"><h3 className="h5">{student.name || student.username}</h3><span className="text-body-secondary">@{student.username}</span></div></div>
          <div className="d-flex flex-wrap gap-2 mb-3">{student.skills.map(skill => <span className="badge text-bg-secondary text-wrap" key={skill}>{skill}</span>)}</div>
          {student.analyzedAt ? <><p className="small mb-2">{age(student.analyzedAt)} · {new Date(student.analyzedAt).toLocaleString('pt-BR')}</p>
            <p className="small text-body-secondary">{student.isExpired ? 'Snapshot antigo; leitura preservada, sem atualização automática.' : 'Snapshot recente.'} {student.isComplete ? 'Sem cortes nas regras de inspeção.' : 'Cobertura parcial.'}</p></> : <p>Sem análise disponível. Os demais alunos continuam no dashboard.</p>}
          <div className="d-flex flex-wrap gap-2"><button className="btn btn-primary" disabled={busy || !student.analyzedAt} onClick={() => action(async () => onOpen(await openSavedAnalysis(student.gitHubProfileId)))}>Ver perfil</button>
            <button className="btn btn-outline-danger" disabled={busy || classroom.members.length <= 1} onClick={() => action(() => removeClassroomMember(id, student.gitHubProfileId))}>Remover da turma</button></div>
        </div></article>
      </div>)}</div>
    </>}
  </section>;
}
