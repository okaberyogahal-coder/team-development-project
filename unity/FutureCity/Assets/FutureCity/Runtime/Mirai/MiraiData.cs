using System;
using System.Collections.Generic;
using UnityEngine;

namespace FutureCity
{
    /// <summary>資格（仕様 FR-324：自己申告・複数登録・任意項目）</summary>
    public class Qualification
    {
        public string name;
        public string issuer;     // 発行団体（任意）
        public string acquired;   // 取得年月（任意）
    }

    /// <summary>プロフィール（仕様 FR-110/314/324）。教えられること・学びたいこと・実年齢・資格。</summary>
    public class MiraiProfile
    {
        public string name;
        public int age;                     // 実年齢を表示（54歳なら54歳）
        public string bio;
        public List<string> teaches = new List<string>();
        public List<string> learns = new List<string>();
        public List<Qualification> quals = new List<Qualification>();
        public bool aiUsesAge = true;       // AI利用設定（年齢の表示とは別）
        public Color color = Color.white;

        // デモ人物の配置（キャンパス中心からの位置）
        public Vector2 spot;
        public float facing;
    }

    public enum CourseKind { Video, Document }

    /// <summary>教材（動画・資料）。予約なしでいつでも閲覧できる（仕様 FR-101/104）。</summary>
    public class Course
    {
        public string id, title, author, category, description;
        public int authorAge;
        public CourseKind kind;
        public int minutes;
        public List<string> tags = new List<string>();
        public bool published = true;
        public bool mine;
        public string fileState = "利用可能";   // 未送信 → 送信中 → 検証中 → 利用可能
        public string KindLabel => kind == CourseKind.Video ? "動画" : "資料";
    }

    /// <summary>予約制オンライン講座の開催回（仕様 FR-103/318、BR-19）</summary>
    public class LectureSession
    {
        public string id, title, teacher, description;
        public int teacherAge;
        public DateTime startsAt;
        public int minutes = 60;
        public int capacity = 7;
        public int booked;
        public bool myBooking;
        public bool canceled;
        public bool mine;                     // 自分が講師
        public string room = "学びの部屋";

        /// <summary>予約締切 = 開始30分前（共通固定・講師ごとの設定なし）</summary>
        public DateTime ClosesAt => startsAt.AddMinutes(-30);
        public int SeatsLeft => Mathf.Max(0, capacity - booked);

        /// <summary>新規予約できるのは now &lt; 締切 かつ 残席あり かつ 中止でない ときだけ。境界ちょうどは不可。</summary>
        public bool CanBook(DateTime now) => !canceled && !myBooking && !mine && now < ClosesAt && SeatsLeft > 0;

        public string Status(DateTime now)
        {
            if (canceled) return "中止";
            if (mine) return "あなたが講師";
            if (myBooking) return "予約済み";
            if (now >= ClosesAt) return "予約締切";
            if (SeatsLeft == 0) return "満席";
            return "予約できます";
        }
    }

    public static class MiraiData
    {
        public const int Year = 2127;

        public static readonly string[] Categories = { "趣味", "暮らしの知恵", "仕事", "デジタル" };

        public static readonly string[] SkillCatalog =
        {
            "料理", "園芸", "書道", "和菓子作り", "手話", "昔の遊び", "ダンス", "ギター",
            "スマホ写真", "生成AI", "プログラミング", "3Dデザイン", "英会話", "資産運用",
            "介護の知恵", "子育ての知恵", "営業経験", "宇宙農業"
        };

