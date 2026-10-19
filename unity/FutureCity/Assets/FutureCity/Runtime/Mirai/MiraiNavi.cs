using System.Collections.Generic;

namespace FutureCity
{
    /// <summary>未来ナビの提案1件（仕様 §6：courseId・理由・次に学ぶ内容）</summary>
    public class NaviSuggestion
    {
        public Course course;
        public string reason;
        public string nextLearning;
        public float score;
    }

    public class NaviResult
    {
        public List<NaviSuggestion> suggestions = new List<NaviSuggestion>();
        /// <summary>0件のときの理由と、入力の変え方の案</summary>
        public string emptyReason;
    }

    /// <summary>
    /// 未来ナビの提案エンジンの差し替え口（仕様 Navi.suggest）。
    /// 完成版は「実AI」が必須（BR-09）。Unity 側ではこのインターフェースを
    /// サーバー経由で AI を呼ぶ実装に差し替える（APIキーをUnityビルドに入れない）。
    /// このプロジェクトに入っているのは、画面と流れを確かめるためのモックです。
    /// </summary>
    public interface INaviProvider
    {
        NaviResult Suggest(MiraiProfile me, IList<Course> catalog, ICollection<string> completedIds);
    }

    /// <summary>
    /// モック（ルールベース）。本人のプロフィールと受講履歴から、ミライフ内の公開教材だけを最大3件。
    ///  ・学びたいことに合う教材
    ///  ・受講した教材の「次の一歩」になる教材
    ///  ・存在しない教材で件数を埋めない
    /// </summary>
    public class MockNaviProvider : INaviProvider
    {
        public const int MaxCandidates = 3;

        public NaviResult Suggest(MiraiProfile me, IList<Course> catalog, ICollection<string> completedIds)
        {
            var result = new NaviResult();
            var completedTags = new HashSet<string>();
            foreach (var c in catalog)
                if (completedIds.Contains(c.id))
                    foreach (var t in c.tags) completedTags.Add(t);

            var scored = new List<NaviSuggestion>();
            foreach (var c in catalog)
            {
                if (!c.published || c.mine || completedIds.Contains(c.id)) continue;
                string tag = c.tags.Count > 0 ? c.tags[0] : "";
                float score = 0f;
                string reason = null;

                if (me.learns.Contains(tag))
                {
                    score += 3f;
                    reason = $"あなたの「学びたいこと」にある「{tag}」の教材です。";
                    if (completedTags.Contains(tag))
                    {
                        score += 1f;
                        reason = $"「{tag}」の教材を受講済みなので、次の段階としておすすめです。";
                    }
                }
                else
                {
                    foreach (var done in completedTags)
                    {
                        if (MiraiData.Related.TryGetValue(done, out var rel) && System.Array.IndexOf(rel, tag) >= 0)
                        {
                            score += 2f;
                            reason = $"受講した「{done}」と相性のよいテーマです。";
                            break;
                        }
                    }
                    if (reason == null)
                    {
                        foreach (var want in me.learns)
                        {
                            if (MiraiData.Related.TryGetValue(want, out var rel) && System.Array.IndexOf(rel, tag) >= 0)
                            {
                                score += 1f;
                                reason = $"学びたい「{want}」とつながるテーマです。";
                                break;
                            }
                        }
                    }
                }
                if (reason == null) continue;

                // 年齢の利用は AI 利用設定に従う（表示とは別。ここでは理由文にだけ反映するモック）
                if (me.aiUsesAge && me.age >= 65 && c.minutes > 0 && c.minutes <= 12)
                {
                    score += 0.3f;
                    reason += $"（{c.minutes}分と短めで始めやすい内容です）";
                }

                scored.Add(new NaviSuggestion { course = c, reason = reason, nextLearning = Next(tag, catalog, c), score = score });
            }

            scored.Sort((a, b) => b.score.CompareTo(a.score));
            // 同じテーマばかりにならないよう、まず1テーマ1件ずつ選び、足りなければ残りから補う
            var usedTags = new HashSet<string>();
            foreach (var s in scored)
            {
                if (result.suggestions.Count >= MaxCandidates) break;
                string t = s.course.tags.Count > 0 ? s.course.tags[0] : "";
                if (usedTags.Add(t)) result.suggestions.Add(s);
            }
            foreach (var s in scored)
            {
                if (result.suggestions.Count >= MaxCandidates) break;
                if (!result.suggestions.Contains(s)) result.suggestions.Add(s);
            }

            if (result.suggestions.Count == 0)
                result.emptyReason = me.learns.Count == 0
                    ? "「学びたいこと」がまだ登録されていません。プロフィールで1つ選ぶと、合う教材を探せます。"
                    : "今の「学びたいこと」に合う公開教材が見つかりませんでした。別のことを追加するか、「学ぶ」で一覧から選んでみてください。";
            return result;
        }

        static string Next(string tag, IList<Course> catalog, Course self)
        {
            if (!MiraiData.Related.TryGetValue(tag, out var rel)) return "";
            foreach (var r in rel)
                foreach (var c in catalog)
                    if (c != self && c.published && c.tags.Contains(r))
                        return $"このあとは「{r}」の教材（{c.title}）に進めます。";
            return "";
        }
    }
}
