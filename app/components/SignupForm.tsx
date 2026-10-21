"use client";
import { Icon } from "./Icon";
import type { IconName } from "../../shared/icons";
import { createDemoProfile, saveDemoProfile } from "../../shared/demo-profile";
import { useRef, useState } from "react";
import { PasswordField } from "./PasswordField";

const steps = ["アカウント", "プロフィール", "学び・得意", "完了"];
const topics = ["料理", "園芸", "写真", "AI・デジタル", "語学", "仕事・人生経験", "音楽", "ものづくり"];
const topicIcons: IconName[] = ["cooking", "leaves", "camera", "laptop", "globe", "briefcase", "music", "palette"];

type TopicPickerProps = {
  label: string;
  selected: string[];
  onChange: (values: string[]) => void;
  custom: string;
  onCustomChange: (value: string) => void;
  id: string;
};

function TopicPicker({ label, selected, onChange, custom, onCustomChange, id }: TopicPickerProps) {
  return (
    <fieldset className={`auth-topics auth-topics-${id}`}>
      <legend>{label}<span>複数選択OK・任意</span></legend>
      <div className="auth-topic-options">
        {topics.map((topic, index) => (
          <button type="button" key={topic} aria-pressed={selected.includes(topic)} className={`topic-chip ${selected.includes(topic) ? "selected" : ""}`} onClick={() => onChange(selected.includes(topic) ? selected.filter(value => value !== topic) : [...selected, topic])}>
            <span aria-hidden="true"><Icon name={topicIcons[index]} /></span>{topic}
          </button>
        ))}
      </div>
      <div className="form-field auth-custom-topic">
        <label htmlFor={id}>{label}を自由入力</label>
        <input id={id} value={custom} onChange={event => onCustomChange(event.target.value)} maxLength={200} placeholder="ほかにもあれば、自由にどうぞ" />
      </div>
    </fieldset>
  );
}