        /// <summary>「次に学ぶこと」の関連（未来ナビのモック用）</summary>
        public static readonly Dictionary<string, string[]> Related = new Dictionary<string, string[]>
        {
            { "生成AI", new[] { "プログラミング", "3Dデザイン", "スマホ写真" } },
            { "スマホ写真", new[] { "3Dデザイン", "生成AI" } },
            { "園芸", new[] { "宇宙農業", "和菓子作り" } },
            { "料理", new[] { "和菓子作り", "園芸" } },
            { "書道", new[] { "和菓子作り", "昔の遊び" } },
            { "和菓子作り", new[] { "料理", "書道" } },
            { "手話", new[] { "英会話", "介護の知恵" } },
            { "英会話", new[] { "手話", "生成AI" } },
            { "ダンス", new[] { "ギター", "手話" } },
            { "ギター", new[] { "ダンス" } },
            { "プログラミング", new[] { "生成AI", "3Dデザイン" } },
            { "3Dデザイン", new[] { "プログラミング", "スマホ写真" } },
            { "昔の遊び", new[] { "書道", "子育ての知恵" } },
            { "子育ての知恵", new[] { "昔の遊び", "料理" } },
            { "介護の知恵", new[] { "手話", "料理" } },
            { "資産運用", new[] { "営業経験", "生成AI" } },
            { "営業経験", new[] { "資産運用", "英会話" } },
            { "宇宙農業", new[] { "園芸", "料理" } },
        };

        public static string FeatureName(MiraiFeature f)
        {
            switch (f)
            {
                case MiraiFeature.Learn: return "学ぶ";
                case MiraiFeature.Teach: return "教える";
                case MiraiFeature.Connect: return "つながる";
                default: return "未来ナビ";
            }
        }

        public static string FeatureCaption(MiraiFeature f)
        {
            switch (f)
            {
                case MiraiFeature.Learn: return "教材を見る・講座を予約する";
                case MiraiFeature.Teach: return "教材を投稿する・講座をひらく";
                case MiraiFeature.Connect: return "4つの交流室で話す";
                default: return "次に学ぶ教材をAIが提案";
            }
        }

        public static Color FeatureColor(MiraiFeature f)
        {
            switch (f)
            {
                case MiraiFeature.Learn: return new Color(0.18f, 0.5f, 0.85f);
                case MiraiFeature.Teach: return new Color(0.88f, 0.45f, 0.08f);
                case MiraiFeature.Connect: return new Color(0.85f, 0.3f, 0.55f);
                default: return new Color(0.2f, 0.6f, 0.32f);
            }
        }

        /// <summary>あなた（利用者）の初期プロフィール。プロフィール画面で自由に変更できる。</summary>
        public static MiraiProfile Player()
        {
            return new MiraiProfile
            {
                name = "はるこ",
                age = 78,
                bio = "料理と庭いじりが好き。孫に写真をきれいに送りたい。",
                teaches = new List<string> { "料理", "園芸", "書道" },
                learns = new List<string> { "スマホ写真", "生成AI" },
                quals = new List<Qualification>
                {
                    new Qualification { name = "調理師", issuer = "", acquired = "1972年4月" },
                    new Qualification { name = "書道 師範", issuer = "地域の書道会", acquired = "1990年" }
                },
                color = new Color(0.98f, 0.82f, 0.6f)
            };
        }

        static MiraiProfile P(string name, int age, string bio, string[] t, string[] l, Qualification[] q,
                              float x, float z, float face, Color c)
        {
            return new MiraiProfile
            {
                name = name, age = age, bio = bio,
                teaches = new List<string>(t), learns = new List<string>(l), quals = new List<Qualification>(q),
                spot = new Vector2(x, z), facing = face, color = c
            };
        }

        static Qualification Q(string n, string iss = "", string when = "") => new Qualification { name = n, issuer = iss, acquired = when };
        static Color C(float r, float g, float b) => new Color(r, g, b);
        static readonly Qualification[] None = new Qualification[0];

