import type { Suggestion } from './types';

function text(value: unknown): string {
  if (typeof value !== 'string') throw new Error('Invalid suggestion text');
  const result = value.trim();
  if (!result || result.length > 500) throw new Error('Invalid suggestion text length');
  return result;
}

/** Validate against the CURRENT public catalog; callers must render these strings as text. */
export function validateSuggestions(value: unknown, allowedIds: ReadonlySet<string>): Suggestion[] {
  if (!Array.isArray(value) || value.length > 3) throw new Error('Invalid suggestions array');
  const seen = new Set<string>();
  return value.map(item => {
    if (!item || typeof item !== 'object' || Array.isArray(item)) throw new Error('Invalid suggestion');
    const materialId = text(item.materialId);
    if (!allowedIds.has(materialId) || seen.has(materialId)) throw new Error('Invalid suggestion material');
    seen.add(materialId);
    return { materialId, reason: text(item.reason), nextLearning: text(item.nextLearning) };
  });
}
