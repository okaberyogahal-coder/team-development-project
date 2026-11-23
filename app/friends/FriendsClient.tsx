"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";
import { Logo } from "../components/Logo";

type FriendProfile = { id: string; nickname: string };
type FriendCandidate = FriendProfile & { connectionStatus: "none" | "friend" | "incoming" | "outgoing" };
type FriendData = {
  friends: FriendProfile[];
  incoming: Array<FriendProfile & { createdAt: string }>;
  outgoing: Array<FriendProfile & { createdAt: string }>;
  matches: FriendCandidate[];
};

export function FriendsClient({
  nickname,
  initialData,
  apiPath = "/api/friends",
  demoMode = false,
}: {
  nickname: string;
  initialData: FriendData;
  apiPath?: string;
  demoMode?: boolean;
}) {
  const [data, setData] = useState<FriendData>(initialData);
  const [searchText, setSearchText] = useState("");
  const [searchNickname, setSearchNickname] = useState("");
  const [loading, setLoading] = useState(false);
  const [mutating, setMutating] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const busy = loading || mutating;
  const minLength = demoMode ? 2 : 1;
  const maxLength = demoMode ? 40 : 20;

  async function loadData(query = "") {
    setLoading(true);
    setError("");
    try {
      const suffix = query ? `?nickname=${encodeURIComponent(query)}` : "";
      const response = await fetch(`${apiPath}${suffix}`, { cache: "no-store" });
      const result = await response.json() as FriendData & { message?: string };
      if (!response.ok) throw new Error(result.message || "フレンド情報を読み込めませんでした。");
      setData(result);
      setSearchNickname(query);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "フレンド情報を読み込めませんでした。");
    } finally {
      setLoading(false);
    }
  }

  async function search(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    const query = searchText.trim();
    if (query.length < minLength || query.length > maxLength) {
      setError(`ニックネームは${minLength}〜${maxLength}文字で入力してください。`);
      setNotice("");
      return;
    }
    setNotice("");
    await loadData(query);
  }

  async function mutate(method: "POST" | "PATCH" | "DELETE", body: object) {
    if (busy) return;
    setMutating(true);
    setError("");
    setNotice("");
    try {
      const response = await fetch(apiPath, {
        method,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      const result = await response.json() as { message?: string };
      if (!response.ok) throw new Error(result.message || "処理できませんでした。");
      setNotice(result.message || "更新しました。");
      await loadData(searchNickname);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "処理できませんでした。");
    } finally {
      setMutating(false);
    }
  }

  return (
    <main className="friends-page">
      <header className="friends-header">
        <Logo priority />
        <span className="friends-header-caption">世代をこえて、つながろう。</span>
        <Link className="friends-back-link" href={demoMode ? "/ai-navi" : "/account"}>{demoMode ? "← AIナビ体験へ" : "← マイページへ"}</Link>
      </header>
      <div className="friends-content">
        <div className="friends-heading">
          <div>
            <p className="friends-eyebrow">{demoMode ? "MOCK COMMUNITY" : "MY COMMUNITY"}</p>
            <h1>{nickname}さんのフレンド{demoMode && "（モック）"}</h1>
            <p>{demoMode ? "架空のテストアカウント同士で、申請や承認を試せます。" : "新しいつながりを見つけて、いっしょに楽しみましょう。"}</p>
          </div>
          <Link className="friends-world-link" href={demoMode ? "/ai-navi" : "/plaza"}>{demoMode ? "AIナビ体験へ" : "ワールドに入る"} <span aria-hidden="true">↗</span></Link>
        </div>

        {notice && <p className="friends-feedback friends-feedback-success" role="status">{notice}</p>}
        {error && <p className="friends-feedback friends-feedback-error" role="alert">{error}</p>}

        <section className="friends-search-card" aria-labelledby="friends-search-title">
          <div className="friends-section-heading">
            <span className="friends-section-number">01</span>
            <div><h2 id="friends-search-title">フレンドを探す</h2><p id="friends-search-hint">ニックネームを完全一致で検索できます。</p></div>
          </div>
          <form className="friends-search-form" onSubmit={search}>
            <label htmlFor="friend-nickname">相手のニックネーム</label>
            <div className="friends-search-controls">
              <input
                id="friend-nickname"
                name="nickname"
                value={searchText}
                onChange={event => setSearchText(event.target.value)}
                type="search"
                enterKeyHint="search"
                autoComplete="off"
                aria-describedby="friends-search-hint"
                placeholder="ニックネームを入力"
              />
              <button type="submit" disabled={busy}>検索する</button>
            </div>
          </form>
          {searchNickname && (
            <div className="friends-results" aria-live="polite">
              <h3>「{searchNickname}」の検索結果</h3>
              {data.matches.length === 0
                ? <p className="friends-empty">該当する利用者はいませんでした。ニックネームをご確認ください。</p>
                : <ul>{data.matches.map(candidate => (
                  <li className="friends-person" key={candidate.id}>
                    <span className="friends-avatar" aria-hidden="true">{candidate.nickname.slice(0, 1)}</span>
                    <strong>{candidate.nickname}</strong>
                    {candidate.connectionStatus === "none" && <button type="button" onClick={() => void mutate("POST", { targetId: candidate.id })} disabled={busy}>申請する</button>}
                    {candidate.connectionStatus === "friend" && <span className="friends-state">フレンド</span>}
                    {candidate.connectionStatus === "incoming" && <span className="friends-state">申請が届いています</span>}
                    {candidate.connectionStatus === "outgoing" && <span className="friends-state">申請中</span>}
                  </li>
                ))}</ul>}
            </div>
          )}
        </section>

        <div className="friends-lists">
          <section className="friends-list-card" aria-labelledby="incoming-title">
            <div className="friends-section-heading">
              <span className="friends-section-number friends-number-pink">02</span>
              <div><h2 id="incoming-title">届いている申請</h2><p>フレンドになりたい人からの申請です。</p></div>
              {data.incoming.length > 0 && <span className="friends-count">{data.incoming.length}</span>}
            </div>
            {loading ? <p className="friends-empty">読み込み中…</p> : data.incoming.length === 0
              ? <p className="friends-empty">届いている申請はありません。</p>
              : <ul>{data.incoming.map(person => (
                <li className="friends-person" key={person.id}>
                  <span className="friends-avatar friends-avatar-pink" aria-hidden="true">{person.nickname.slice(0, 1)}</span>
                  <strong>{person.nickname}</strong>
                  <div className="friends-person-actions">
                    <button type="button" className="friends-accept" onClick={() => void mutate("PATCH", { action: "accept", targetId: person.id })} disabled={busy}>承認</button>
                    <button type="button" className="friends-text-button" onClick={() => void mutate("PATCH", { action: "decline", targetId: person.id })} disabled={busy}>見送る</button>
                  </div>
                </li>
              ))}</ul>}
          </section>

          <section className="friends-list-card" aria-labelledby="outgoing-title">
            <div className="friends-section-heading">
              <span className="friends-section-number friends-number-green">03</span>
              <div><h2 id="outgoing-title">送った申請</h2><p>承認されるとフレンドになります。</p></div>
            </div>
            {loading ? <p className="friends-empty">読み込み中…</p> : data.outgoing.length === 0
              ? <p className="friends-empty">送っている申請はありません。</p>
              : <ul>{data.outgoing.map(person => (
                <li className="friends-person" key={person.id}>
                  <span className="friends-avatar friends-avatar-green" aria-hidden="true">{person.nickname.slice(0, 1)}</span>
                  <strong>{person.nickname}</strong>
                  <button type="button" className="friends-text-button" onClick={() => void mutate("DELETE", { targetId: person.id })} disabled={busy}>取り消す</button>
                </li>
              ))}</ul>}
          </section>

          <section className="friends-list-card friends-current-list" aria-labelledby="friends-list-title">
            <div className="friends-section-heading">
              <span className="friends-section-number">04</span>
              <div><h2 id="friends-list-title">フレンド</h2><p>つながっているフレンドです。</p></div>
              <span className="friends-count">{data.friends.length}</span>
            </div>
            {loading ? <p className="friends-empty">読み込み中…</p> : data.friends.length === 0
              ? <p className="friends-empty">まだフレンドはいません。ニックネームで探してみましょう。</p>
              : <ul>{data.friends.map(person => (
                <li className="friends-person" key={person.id}>
                  <span className="friends-avatar" aria-hidden="true">{person.nickname.slice(0, 1)}</span>
                  <strong>{person.nickname}</strong>
                  <button type="button" className="friends-text-button" onClick={() => {
                    if (window.confirm(`${person.nickname}さんとのフレンドを解除しますか？`)) {
                      void mutate("DELETE", { targetId: person.id });
                    }
                  }} disabled={busy}>フレンド解除</button>
                </li>
              ))}</ul>}
          </section>
        </div>
        <footer className="friends-footer"><span>ME LIFE</span> 小さな一歩が、未来を変える。</footer>
      </div>
    </main>
  );
}
