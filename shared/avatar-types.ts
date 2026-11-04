export interface AvatarChoice {
  body: number;
  face: number;
  hair: number;
  outerwear: number;
  pants: number;
  shoes: number;
  glasses: number;
  accessory: number;
  hairColor: string;
  topColor: string;
  pantsColor: string;
}

export const DEFAULT_AVATAR_CHOICE: AvatarChoice = {
  body: 0,
  face: 0,
  hair: 0,
  outerwear: 0,
  pants: 0,
  shoes: 0,
  glasses: -1,
  accessory: -1,
  hairColor: '#60412E',
  topColor: '#2563EB',
  pantsColor: '#627B99',
};

export const AVATAR_OPTIONS = {
  face: [
    { label: 'にこやか', value: 0 },
    { label: 'おだやか', value: 1 },
    { label: 'きりっと', value: 2 },
  ],
  hair: [
    { label: 'ショートヘア', value: 0 },
    { label: 'ミディアムヘア', value: 1 },
  ],
  outerwear: [
    { label: 'カジュアルジャケット', value: 0 },
    { label: 'シンプルシャツ', value: 1 },
    { label: 'スポーティウェア', value: 2 },
  ],
  pants: [
    { label: 'チノパン', value: 0 },
    { label: 'スラックス', value: 1 },
  ],
  shoes: [
    { label: 'ウォーキングスニーカー', value: 0 },
    { label: 'カジュアルシューズ', value: 1 },
    { label: 'レザーシューズ', value: 2 },
  ],
  glasses: [
    { label: 'なし', value: -1 },
    { label: '丸メガネ', value: 0 },
    { label: 'スクエアメガネ', value: 1 },
  ],
  accessory: [
    { label: 'なし', value: -1 },
    { label: 'ダンディひげ', value: 0 },
    { label: 'カイゼルひげ', value: 1 },
    { label: 'ヘッドホン', value: 2 },
    { label: 'ピエロの鼻', value: 3 },
    { label: 'おしゃぶり', value: 4 },
  ],
};

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