export function SignupForm() {
  const [step, setStep] = useState(0);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [adult, setAdult] = useState(false);
  const [terms, setTerms] = useState(false);
  const [privacy, setPrivacy] = useState(false);
  const [nickname, setNickname] = useState("");
  const [interests, setInterests] = useState<string[]>([]);
  const [learn, setLearn] = useState<string[]>([]);
  const [teach, setTeach] = useState<string[]>([]);
  const [interestText, setInterestText] = useState("");
  const [learnText, setLearnText] = useState("");
  const [teachText, setTeachText] = useState("");
  const [strength, setStrength] = useState("");
  const [goal, setGoal] = useState("");
  const [error, setError] = useState("");
  const heading = useRef<HTMLHeadingElement>(null);

  function move(next: number) {
    setError("");
    setStep(next);
    requestAnimationFrame(() => {
      heading.current?.focus({ preventScroll: true });
      heading.current?.closest(".auth-panel")?.querySelector(".signup-fields")?.scrollTo({ top: 0, behavior: "instant" });
      if (!window.matchMedia("(min-width: 1024px)").matches) {
        heading.current?.scrollIntoView({ block: "start", behavior: "instant" });
      }
    });
  }

  return (
    <div className={`auth-form-screen signup-screen signup-step-${step + 1}`}>
      <ol aria-label="登録の進み具合" className="auth-steps">
        {steps.map((label, index) => (
          <li key={label} aria-current={index === step ? "step" : undefined} className={`auth-step ${index < step ? "complete" : ""} ${index === step ? "current" : ""}`}>
            <span>{index < step ? <Icon name="check" /> : index + 1}</span><p>{label}</p>
          </li>
        ))}
      </ol>
      <div className="auth-form-title">
        <div className="auth-title-kicker"><span className="auth-mini-star" aria-hidden="true"><Icon name="spark" /></span> はじめよう、あたらしい毎日 <span className="auth-step-count">{step + 1} / 4</span></div>
        <h1 ref={heading} tabIndex={-1} className="form-heading">{["アカウントを作成", "あなたらしさを、教えてね。", "好きなことから、つながろう。", "ミライフへようこそ！"][step]}</h1>
        {step < 3 && <p className="auth-form-description">{["新しい学びも、出会いも。まずはここから。", "広場で呼ばれたい名前と、お気に入りの姿を選ぼう。", "学ぶのも、教えるのも。今のあなたの「好き」でOK！"][step]}</p>}
      </div>
      {step === 3 ? (
        <div className="auth-complete">
          <div className="auth-complete-avatar"><span aria-hidden="true"><Icon name="sprout" /></span><span className="auth-complete-star" aria-hidden="true"><Icon name="spark-pink" /></span></div>
          <p className="auth-complete-name">{nickname}さん、はじめの一歩、おめでとう！</p>
          <p>学びたいこと、教えたいこと。<br />ミライフ広場で、新しい出会いを見つけよう。</p>
          <div className="auth-demo-note"><strong>体験プロフィールを作成しました。</strong><p>次は3Dアバターを作りましょう。</p><p>名前と外見はこのブラウザだけに保存されます。実際のアカウント登録・認証は行っていません。</p></div>
          <a href="/avatar" className="primary-button">3Dアバターを作成する <span aria-hidden="true">↗</span></a>
          <a href="/" className="auth-text-link">トップページへ戻る</a>
        </div>
      ) : (
        <>
          <p className="auth-demo-note"><strong>画面体験版</strong> 登録から入場までの体験用です。名前と外見だけをこのブラウザに保存します。メール・パスワードは送信・保存しません。</p>
          <form className="signup-form" onSubmit={event => {
            event.preventDefault();
            if (step === 0 && password !== confirmation) { setError("パスワードが一致しません。もう一度確認してください。"); return; }
            if (step === 0 && (!adult || !terms || !privacy)) { setError("年齢確認と各同意を確認してください。"); return; }
            if (step === 1) {
              try { setNickname(createDemoProfile(nickname, 'validation').nickname); }
              catch { setError("ニックネームを入力してください。< と > のみの名前は使えません。"); return; }
            }
            if (step === 2) {
              try { saveDemoProfile(window.localStorage, createDemoProfile(nickname, crypto.randomUUID())); }
              catch { setError("このブラウザに保存できません。ブラウザの保存設定を確認してください。"); return; }
              setPassword(""); setConfirmation(""); setEmail("");
            }
            move(step + 1);
          }}>
            <div className="signup-fields">
              {step === 0 && <div className="auth-account-fields">
                <div className="form-field"><label htmlFor="signup-email">メールアドレス <span className="auth-required">必須</span></label><input id="signup-email" aria-label="メールアドレス" type="email" autoComplete="email" required maxLength={254} value={email} onChange={event => setEmail(event.target.value)} placeholder="例：hello@example.com" /></div>
                <div className="auth-field-pair">
                  <PasswordField label="パスワード" id="signup-password" value={password} onChange={setPassword} minLength={8} />
                  <PasswordField label="パスワード（確認）" id="signup-confirmation" value={confirmation} onChange={setConfirmation} minLength={8} />
                </div>
                <p className="auth-field-hint">パスワードは8文字以上で入力してください。</p>
                <fieldset className="auth-consents">
                  <legend className="sr-only">年齢確認と規約への同意</legend>
                  <label className="check-label"><input type="checkbox" required checked={adult} onChange={event => setAdult(event.target.checked)} /><span>18歳以上です</span><span className="auth-required">必須</span></label>
                  <div className="auth-consent-pair">
                    <label className="check-label"><input type="checkbox" required checked={terms} onChange={event => setTerms(event.target.checked)} /><span>利用規約に同意します<span className="auth-consent-caption">（体験用）</span></span></label>
                    <label className="check-label"><input type="checkbox" required checked={privacy} onChange={event => setPrivacy(event.target.checked)} /><span>プライバシーポリシーに同意します<span className="auth-consent-caption">（体験用）</span></span></label>
                  </div>
                  <details className="auth-disclosure"><summary>利用規約・プライバシーポリシーについて</summary><p>正式な利用規約・プライバシーポリシーは準備中です。チェックは操作体験用です。実際のアカウント認証は行わず、体験プロフィールをこのブラウザに保存します。正式提供前に正式な内容を掲載します。</p></details>
                </fieldset>
              </div>}
              {step === 1 && <div className="auth-profile-fields">
                <div className="form-field"><label htmlFor="nickname">ニックネーム <span className="auth-required">必須</span></label><input id="nickname" aria-label="ニックネーム" required maxLength={20} value={nickname} onChange={event => setNickname(event.target.value)} placeholder="広場で呼ばれたい名前（20文字以内）" autoComplete="nickname" /></div>
                <div className="auth-demo-note"><strong>次に、3Dアバターを作成します。</strong><p>Unity Worldと同じキャラクターで、服・表情・髪型・小物・コスチュームを選べます。作成した姿のまま広場へ入れます。</p></div>
                <TopicPicker id="interests" label="興味のある分野" selected={interests} onChange={setInterests} custom={interestText} onCustomChange={setInterestText} />
              </div>}
              {step === 2 && <div className="auth-learning-fields">
                <div className="auth-topic-pair">
                  <TopicPicker id="learn" label="学びたいこと" selected={learn} onChange={setLearn} custom={learnText} onCustomChange={setLearnText} />
                  <TopicPicker id="teach" label="教えられること" selected={teach} onChange={setTeach} custom={teachText} onCustomChange={setTeachText} />
                </div>
                <div className="auth-field-pair">
                  <div className="form-field"><label htmlFor="strength">得意なこと <span className="auth-optional">任意</span></label><textarea id="strength" rows={2} maxLength={500} value={strength} onChange={event => setStrength(event.target.value)} placeholder="例：人の話を聞くこと" /></div>
                  <div className="form-field"><label htmlFor="goal">目標 <span className="auth-optional">任意</span></label><textarea id="goal" rows={2} maxLength={500} value={goal} onChange={event => setGoal(event.target.value)} placeholder="例：写真好きな仲間をつくりたい" /></div>
                </div>
              </div>}
            </div>
            <footer className="auth-form-footer">
              <div className="auth-error-slot">{error && <p role="alert" className="form-alert">{error}</p>}</div>
              <div className="auth-actions">{step > 0 ? <button type="button" className="auth-back-button" onClick={() => move(step - 1)}><span aria-hidden="true">←</span> 戻る</button> : <span className="auth-first-step-hint">あなたのペースで、ゆっくり。</span>}<button type="submit" className="primary-button">{step === 2 ? "入力を完了する" : "次へ"}<span aria-hidden="true">→</span></button></div>
              <p className="auth-switch-link">すでにアカウントをお持ちの方は <a href="/login">ログイン</a></p>
            </footer>
          </form>
        </>
      )}
    </div>
  );
}