        /// <summary>
        /// デモ人物（NPC）。実際の参加者ではないため、参加人数・部屋の人数には数えない（仕様 §9）。
        /// 位置はキャンパス中心からの座標（北が +Z）。
        /// </summary>
        public static List<MiraiProfile> Citizens()
        {
            return new List<MiraiProfile>
            {
                // ロビー（噴水のまわり）
                P("ハナ", 98, "園芸歴80年。写真をきれいに撮れるようになりたい。", new[] { "園芸", "和菓子作り" }, new[] { "スマホ写真", "生成AI" },
                  new[] { Q("園芸装飾技能士") }, 4f, -9.2f, 200f, C(0.55f, 0.78f, 0.5f)),
                P("ゲンジ", 112, "最年長の語り部。宇宙で畑をするのが夢。", new[] { "昔の遊び", "書道" }, new[] { "宇宙農業", "ダンス" },
                  None, -4.2f, -9.4f, 160f, C(0.78f, 0.72f, 0.6f)),
                P("サクラ", 9, "ダンスが大好き。ひいおじいちゃんの遊びに興味あり。", new[] { "ダンス" }, new[] { "昔の遊び", "英会話" },
                  None, -2.6f, -10.6f, 120f, C(1f, 0.72f, 0.4f)),
                P("トモコ", 81, "元保育士。手話サークルを60年続けている。", new[] { "手話", "子育ての知恵" }, new[] { "スマホ写真", "英会話" },
                  new[] { Q("保育士", "", "1966年"), Q("手話通訳士") }, 9.5f, 4f, 250f, C(0.95f, 0.62f, 0.5f)),

                // 藤棚・池・菜園・花畑
                P("マサエ", 90, "書道家。最近は3Dで作品を残したい。", new[] { "書道", "和菓子作り" }, new[] { "3Dデザイン" },
                  new[] { Q("書道 師範") }, -22f, -50f, 0f, C(0.7f, 0.45f, 0.6f)),
                P("ジロウ", 75, "郷土料理の店を50年。AIで新メニューを考えたい。", new[] { "料理", "資産運用" }, new[] { "生成AI" },
                  new[] { Q("調理師"), Q("ふぐ調理師") }, 24f, -46.5f, 180f, C(0.5f, 0.65f, 0.45f)),
                P("ハルキ", 52, "週末バンドのギタリスト。庭づくりに挑戦中。", new[] { "ギター", "3Dデザイン" }, new[] { "園芸" },
                  None, -21f, 47f, 0f, C(0.9f, 0.45f, 0.35f)),
                P("ミオ", 34, "写真家。ベランダで野菜を育てたい。", new[] { "スマホ写真", "3Dデザイン" }, new[] { "園芸", "子育ての知恵" },
                  new[] { Q("写真技能士 2級") }, 21f, 48f, 20f, C(1f, 0.55f, 0.65f)),

                // 交流室（デモ人物は部屋の人数に数えない）
                P("ユウト", 17, "高校生。祖父母世代にAIの使い方を教えるのが好き。", new[] { "生成AI", "プログラミング" }, new[] { "料理", "書道" },
                  new[] { Q("基本情報技術者") }, 50f, 56f, 180f, C(0.35f, 0.6f, 1f)),
                P("アヤ", 67, "介護職を引退し、3Dデザインを学び直し中。", new[] { "介護の知恵", "料理" }, new[] { "プログラミング", "3Dデザイン" },
                  new[] { Q("介護福祉士", "", "1998年") }, 46f, 50f, 0f, C(0.85f, 0.85f, 0.95f)),
                P("ノア", 14, "3Dモデリングが得意な中学生。", new[] { "3Dデザイン" }, new[] { "ギター", "料理" },
                  None, 54f, 50f, 0f, C(0.5f, 0.9f, 0.6f)),
                P("ケンタ", 45, "投資アドバイザー。娘と手話で話したい。", new[] { "資産運用", "営業経験" }, new[] { "手話", "ギター" },
                  new[] { Q("ファイナンシャル・プランナー 1級") }, 44.8f, -47.5f, 90f, C(0.45f, 0.5f, 0.7f)),
                P("リコ", 58, "元営業部長。体を動かすのが楽しみ。", new[] { "営業経験", "手話" }, new[] { "ダンス", "ギター" },
                  None, 49.2f, -47.5f, 270f, C(0.4f, 0.75f, 0.85f)),
                P("エマ", 29, "月面都市から移住。日本の文化を学びたい。", new[] { "英会話", "ダンス" }, new[] { "書道", "和菓子作り" },
                  new[] { Q("英語教員免許") }, -50f, 53.4f, 180f, C(0.65f, 0.5f, 1f)),
                P("レン", 23, "月面農場のエンジニア。", new[] { "宇宙農業", "英会話" }, new[] { "和菓子作り", "営業経験" },
                  None, -47f, -49f, 90f, C(0.35f, 0.85f, 0.75f)),
                P("カイ", 38, "エンジニア。親の介護に備えて知恵を学びたい。", new[] { "プログラミング", "宇宙農業" }, new[] { "介護の知恵" },
                  None, -2f, 26.5f, 180f, C(0.5f, 0.55f, 0.9f)),
            };
        }

