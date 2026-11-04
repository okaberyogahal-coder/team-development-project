import PART_MANIFEST from './avatar-parts.json';

// Six candidate IDs are retained independently of Web publication. The young-man
// prototype explicitly reuses Body_010; the Unity catalog controls availability.
export const LEGACY_BASE_AVATAR_ID = 'creative-free-body-010';
export const BASE_AVATARS = [
  { id: 'young-woman', label: '若い女性', available: false },
  { id: 'young-man', label: '若い男性', available: false },
  { id: 'middle-aged-woman', label: '大人の女性', available: false },
  { id: 'middle-aged-man', label: '大人の男性', available: false },
  { id: 'elderly-woman', label: 'シニアの女性', available: false },
  { id: 'elderly-man', label: 'シニアの男性', available: false },
] as const;

export interface AvatarChoice {
  baseAvatarId: string;
  body: number;
  face: number;
  hair: number;
  outerwear: number;
  pants: number;
  shoes: number;
  glasses: number;
  accessory: number;
  costume: number;
  hairColor: string;
  topColor: string;
  pantsColor: string;
  skinColor: string;
}

export const DEFAULT_AVATAR_CHOICE: AvatarChoice = {
  baseAvatarId: LEGACY_BASE_AVATAR_ID,
  body: 0,
  face: 0,
  hair: 0,
  outerwear: 0,
  pants: 0,
  shoes: 0,
  glasses: -1,
  accessory: -1,
  costume: -1,
  hairColor: '#60412E',
  topColor: '#2563EB',
  pantsColor: '#627B99',
  // Empty means the original gradient atlas, preserving old users exactly.
  skinColor: '',
};

// One append-only manifest is consumed by Web and the Unity library builder.
// Existing indices are stable; new hair/pants options are appended only.
const slotOptions = (key: string) => PART_MANIFEST.slots.find(slot => slot.key === key)!.parts;
export const AVATAR_OPTIONS = {
  face: slotOptions('face'), hair: slotOptions('hair'), outerwear: slotOptions('outerwear'),
  pants: slotOptions('pants'), shoes: slotOptions('shoes'),
  glasses: [{ label: 'なし', value: -1, prefab: '' }, ...slotOptions('glasses')],
  accessory: [{ label: 'なし', value: -1, prefab: '' }, ...slotOptions('accessory')],
};

export const COSTUME_OPTIONS = [
  { value: -1, label: '通常の服' },
  { value: 0, label: 'ピエロの衣装' },
  { value: 1, label: 'ネズミの着ぐるみ' },
] as const;

export const SKIN_COLORS = [
  { label: '元の肌色', value: '', swatch: '#EBA36E' },
  { label: '肌色・ライト', value: '#F3D5BF', swatch: '#F3D5BF' },
  { label: '肌色・ライトミディアム', value: '#DFB18E', swatch: '#DFB18E' },
  { label: '肌色・ミディアム', value: '#C58F67', swatch: '#C58F67' },
  { label: '肌色・ブラウン', value: '#9A6747', swatch: '#9A6747' },
  { label: '肌色・ディープ', value: '#66432F', swatch: '#66432F' },
] as const;

export const COLOR_PALETTE = {
  tops: [
    { label: 'ミライフブルー', hex: '#2563EB' },
    { label: 'フォレストグリーン', hex: '#16A34A' },
    { label: 'サンセットオレンジ', hex: '#EA580C' },
    { label: 'チェリーピンク', hex: '#E11D48' },
    { label: 'マスタードイエロー', hex: '#CA8A04' },
    { label: 'ノーブルパープル', hex: '#7C3AED' },
    { label: 'チャコールグレー', hex: '#374151' },
    { label: 'ピュアホワイト', hex: '#F3F4F6' },
    { label: 'やさしいピンク', hex: '#E8A6B8' },
    { label: 'ミライフ水色', hex: '#8CCBE6' },
  ],
  pants: [
    { label: 'インディゴデニム', hex: '#627B99' },
    { label: 'ダークネイビー', hex: '#1E293B' },
    { label: 'ライトベージュ', hex: '#D7C4A5' },
    { label: 'オリーブカーキ', hex: '#4B5548' },
    { label: 'アッシュグレー', hex: '#4B5563' },
    { label: 'ブラック', hex: '#111827' },
  ],
  hair: [
    { label: 'ナチュラルブラウン', hex: '#60412E' },
    { label: 'ダークブラック', hex: '#1C1917' },
    { label: 'ライトアッシュ', hex: '#8B7355' },
    { label: 'シルバーグレー', hex: '#9CA3AF' },
    { label: 'ゴールドブロンド', hex: '#D4AF37' },
  ],
};


/** Migrate old saves without changing valid outfit choices or their index order. */
export function restoreAvatarChoice(value: unknown): { choice: AvatarChoice; adjusted: boolean } | null {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return null;
  const saved = value as Record<string, unknown>;
  const choice = { ...DEFAULT_AVATAR_CHOICE };
  let adjusted = false;
  if (typeof saved.baseAvatarId === 'string' && BASE_AVATARS.some(base => base.id === saved.baseAvatarId)) choice.baseAvatarId = saved.baseAvatarId;
  else if (saved.baseAvatarId !== undefined && saved.baseAvatarId !== LEGACY_BASE_AVATAR_ID) adjusted = true;
  for (const key of ['body', 'face', 'hair', 'outerwear', 'pants', 'shoes', 'glasses', 'accessory'] as const) {
    const options = key === 'body' ? [0] : AVATAR_OPTIONS[key].map(option => option.value);
    if (saved[key] === undefined) continue;
    if (typeof saved[key] === 'number' && options.includes(saved[key])) choice[key] = saved[key];
    else adjusted = true;
  }
  if (saved.costume !== undefined) {
    if (COSTUME_OPTIONS.some(option => option.value === saved.costume)) choice.costume = saved.costume as number;
    else adjusted = true;
  }
  for (const key of ['hairColor', 'topColor', 'pantsColor'] as const) {
    if (saved[key] === undefined) continue;
    if (typeof saved[key] === 'string' && /^#[0-9a-f]{6}$/i.test(saved[key])) choice[key] = saved[key];
    else adjusted = true;
  }
  if (saved.skinColor !== undefined) {
    if (typeof saved.skinColor === 'string' && (saved.skinColor === '' || /^#[0-9a-f]{6}$/i.test(saved.skinColor))) choice.skinColor = saved.skinColor;
    else adjusted = true;
  }
  return { choice, adjusted };
}
