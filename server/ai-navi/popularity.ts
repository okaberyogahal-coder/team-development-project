import type { LearningEvent, Material, PopularitySummary } from './types';

/** Computes global aggregates; raw identities must never be included in an AI request. */
export function aggregatePopularity(
  materials: Material[], events: LearningEvent[], now: number,
): PopularitySummary {
  if (!Number.isFinite(now)) throw new Error('Invalid aggregation time');
  const start = now - 30 * 86400000;
  const counts = new Map<string, { users: Set<string>; completed: Set<string> }>();
  for (const material of materials) {
    if (material.published) counts.set(material.id, { users: new Set(), completed: new Set() });
  }
  const active = new Set<string>();
  for (const event of events) {
    const count = counts.get(event.materialId);
    if (!count || !Number.isFinite(event.at) || event.at <= start || event.at > now) continue;
    active.add(event.userId);
    count.users.add(event.userId);
    if (event.kind === 'complete') count.completed.add(event.userId);
  }
  const activeUsers = active.size;
  const items = [...counts].map(([materialId, count]) => ({
    materialId,
    users: count.users.size,
    completedUsers: count.completed.size,
    ratio: activeUsers ? count.users.size / activeUsers : null,
    popular: activeUsers > 0 && count.users.size * 100 >= activeUsers * 80,
  }));
  items.sort((a, b) => b.completedUsers - a.completedUsers || b.users - a.users
    || (a.materialId < b.materialId ? -1 : a.materialId > b.materialId ? 1 : 0));
  return { start, end: now, activeUsers, items };
}
