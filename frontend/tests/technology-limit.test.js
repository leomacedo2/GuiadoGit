import test from 'node:test';
import assert from 'node:assert/strict';
import { limitTechnologies } from '../src/analysis/technologyLimit.js';
import { technologyEvidence, languageEvidence } from '../src/analysis/chartData.js';
import { activityWindow } from '../src/analysis/activityData.js';

test('shared display limit defaults to five, accepts ten/all and does not reorder or mutate', () => {
  const skills = Array.from({ length: 17 }, (_, i) => ({ name: `Tech${i}`, category: 'Linguagens', repositoryCount: 30 - i }));
  const rows = technologyEvidence(skills, 'all');
  const before = structuredClone(rows);
  assert.equal(rows.length, 17);
  assert.deepEqual(languageEvidence(skills, 'all'), rows);
  assert.deepEqual(limitTechnologies(rows), rows.slice(0, 5));
  assert.deepEqual(limitTechnologies(rows, '10'), rows.slice(0, 10));
  assert.deepEqual(limitTechnologies(rows, 'all'), rows);
  assert.deepEqual(limitTechnologies(rows, '5'), rows.slice(0, 5));
  assert.deepEqual(rows, before);
});

test('all commit series survive six-month slicing and switching back to top five', () => {
  const activity = { months: Array.from({ length: 12 }, (_, i) => `2026-${String(i + 1).padStart(2, '0')}`),
    series: Array.from({ length: 17 }, (_, i) => ({ name: `Tech${i}`, counts: [200 - i * 2, ...Array(10).fill(0), i] })) };
  const before = structuredClone(activity);
  for (const limit of ['5', '10', 'all']) {
    const full = activityWindow(activity, 12, limit), recent = activityWindow(activity, 6, limit);
    assert.equal(full.series.length, limit === 'all' ? 17 : Number(limit));
    assert.deepEqual(full.series.map(s => s.name), recent.series.map(s => s.name));
    assert.ok(recent.series.every(s => s.counts.length === 6));
  }
  assert.equal(activityWindow(activity).series.length, 5);
  assert.deepEqual(activity, before);
});
