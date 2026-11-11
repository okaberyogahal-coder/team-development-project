import type { Candidate, CandidateSource, Material, PersonalInput, PopularitySummary } from './types';

const normalize = (value: string) => value.normalize('NFKC').trim().toLowerCase();
function matches(topics: string[], terms: string[]) {
  const normalizedTopics = topics.map(normalize).filter(Boolean);
  return terms.map(normalize).filter(Boolean).some(term =>
    normalizedTopics.some(topic => topic.includes(term) || term.includes(topic)));
}

/** Candidate retrieval only. Semantic selection and explanations belong to the AI adapter. */
export function selectCandidates(
  materials: Material[], personal: PersonalInput, popularity: PopularitySummary,
): Candidate[] {
  const catalog = new Map(materials.filter(material => material.published).map(material => [material.id, material]));
  const historyTopics = personal.events.flatMap(event => catalog.get(event.materialId)?.topics ?? []);
  const feedback = new Map(personal.feedback.map(item => [item.materialId, item.value]));
  const ranks = new Map(popularity.items.map((item, index) => [item.materialId, index]));
  const popular = new Set(popularity.items.filter(item => item.popular).map(item => item.materialId));
  const ranked: { candidate: Candidate; priority: number; popularityRank: number }[] = [];
  for (const material of catalog.values()) {
    const reaction = feedback.get(material.id);
    if (reaction === 'not-interested') continue;
    const sources: CandidateSource[] = [];
    if (reaction === 'interested') sources.push('feedback');
    if (matches(material.topics, [...personal.interests, ...personal.goals])) sources.push('interest');
    if (matches(material.topics, historyTopics)) sources.push('history');
    if (popular.has(material.id)) sources.push('popularity');
    if (!sources.length) sources.push('catalog');
    const priority = ['feedback', 'interest', 'history', 'popularity', 'catalog'].indexOf(sources[0]);
    ranked.push({ candidate: { materialId: material.id, sources }, priority,
      popularityRank: ranks.get(material.id) ?? Number.MAX_SAFE_INTEGER });
  }
  ranked.sort((a, b) => a.priority - b.priority || a.popularityRank - b.popularityRank
    || (a.candidate.materialId < b.candidate.materialId ? -1 : a.candidate.materialId > b.candidate.materialId ? 1 : 0));
  return ranked.slice(0, 20).map(item => item.candidate);
}
