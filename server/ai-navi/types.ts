/** Internal server contracts. The repository layer must scope personal events to the authenticated user. */
export interface Material {
  id: string;
  title: string;
  description: string;
  topics: string[];
  published: boolean;
}
export interface LearningEvent {
  userId: string;
  materialId: string;
  kind: 'view' | 'complete';
  at: number;
}
export interface Feedback {
  materialId: string;
  value: 'interested' | 'not-interested';
}
/** Feedback contains only the latest, non-retracted reaction per material. */
export interface PersonalInput {
  interests: string[];
  goals: string[];
  events: LearningEvent[];
  feedback: Feedback[];
}
export interface PopularityItem {
  materialId: string;
  users: number;
  completedUsers: number;
  /** A fraction in [0, 1], not a rounded display percentage. */
  ratio: number | null;
  popular: boolean;
}
export interface PopularitySummary {
  start: number;
  end: number;
  activeUsers: number;
  items: PopularityItem[];
}
export type CandidateSource = 'interest' | 'history' | 'feedback' | 'popularity' | 'catalog';
export interface Candidate {
  materialId: string;
  sources: CandidateSource[];
}
export interface DiscoveryHistory {
  id: string;
  materialId: string;
  kind: 'view' | 'complete';
  title: string;
  description: string;
  topics: string[];
}
export interface Suggestion {
  theme: string;
  reason: string;
  firstStep: string;
  basis: 'history' | 'popularity';
  evidenceIds: string[];
  relatedMaterialIds: string[];
}
