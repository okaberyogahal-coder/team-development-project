import type { Metadata } from "next";
import { redirect } from "next/navigation";
import Link from "next/link";
import { AuthShell } from "../components/AuthShell";
import { LogoutButton } from "../components/LogoutButton";
import { getCurrentUser } from "../../server/auth/server";

export const metadata: Metadata = { title: "マイページ | ミライフ ME LIFE", robots: { index: false, follow: false } };

export default async function AccountPage({searchParams}:{searchParams:Promise<{updated?:string}>}) {
  const {updated}=await searchParams;
  const user = await getCurrentUser();
  if (!user) redirect("/login");
  return <AuthShell><section className="auth-form-screen account-screen">
    <div className="auth-form-title"><p className="auth-title-kicker">あなたの「好き」の続きを、ここから。</p><h1 className="form-heading">こんにちは、{user.profile?.nickname || "利用者"}さん。</h1><p className="auth-form-description">ログインしています。</p></div>
    {updated==="1"&&<p role="status" className="profile-save-notice">プロフィールを保存しました。</p>}
    <dl className="account-details">
      <div><dt>メールアドレス</dt><dd>{user.email}</dd></div>
      <div><dt>興味のある分野</dt><dd>{user.preferences?.interests.join("、") || "未設定"}</dd></div>
      <div><dt>学びたいこと</dt><dd>{user.profile?.wantsToLearn.join("、") || "未設定"}</dd></div>
      <div><dt>教えられること</dt><dd>{user.profile?.teaches.join("、") || "未設定"}</dd></div>
      <div><dt>得意なこと（本人のみ）</dt><dd>{user.preferences?.strengths || "未設定"}</dd></div>
      <div><dt>目標（本人のみ）</dt><dd>{user.preferences?.goal || "未設定"}</dd></div>
    </dl>
    <p className="auth-demo-note">表示名・アバター・教えたいこと・学びたいことをワールドでも使えます。メール確認・教材機能は準備中です。</p>
    <div className="account-actions"><Link className="secondary-button" href="/account/edit">プロフィールを編集</Link><Link className="primary-button" href="/plaza">ワールドに入る</Link><Link className="secondary-button" href="/">トップページへ</Link><LogoutButton /></div>
  </section></AuthShell>;
}
