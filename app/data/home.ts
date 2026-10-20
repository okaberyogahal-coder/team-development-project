import type { IconName } from "../../shared/icons";
export type Feature = {
  icon: IconName;
  title: string;
  description: string;
  color: string;
  accent: string;
  ring: string;
};

export const features: Feature[] = [
  { icon: "book", title: "学ぶ", description: "趣味、デジタル、仕事、生活の知恵。自分のペースで、新しい知識や体験に出会えます。", color: "bg-blue-50", accent: "text-blue-700", ring: "ring-blue-100" },
  { icon: "sprout", title: "教える", description: "料理のコツも、仕事の経験も、あなたの得意なことが誰かの新しい一歩になります。", color: "bg-yellow-50", accent: "text-yellow-800", ring: "ring-yellow-100" },
  { icon: "chat", title: "つながる", description: "アバターで広場を歩き、共通の興味を持つ仲間と世代をこえて交流できます。", color: "bg-rose-50", accent: "text-rose-700", ring: "ring-rose-100" },
  { icon: "sparkles", title: "未来ナビ", description: "興味や学びたいことをもとに、次の挑戦や学び合う相手を提案する案内役です。", color: "bg-cyan-50", accent: "text-cyan-800", ring: "ring-cyan-100" },
];

export const flowSteps: { label: string; detail: string; icon: IconName; color: string }[] = [
  {
    label: "学ぶ",
    detail: "知識と出会う",
    icon: "open-book",
    color: "bg-blue-500",
  },
  {
    label: "体験する",
    detail: "まず試してみる",
    icon: "sprout",
    color: "bg-green-500",
  },
  {
    label: "交流する",
    detail: "誰かと広げる",
    icon: "chat",
    color: "bg-pink-500",
  },
  {
    label: "次の挑戦へ",
    detail: "未来を更新する",
    icon: "flag",
    color: "bg-orange-400",
  },
];

export const aiSuggestions = [
  "英会話に挑戦してみませんか？",
  "写真を趣味にしてみませんか？",
  "デジタルスキルを学んでみませんか？",
  "自分の経験を誰かに教えてみませんか？",
];
