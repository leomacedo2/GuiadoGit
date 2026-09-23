import { limitTechnologies } from './technologyLimit.js';
const rank = rows => rows.filter(row => row.count > 0).sort((a, b) => b.count - a.count || a.label.localeCompare(b.label, 'pt-BR'));

export function technologyEvidence(skills, limit = 10) {
  return limitTechnologies(rank(skills.map(skill => ({ label: skill.name, count: skill.repositoryCount }))), limit);
}
export function languageEvidence(skills, limit = 8) {
  return technologyEvidence(skills.filter(skill => skill.category === 'Linguagens'), limit);
}
export function categoryEvidence(skills) {
  const categories = new Map();
  for (const skill of skills) {
    if (!categories.has(skill.category)) categories.set(skill.category, new Set());
    for (const evidence of skill.evidence || []) categories.get(skill.category).add(evidence.repositoryId);
  }
  return rank([...categories].map(([label, ids]) => ({ label, count: ids.size })));
}
