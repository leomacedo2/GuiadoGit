export function technologyRepositories(analysis, technology) {
  const evidence = (analysis?.skills ?? []).filter(skill => skill.name.toLowerCase() === technology.toLowerCase())
    .flatMap(skill => skill.evidence ?? []);
  const byId = new Map();
  for (const item of evidence) {
    const key = String(item.repositoryId);
    if (!byId.has(key)) byId.set(key, new Set());
    if (item.reason) byId.get(key).add(item.reason);
  }
  return (analysis?.profile?.repositories ?? []).filter(repo => byId.has(String(repo.id)))
    .map(repo => ({ ...repo, reasons: [...byId.get(String(repo.id))] }));
}

export const profileTechnologyPath = (username, technology) =>
  `/perfil/${encodeURIComponent(username)}/tecnologia/${encodeURIComponent(technology)}`;
export const classroomTechnologyPath = (id, technology, studentId) =>
  `/turmas/${encodeURIComponent(id)}/tecnologia/${encodeURIComponent(technology)}${studentId ? `?aluno=${encodeURIComponent(studentId)}` : ''}`;
