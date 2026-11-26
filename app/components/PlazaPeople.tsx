"use client";

import Link from 'next/link';
import {useCallback, useEffect, useId, useRef, useState} from 'react';
import {plazaHeartbeatMs, type PlazaPerson} from '../../shared/plaza-people';

export function PlazaPeople({ownerId}: {ownerId: string}) {
  const titleId = useId();
  const trigger = useRef<HTMLButtonElement>(null);
  const dialog = useRef<HTMLDialogElement>(null);
  const title = useRef<HTMLHeadingElement>(null);
  const presence = useRef<AbortController | null>(null);
  const request = useRef<AbortController | null>(null);
  const [people, setPeople] = useState<PlazaPerson[] | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [presenceError, setPresenceError] = useState('');
  const [requestMessage, setRequestMessage] = useState<{text: string; failed: boolean} | null>(null);
  const [sending, setSending] = useState(false);
  const selected = people?.find(person => person.id === selectedId);

  const refresh = useCallback(async () => {
    if (presence.current) return;
    const controller = new AbortController();
    presence.current = controller;
    try {
      const response = await fetch('/api/plaza/people', {
        method: 'POST',
        headers: {'X-Profile-Owner': ownerId},
        signal: AbortSignal.any([controller.signal, AbortSignal.timeout(10_000)]),
      });
      if (!response.ok) throw new Error();
      const data = await response.json() as {people: PlazaPerson[]};
      if (controller.signal.aborted) return;
      setPeople(data.people);
      setPresenceError('');
    } catch {
      if (!controller.signal.aborted) setPresenceError('広場にいる人を読み込めませんでした。');
    } finally {
      if (presence.current === controller) presence.current = null;
    }
  }, [ownerId]);

  useEffect(() => {
    const initial = window.setTimeout(() => void refresh(), 0);
    const timer = window.setInterval(() => void refresh(), plazaHeartbeatMs);
    return () => {
      window.clearTimeout(initial);
      window.clearInterval(timer);
      presence.current?.abort();
      presence.current = null;
      request.current?.abort();
      request.current = null;
    };
  }, [refresh]);

  useEffect(() => {
    if (dialog.current?.open) title.current?.focus();
  }, [selectedId]);

  function showPeople() {
    dialog.current?.showModal();
    title.current?.focus();
    void refresh();
  }

  function selectPerson(id: string | null) {
    setSelectedId(id);
    setRequestMessage(null);
  }

  async function sendRequest(person: PlazaPerson) {
    if (request.current || person.connectionStatus !== 'none') return;
    const controller = new AbortController();
    request.current = controller;
    setSending(true);
    setRequestMessage(null);
    try {
      const response = await fetch('/api/friends', {
        method: 'POST',
        headers: {'Content-Type': 'application/json', 'X-Profile-Owner': ownerId},
        body: JSON.stringify({targetId: person.id}),
        signal: AbortSignal.any([controller.signal, AbortSignal.timeout(10_000)]),
      });
      const result = await response.json() as {message?: string};
      if (controller.signal.aborted) return;
      if (response.ok) {
        setRequestMessage({text: result.message || 'フレンド申請を送りました。', failed: false});
        setPeople(previous => previous?.map(item => item.id === person.id
          ? {...item, connectionStatus: 'outgoing'} : item) ?? null);
      } else {
        setRequestMessage({text: result.message || '申請を送れませんでした。', failed: true});
      }
      presence.current?.abort();
      presence.current = null;
      void refresh();
    } catch {
      if (!controller.signal.aborted) setRequestMessage({text: '申請の結果を確認できませんでした。', failed: true});
    } finally {
      if (!controller.signal.aborted) {
        request.current = null;
        setSending(false);
      }
    }
  }

  return (
    <>
      <button ref={trigger} className="world-people-trigger" type="button" aria-haspopup="dialog" onClick={showPeople}>
        広場にいる人
      </button>
      <dialog ref={dialog} className="world-people-dialog" aria-labelledby={titleId} onClose={() => trigger.current?.focus()}>
        <header>
          <h2 id={titleId} ref={title} tabIndex={-1}>{selectedId ? 'プロフィール' : '広場にいる人'}</h2>
          <button type="button" onClick={() => dialog.current?.close()}>閉じる</button>
        </header>
        {presenceError && <p role="alert">{presenceError} <button type="button" onClick={refresh}>再読み込み</button></p>}
        {selectedId ? <>
          <button type="button" disabled={sending} onClick={() => selectPerson(null)}>一覧へ戻る</button>
          {selected ? <section aria-label={selected.nickname + 'さんのプロフィール'}>
            <h3>{selected.nickname}</h3>
            {selected.ageBadge && <p>{selected.ageBadge}</p>}
            <dl>
              <dt>教えられること</dt><dd>{selected.teaches.join('・') || '未設定'}</dd>
              <dt>学びたいこと</dt><dd>{selected.wantsToLearn.join('・') || '未設定'}</dd>
            </dl>
            {selected.connectionStatus === 'none' &&
              <button className="world-people-primary" type="button" disabled={sending || !!presenceError} onClick={() => sendRequest(selected)}>
                {sending ? '送信中…' : 'フレンド申請を送る'}
              </button>}
            {selected.connectionStatus === 'outgoing' && <p>申請済み</p>}
            {selected.connectionStatus === 'incoming' && <Link href="/friends">受け取った申請を確認する</Link>}
            {selected.connectionStatus === 'friend' && <p>フレンド</p>}
          </section> : <p role="status">この利用者は広場から退出しました。</p>}
          {requestMessage && <p role={requestMessage.failed ? 'alert' : 'status'}>{requestMessage.text}</p>}
        </> : <>
          {!people && !presenceError && <p role="status">読み込み中…</p>}
          {people?.length === 0 && !presenceError && <p>いまはほかの利用者がいません。</p>}
          {!!people?.length && <ul>
            {people.map(person => <li key={person.id}>
              <button type="button" onClick={() => selectPerson(person.id)}>{person.nickname}</button>
            </li>)}
          </ul>}
        </>}
      </dialog>
    </>
  );
}
