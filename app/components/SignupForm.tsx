"use client";
import { Icon } from "./Icon";
import type { IconName } from "../../shared/icons";
import { useRef, useState } from "react";
import { PasswordField } from "./PasswordField";
import { MIN_PASSWORD_LENGTH } from "../../shared/auth/validation";
import { notifyAuthChanged } from "../../shared/auth/events";

const steps = ["アカウント", "プロフィール", "学び・得意", "完了"];
const avatars: IconName[] = ["sprout", "cat", "bear", "rabbit", "penguin", "fox"];
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
        <input id={id} value={custom} onChange={event => onCustomChange(event.target.value)} maxLength={200} aria-describedby={`${id}-hint`} placeholder="ほかにもあれば、自由にどうぞ" />
        <span id={`${id}-hint`} className="sr-only">1項目30文字以内。複数ある場合は読点で区切ってください。選択項目と合わせて10個以内です。</span>
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
  const [avatar, setAvatar] = useState(avatars[0]);
  const [interests, setInterests] = useState<string[]>([]);
  const [learn, setLearn] = useState<string[]>([]);
  const [teach, setTeach] = useState<string[]>([]);
  const [interestText, setInterestText] = useState("");
  const [learnText, setLearnText] = useState("");
  const [teachText, setTeachText] = useState("");
  const [strength, setStrength] = useState("");
  const [goal, setGoal] = useState("");
  const [error, setError] = useState("");
  const [pending, setPending] = useState(false);
  const [signedIn, setSignedIn] = useState(false);
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
          <div className="auth-complete-avatar"><span aria-hidden="true"><Icon name={avatar} /></span><span className="auth-complete-star" aria-hidden="true"><Icon name="spark-pink" /></span></div>
          <p className="auth-complete-name">{nickname}さん、はじめの一歩、おめでとう！</p>
          <p>学びたいこと、教えたいこと。<br />ミライフ広場で、新しい出会いを見つけよう。</p>
          <div className="auth-demo-note" role="status"><strong>アカウントを作成しました。</strong><p>プロフィールと学びの設定を保存しました。</p></div>
          <a href={signedIn ? "/account" : "/login"} className="primary-button">{signedIn ? "マイページへ" : "ログインする"} <span aria-hidden="true">↗</span></a>
          <a href="/" className="auth-text-link">トップページへ戻る</a>
        </div>
      ) : (
        <>
          <p className="auth-demo-note">登録内容はこの開発環境のDBに保存されます。</p>
          <form className="signup-form" aria-busy={pending} aria-describedby={error ? "signup-error" : undefined} onSubmit={async event => {
            event.preventDefault();
            if (pending) return;
            if (step === 0 && password !== confirmation) { setError("パスワードが一致しません。もう一度確認してください。"); return; }
            if (step === 0 && (!adult || !terms || !privacy)) { setError("年齢確認と各同意を確認してください。"); return; }
            if (step === 1 && !nickname.trim()) { setError("ニックネームを入力してください。"); return; }
            if (step === 2) {
              setError("");
              setPending(true);
              const withCustom = (selected: string[], custom: string) => [...new Set([...selected, ...custom.split(/[,、]/).map(item => item.trim()).filter(Boolean)])];
              try {
                const response = await fetch("/api/account/signup", {
                  method: "POST", headers: { "Content-Type": "application/json" },
                  body: JSON.stringify({ email, password, confirmation, adult, terms, privacy, nickname, avatar,
                    interests: withCustom(interests, interestText), wantsToLearn: withCustom(learn, learnText), teaches: withCustom(teach, teachText), strengths: strength, goal }),
                });
                const data = await response.json();
                if (!response.ok) { setError(data.message || "登録できませんでした。"); return; }
                setSignedIn(data.signedIn === true);
                notifyAuthChanged();
                setPassword(""); setConfirmation("");
                move(3);
              } catch { setError("接続できませんでした。入力内容を残したまま、もう一度お試しください。"); }
              finally { setPending(false); }
              return;
            }
            move(step + 1);
          }}>
            <div className="signup-fields">
              {step === 0 && <div className="auth-account-fields">
                <div className="form-field"><label htmlFor="signup-email">メールアドレス <span className="auth-required">必須</span></label><input id="signup-email" aria-label="メールアドレス" type="email" autoComplete="email" required maxLength={254} value={email} onChange={event => setEmail(event.target.value)} placeholder="例：hello@example.com" /></div>
                <div className="auth-field-pair">
                  <PasswordField label="パスワード" id="signup-password" value={password} onChange={setPassword} minLength={MIN_PASSWORD_LENGTH} describedBy="signup-password-hint" />
                  <PasswordField label="パスワード（確認）" id="signup-confirmation" value={confirmation} onChange={setConfirmation} minLength={MIN_PASSWORD_LENGTH} describedBy="signup-password-hint" invalid={Boolean(error) && password !== confirmation} />
                </div>
                <p className="auth-field-hint" id="signup-password-hint">パスワードは12〜128文字で入力してください。</p>
                <fieldset className="auth-consents">
                  <legend className="sr-only">年齢確認と規約への同意</legend>
                  <label className="check-label"><input type="checkbox" required checked={adult} onChange={event => setAdult(event.target.checked)} /><span>18歳以上です</span><span className="auth-required">必須</span></label>
                  <div className="auth-consent-pair">
                    <label className="check-label"><input type="checkbox" required checked={terms} onChange={event => setTerms(event.target.checked)} /><span>開発版の利用条件を確認しました</span></label>
                    <label className="check-label"><input type="checkbox" required checked={privacy} onChange={event => setPrivacy(event.target.checked)} /><span>アカウント情報の保存に同意します</span></label>
                  </div>
                  <details className="auth-disclosure"><summary>開発版の利用条件・保存する情報</summary><p>現在は18歳以上の方による開発検証向けです。未成年の利用条件は検討中です。メール確認・パスワード再設定は準備中です。メール、パスワードのハッシュ、プロフィール、学びの設定、同意記録、ログインセッションを保存します。入力した目標や履歴を他の利用者に公開しません。公開運用前に正式な規約とプライバシーポリシーを整備します。</p></details>
                </fieldset>
              </div>}
              {step === 1 && <div className="auth-profile-fields">
                <div className="form-field"><label htmlFor="nickname">ニックネーム <span className="auth-required">必須</span></label><input id="nickname" aria-label="ニックネーム" required maxLength={20} value={nickname} onChange={event => setNickname(event.target.value)} placeholder="広場で呼ばれたい名前（20文字以内）" autoComplete="nickname" /></div>
                <fieldset className="auth-avatars"><legend>アバターを選ぶ <span>あなたのお気に入りは？</span></legend><div>{avatars.map((item, index) => <button key={item} type="button" aria-label={`アバター ${index + 1}`} aria-pressed={avatar === item} onClick={() => setAvatar(item)} className={`avatar-choice ${avatar === item ? "selected" : ""}`}><span aria-hidden="true"><Icon name={item} /></span>{avatar === item && <span className="avatar-check" aria-hidden="true"><Icon name="check-white" /></span>}</button>)}</div><p className="auth-field-hint">選択は保存され、ワールドのアイコンと人型アバターの服の色に反映されます。</p></fieldset>
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
              <div className="auth-error-slot">{error && <p id="signup-error" role="alert" className="form-alert">{error}</p>}</div>
              <div className="auth-actions">{step > 0 ? <button type="button" className="auth-back-button" disabled={pending} onClick={() => move(step - 1)}><span aria-hidden="true">←</span> 戻る</button> : <span className="auth-first-step-hint">あなたのペースで、ゆっくり。</span>}<button type="submit" className="primary-button" disabled={pending}>{pending ? "登録中…" : step === 2 ? "アカウントを作成" : "次へ"}<span aria-hidden="true">→</span></button></div>
              <p className="auth-switch-link">すでにアカウントをお持ちの方は <a href="/login">ログイン</a></p>
            </footer>
          </form>
        </>
      )}
    </div>
  );
}