        static Course Co(string id, string title, string author, int age, string cat, CourseKind k, int min, string desc, params string[] tags)
        {
            return new Course { id = id, title = title, author = author, authorAge = age, category = cat, kind = k, minutes = min, description = desc, tags = new List<string>(tags) };
        }

        /// <summary>公開教材（利用者が投稿した動画・資料のデモ）</summary>
        public static List<Course> Courses()
        {
            var V = CourseKind.Video;
            var D = CourseKind.Document;
            return new List<Course>
            {
                Co("c01", "スマホで花をきれいに撮る", "ミオ", 34, "趣味", V, 12, "逆光をいかすコツと、ピントの合わせ方。庭の花で練習します。", "スマホ写真"),
                Co("c02", "写真を家族に送ってみよう", "ミオ", 34, "デジタル", D, 8, "撮った写真を選んで、家族のグループに送るまでを大きな画面で説明します。", "スマホ写真"),
                Co("c03", "生成AIに はじめて話しかける", "ユウト", 17, "デジタル", V, 10, "質問のしかた、聞き直し方。間違えることもあるので確かめ方も学びます。", "生成AI"),
                Co("c04", "AIと一緒に、孫への手紙を書く", "ユウト", 17, "デジタル", D, 15, "伝えたい気持ちを箇条書きにして、AIに手伝ってもらう手順です。", "生成AI"),
                Co("c05", "季節の和菓子：桜もち", "ハナ", 98, "趣味", V, 18, "道明寺粉でつくる桜もち。家にある道具でできます。", "和菓子作り"),
                Co("c06", "ベランダ菜園の始め方", "ハナ", 98, "暮らしの知恵", D, 10, "プランターと土の選び方、水やりの目安。", "園芸"),
                Co("c07", "やさしい手話：あいさつ編", "トモコ", 81, "暮らしの知恵", V, 9, "おはよう・ありがとう・またね。ゆっくり何度でも見られます。", "手話"),
                Co("c08", "筆ペンで年賀状", "マサエ", 90, "趣味", V, 14, "筆ペン1本で書く、はね・はらいのコツ。", "書道"),
                Co("c09", "郷土料理：だご汁", "ジロウ", 75, "暮らしの知恵", V, 20, "だんごの練り方と、季節の野菜の切り方。", "料理"),
                Co("c10", "はじめての資産運用：考え方", "ケンタ", 45, "仕事", D, 12, "商品の紹介ではなく、考え方の基本を説明します。", "資産運用"),
                Co("c11", "いすに座ってできるダンス", "サクラ", 9, "趣味", V, 7, "立たなくても大丈夫。音楽に合わせて手と足を動かします。", "ダンス"),
                Co("c12", "昔の遊び：お手玉とけん玉", "ゲンジ", 112, "趣味", V, 11, "お手玉の作り方と、けん玉の最初の技。", "昔の遊び"),
                Co("c13", "コード3つで弾けるギター", "ハルキ", 52, "趣味", V, 16, "指が痛くなりにくい押さえ方から始めます。", "ギター"),
                Co("c14", "月面農場のしごと紹介", "レン", 23, "仕事", D, 9, "宇宙で野菜を育てる仕事の一日。", "宇宙農業"),
                Co("c15", "3Dで作品を残そう：入門", "ノア", 14, "デジタル", V, 15, "書や絵を3Dの作品にして、家族と見る方法。", "3Dデザイン"),
                Co("c16", "介護する家族のための体を守るコツ", "アヤ", 67, "暮らしの知恵", D, 10, "介助のときに腰を痛めない姿勢。", "介護の知恵"),
                Co("c17", "旅先のひとこと英会話", "エマ", 29, "暮らしの知恵", V, 9, "道をたずねる、お礼を言う。ゆっくり発音します。", "英会話"),
                Co("c18", "スマホ写真：人物をやさしく撮る", "ミオ", 34, "趣味", V, 11, "家族の自然な笑顔を撮るための声かけと光。", "スマホ写真"),
            };
        }

