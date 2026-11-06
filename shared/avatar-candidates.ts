import { BASE_AVATARS } from './avatar-types';

// Avatar-domain values only. These are NOT registration/DB field names or values.
export type AvatarGenderState =
  | { status: 'ready'; gender: 'female' | 'male'; source: 'profile' | 'development-fixture' | 'selection' }
  | { status: 'loading' | 'unset' | 'unconnected' | 'error' };

export function getAvatarCandidates(state: AvatarGenderState) {
  if (state.status !== 'ready') return [];
  const ids = state.gender === 'female'
    ? ['young-woman', 'middle-aged-woman', 'elderly-woman']
    : state.gender === 'male' ? ['young-man', 'middle-aged-man', 'elderly-man'] : [];
  return ids.map((id, i) => ({ ...BASE_AVATARS.find(base => base.id === id)!, ageLabel: ['若者', '大人', 'シニア'][i], description: ['明るく軽やかな印象', '落ち着いた自然な印象', '穏やかでやさしい印象'][i] }));
}

export function getAllAvatarCandidates() {
  const list = [
    { id: 'young-man', ageLabel: '若者男性', description: '明るく軽やかな印象' },
    { id: 'young-woman', ageLabel: '若者女性', description: '明るく軽やかな印象' },
    { id: 'middle-aged-man', ageLabel: '大人男性', description: '落ち着いた自然な印象' },
    { id: 'middle-aged-woman', ageLabel: '大人女性', description: '落ち着いた自然な印象' },
    { id: 'elderly-man', ageLabel: 'シニア男性', description: '穏やかでやさしい印象' },
    { id: 'elderly-woman', ageLabel: 'シニア女性', description: '穏やかでやさしい印象' },
  ];
  return list.map(item => ({ ...BASE_AVATARS.find(base => base.id === item.id)!, ...item }));
}

// Replace this boundary once the registration owner confirms field/value/source.
// Deliberately does not inspect a guessed property on a user/session object.
export function getRegisteredAvatarGender(): AvatarGenderState {
  return { status: 'unconnected' };
}

export function resolveAvatarGender(registered: AvatarGenderState, fixture: unknown, development: boolean): AvatarGenderState {
  if (development && registered.status === 'unconnected' && (fixture === 'female' || fixture === 'male')) {
    return { status: 'ready', gender: fixture, source: 'development-fixture' };
  }
  return registered;
}
