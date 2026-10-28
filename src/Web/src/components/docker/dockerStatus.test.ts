import { describe, expect, it } from 'vitest';
import type { DockerContainer, DockerImage } from '../../api/docker';
import { availableActions, groupByComposeProject, imageReference, imageUsage, shortImageId } from './dockerStatus';

function container(overrides: Partial<DockerContainer>): DockerContainer {
  return {
    id: 'id-web',
    name: 'web',
    image: 'nginx:latest',
    state: 'Running',
    status: 'Up 2 hours',
    health: null,
    ports: '',
    createdAt: '',
    compose: null,
    ...overrides,
  };
}

function image(overrides: Partial<DockerImage>): DockerImage {
  return {
    id: 'sha256:0123456789abcdef0123',
    repository: 'nginx',
    tag: 'latest',
    size: '192MB',
    createdSince: '2 weeks ago',
    ...overrides,
  };
}

describe('dockerStatus', () => {
  it('offers removal for every state except removing', () => {
    expect(availableActions(container({ state: 'Running' }))).toEqual(['restart', 'stop', 'remove']);
    expect(availableActions(container({ state: 'Exited' }))).toEqual(['start', 'remove']);
    expect(availableActions(container({ state: 'Unknown' }))).toEqual(['remove']);
    expect(availableActions(container({ state: 'Removing' }))).toEqual([]);
  });

  it('groups compose containers by project and keeps standalone ones last', () => {
    const shop = { project: 'shop', workingDirectory: '/srv/shop' };
    const groups = groupByComposeProject([
      container({ id: '1', name: 'solo' }),
      container({ id: '2', name: 'shop-web', compose: shop }),
      container({ id: '3', name: 'shop-db', compose: shop }),
    ]);

    expect(groups.map((group) => group.compose?.project ?? null)).toEqual(['shop', null]);
    expect(groups[0]!.containers.map((item) => item.name)).toEqual(['shop-web', 'shop-db']);
    expect(groups[1]!.containers.map((item) => item.name)).toEqual(['solo']);
  });

  it('references dangling images by id', () => {
    expect(imageReference(image({}))).toBe('nginx:latest');
    expect(imageReference(image({ repository: '<none>', tag: '<none>' }))).toBe('sha256:0123456789abcdef0123');
    expect(shortImageId('sha256:0123456789abcdef0123')).toBe('0123456789ab');
  });

  it('counts containers using an image by reference, untagged latest or id', () => {
    const containers = [
      container({ id: '1', image: 'nginx:latest' }),
      container({ id: '2', image: 'nginx' }),
      container({ id: '3', image: '0123456789ab' }),
      container({ id: '4', image: 'redis:7' }),
    ];

    expect(imageUsage(image({}), containers)).toBe(3);
  });
});