        /// <summary>自分の教材（最初は下書きが1件）</summary>
        public static Course MyDraft(MiraiProfile me)
        {
            return new Course
            {
                id = "m01", title = "家庭の味：肉じゃが", author = me.name, authorAge = me.age, category = "暮らしの知恵",
                kind = CourseKind.Video, minutes = 0, description = "60年つくってきた、甘すぎない肉じゃがの作り方。",
                tags = new List<string> { "料理" }, published = false, mine = true, fileState = "未送信"
            };
        }

        /// <summary>予約制オンライン講座の開催回（現在時刻を基準に作る）</summary>
        public static List<LectureSession> Sessions(DateTime now)
        {
            return new List<LectureSession>
            {
                new LectureSession { id = "s1", title = "生成AI 体験教室（少人数）", teacher = "ユウト", teacherAge = 17,
                    description = "はじめての人向け。AIに質問して、答えを確かめるまで。", startsAt = Round5(now.AddHours(2)), booked = 4, room = "学びの部屋" },
                // 体験中に締切を迎える回（開始の約36分後に始まる → 数分後に「予約締切」へ変わる）
                new LectureSession { id = "s6", title = "AIに質問してみる ミニ講座", teacher = "ユウト", teacherAge = 17,
                    description = "15分だけ。聞き方と、答えの確かめ方。", startsAt = Round5(now.AddMinutes(36)), minutes = 15, booked = 3, room = "学びの部屋" },
                new LectureSession { id = "s2", title = "スマホ写真 さんぽ講座", teacher = "ミオ", teacherAge = 34,
                    description = "庭を歩きながら花を撮ります。", startsAt = Round5(now.AddMinutes(18)), booked = 5, room = "趣味の部屋" },
                new LectureSession { id = "s3", title = "桜もちを一緒に作ろう", teacher = "ハナ", teacherAge = 98,
                    description = "材料は前日までに用意してください。", startsAt = At(now, 1, 14, 0), booked = 7, room = "交流カフェ" },
                new LectureSession { id = "s4", title = "やさしい手話 あいさつ", teacher = "トモコ", teacherAge = 81,
                    description = "ゆっくり・くり返し練習します。", startsAt = At(now, 1, 10, 0), booked = 2, room = "学びの部屋" },
                new LectureSession { id = "s5", title = "筆ペン講座：季節のたより", teacher = "マサエ", teacherAge = 90,
                    description = "はがき1枚を仕上げます。", startsAt = At(now, 2, 13, 30), booked = 3, room = "趣味の部屋" },
            };
        }

        static DateTime At(DateTime now, int addDays, int hour, int minute) => now.Date.AddDays(addDays).AddHours(hour).AddMinutes(minute);
        /// <summary>5分単位に切り上げ（開始時刻をきりのよい時刻にする）</summary>
        static DateTime Round5(DateTime t)
        {
            var r = new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute / 5 * 5, 0);
            return r < t ? r.AddMinutes(5) : r;
        }

        public static string FormatDate(DateTime t)
        {
            string[] wd = { "日", "月", "火", "水", "木", "金", "土" };
            return $"{t.Month}月{t.Day}日（{wd[(int)t.DayOfWeek]}）{t.Hour:00}:{t.Minute:00}";
        }
    }
}
