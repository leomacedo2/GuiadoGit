import test from 'node:test';
import assert from 'node:assert/strict';
import { categoryEvidence, languageEvidence, technologyEvidence } from '../src/analysis/chartData.js';

const skills = [
  { name: 'JavaScript', category: 'Linguagens', repositoryCount: 3, evidence: [{ repositoryId: 1 }, { repositoryId: 2 }, { repositoryId: 3 }] },
  { name: 'TypeScript', category: 'Linguagens', repositoryCount: 2, evidence: [{ repositoryId: 2 }, { repositoryId: 3 }] },
  { name: 'React', category: 'Frontend', repositoryCount: 1, evidence: [{ repositoryId: 2 }, { repositoryId: 2 }] }
];
test('language counts preserve actual repository totals instead of percentages', () => {
  assert.deepEqual(languageEvidence(skills), [{ label: 'JavaScript', count: 3 }, { label: 'TypeScript', count: 2 }]);
});
test('category counts deduplicate repositories across skills and reasons', () => {
  assert.deepEqual(categoryEvidence(skills), [{ label: 'Linguagens', count: 3 }, { label: 'Frontend', count: 1 }]);
});
test('technology ranking uses counts and respects display limit without mutating input', () => {
  const before = structuredClone(skills);
  assert.deepEqual(technologyEvidence(skills, 2), [{ label: 'JavaScript', count: 3 }, { label: 'TypeScript', count: 2 }]);
  assert.deepEqual(skills, before);
});
test('empty evidence does not invent categories or languages', () => {
  assert.deepEqual(categoryEvidence([]), []); assert.deepEqual(languageEvidence([]), []);
  assert.deepEqual(technologyEvidence([{ name: 'Python', repositoryCount: 0 }]), []);
});
