export type Feature = {
  icon: string;
  title: string;
  description: string;
  color: string;
  accent: string;
  ring: string;
};

export const features: Feature[] = [
  {
    icon: "📘",
    title: "学ぶ",
    description: "新しい知識やスキルを、自分のペースで楽しく学べます。",
    color: "bg-blue-50",
    accent: "text-blue-600",
    ring: "ring-blue-100",
  },
  {
    icon: "🎨",
    title: "体験する",
    description: "趣味や興味のある分野に、オンラインで気軽に挑戦できます。",
    color: "bg-green-50",
    accent: "text-green-600",
    ring: "ring-green-100",
  },
  {
    icon: "💬",
    title: "交流する",
    description: "世代や場所を超えて、人と人があたたかくつながれます。",
    color: "bg-pink-50",
    accent: "text-pink-600",
    ring: "ring-pink-100",
  },
  {
    icon: "💻",
    title: "スキル習得",
    description: "仕事や将来に役立つデジタルスキルを身につけられます。",
    color: "bg-sky-50",
    accent: "text-sky-600",
    ring: "ring-sky-100",
  },
  {
    icon: "🤝",
    title: "経験を共有",
    description: "仕事経験、趣味、人生の知恵を次の世代へ伝えられます。",
    color: "bg-orange-50",
    accent: "text-orange-500",
    ring: "ring-orange-100",
  },
  {
    icon: "✨",
    title: "AI未来ナビ",
    description: "興味や目標から、次に挑戦できそうなことを提案します。",
    color: "bg-violet-50",
    accent: "text-violet-600",
    ring: "ring-violet-100",
  },
];

export const flowSteps = [
  {
    label: "学ぶ",
    detail: "知識と出会う",
    icon: "📖",
    color: "bg-blue-500",
  },
  {
    label: "体験する",
    detail: "まず試してみる",
    icon: "🌱",
    color: "bg-green-500",
  },
  {
    label: "交流する",
    detail: "誰かと広げる",
    icon: "💬",
    color: "bg-pink-500",
  },
  {
    label: "次の挑戦へ",
    detail: "未来を更新する",
    icon: "🚩",
    color: "bg-orange-400",
  },
];

export const aiSuggestions = [
  "英会話に挑戦してみませんか？",
  "写真を趣味にしてみませんか？",
  "デジタルスキルを学んでみませんか？",
  "自分の経験を誰かに教えてみませんか？",
];
