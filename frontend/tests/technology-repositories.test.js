import test from 'node:test';
import assert from 'node:assert/strict';
import { technologyRepositories, profileTechnologyPath } from '../src/analysis/technologyRepositories.js';

test('technology uses evidence repository IDs, deduplicates reasons and never infers from language', () => {
  const analysis = { profile: { repositories: [{ id: 1, name: 'api', language: 'Python' }, { id: 2, name: 'other', language: 'C#' }] },
    skills: [{ name: 'C#', evidence: [{ repositoryId: 1, reason: 'src/api.csproj' }, { repositoryId: 1, reason: 'src/api.csproj' }] }] };
  const result = technologyRepositories(analysis, 'c#');
  assert.deepEqual(result.map(r => r.name), ['api']);
  assert.deepEqual(result[0].reasons, ['src/api.csproj']);
  assert.deepEqual(technologyRepositories(analysis, 'Unknown'), []);
  assert.equal(profileTechnologyPath('example', 'C#'), '/perfil/example/tecnologia/C%23');
});
