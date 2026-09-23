import test from 'node:test';
import assert from 'node:assert/strict';
import { activityWindow, monthLabel } from '../src/analysis/activityData.js';

test('period filter preserves persisted counts and gaps without inventing activity', () => {
  const activity = { months: ['2026-01', '2026-02', '2026-03'], series: [{ name: 'React', counts: [2, null, 3] }, { name: 'Python', counts: [1, null, null] }] };
  const before = structuredClone(activity);
  assert.deepEqual(activityWindow(activity, 2), { months: ['2026-02', '2026-03'], series: [{ name: 'React', counts: [null, 3] }, { name: 'Python', counts: [null, null] }] });
  assert.deepEqual(activity, before);
  assert.equal(monthLabel('2026-09'), '09/2026');
});
test('legacy snapshots without activity have an empty view rather than inferred dates', () => {
  assert.deepEqual(activityWindow(undefined), { months: [], series: [] });
});


test('ranks top five on twelve months and preserves them with zeros in six months', () => {
  const activity = { months: Array.from({ length: 12 }, (_, i) => `2026-${String(i + 1).padStart(2, '0')}`),
    series: Array.from({ length: 7 }, (_, i) => ({ name: `Tech${i}`, counts: [100 - i, ...Array(11).fill(0)] })) };
  const twelve = activityWindow(activity, 12), six = activityWindow(activity, 6);
  assert.equal(twelve.series.length, 5);
  assert.deepEqual(six.series.map(s => s.name), twelve.series.map(s => s.name));
  assert.ok(six.series.every(s => s.counts.length === 6 && s.counts.every(n => n === 0)));
});

test('classroom top ten retains positions six to ten and the same ranking in six months', () => {
  const activity = { months: Array.from({ length: 12 }, (_, i) => `2026-${String(i + 1).padStart(2, '0')}`),
    series: Array.from({ length: 12 }, (_, i) => ({ name: i === 8 ? 'TSQL' : `Tech${i}`, counts: [200 - i * 2, ...Array(10).fill(0), i] })) };
  const before = structuredClone(activity);
  const twelve = activityWindow(activity, 12, 10), six = activityWindow(activity, 6, 10);
  assert.equal(twelve.series.length, 10);
  assert.deepEqual(twelve.series.slice(5).map(s => s.name), ['Tech5', 'Tech6', 'Tech7', 'TSQL', 'Tech9']);
  // Recent commits have the opposite ranking, but must retain the twelve-month series.
  assert.deepEqual(six.series.map(s => s.name), twelve.series.map(s => s.name));
  assert.equal(activityWindow(activity).series.length, 5);
  assert.deepEqual(activity, before);
});
