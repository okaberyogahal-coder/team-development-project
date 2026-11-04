'use client';

import { LEGACY_AVATAR_ASSETS, type AvatarAssetProfile } from '../../shared/avatar-development-assets';
import React, { useState, useEffect, useRef } from 'react';
import Image from 'next/image';
import { useRouter } from 'next/navigation';
import {
  AvatarChoice,
  DEFAULT_AVATAR_CHOICE,
  AVATAR_OPTIONS,
  COLOR_PALETTE,
  COSTUME_OPTIONS,
  SKIN_COLORS,
  restoreAvatarChoice,
  LEGACY_BASE_AVATAR_ID,
} from '../../shared/avatar-types';

import { getAvatarCandidates, getAllAvatarCandidates, type AvatarGenderState } from '../../shared/avatar-candidates';
import { avatarPartThumbnail } from '../../shared/avatar-thumbnails';
import { readAvatarCatalog } from '../../shared/avatar-catalog';

function AvatarPartThumbnail({ slot, value, baseAvatarId, headImageRoot }: { slot: string; value: number; baseAvatarId: string; headImageRoot?: string }) {
  const src = avatarPartThumbnail(slot, value, baseAvatarId, headImageRoot);
  return <><Image className={`avatar-part-image ${slot === 'costume' ? 'avatar-costume-image' : ''}`} src={src} alt="" width={slot === 'costume' ? 384 : 256} height={slot === 'costume' ? 512 : 256} loading="lazy" decoding="async" unoptimized /><span className="avatar-option-check" aria-hidden="true">✓</span></>;
}

function hairLabel(value: number, label: string, baseAvatarId: string) {
  if (value > 1) return label;
  if (baseAvatarId === 'young-woman') return ['ウェーブロング', 'すっきりロング'][value];
  if (baseAvatarId === 'middle-aged-woman') return ['ウェーブボブ', 'すっきりボブ'][value];
  return label;
}

export function AvatarCustomizer({
  genderState = { status: 'unconnected' },
  assets = LEGACY_AVATAR_ASSETS,
  allCandidatesMode = false,
}: {
  genderState?: AvatarGenderState;
  assets?: AvatarAssetProfile;
  allCandidatesMode?: boolean;
}) {
  const candidates = allCandidatesMode ? getAllAvatarCandidates() : getAvatarCandidates(genderState);
  const initialCandidateId = candidates[0]?.id;
  const initialDefaultPending = useRef(assets.id !== 'legacy');
  const router = useRouter();
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const [activeTab, setActiveTab] = useState<'outerwear' | 'pants' | 'hair' | 'face' | 'shoes' | 'accessories' | 'colors' | 'costume'>('outerwear');
  const [choice, setChoice] = useState<AvatarChoice>(() => {
    if (assets.id === 'v3-unified-20261104') {
      return {
        ...DEFAULT_AVATAR_CHOICE,
        baseAvatarId: initialCandidateId || DEFAULT_AVATAR_CHOICE.baseAvatarId,
        face: 2,
      };
    }
    return DEFAULT_AVATAR_CHOICE;
  });
  const [catalogReceived, setCatalogReceived] = useState(false);
  const [skinColorSupported, setSkinColorSupported] = useState(false);
  const [models, setModels] = useState<{ id: string; defaults: AvatarChoice }[]>([]);
  const [partCounts, setPartCounts] = useState<Record<string, number>>({ body: 1, face: 3, hair: 2, outerwear: 3, pants: 2, shoes: 3, glasses: 2, accessory: 5 });
  const [step, setStep] = useState<1 | 2>(1);
  const [notice, setNotice] = useState('');
  const [saveError, setSaveError] = useState(false);
  const [hydrated, setHydrated] = useState(false);
  const [hasPreviewBuild, setHasPreviewBuild] = useState<boolean | null>(null);

  // 初期化：localStorage に保存された設定があれば読み込み
  useEffect(() => {
    // Restore browser storage after hydration, before enabling automatic saves.
    const restore = window.setTimeout(() => {
      try {
        const saved = localStorage.getItem('miraifu_avatar');
        if (saved) {
          const restored = restoreAvatarChoice(JSON.parse(saved));
          if (restored) {
            const migratedLegacyModel = assets.id === 'v3-unified-20261104' &&
              restored.choice.baseAvatarId === LEGACY_BASE_AVATAR_ID;
            if (migratedLegacyModel) {
              restored.choice.baseAvatarId = initialCandidateId || 'young-man';
            }
            initialDefaultPending.current = false;
            setChoice(restored.choice);
            // Old saves represent an already-created avatar. New drafts stay
            // on selection until the user explicitly continues to editing.
            if (localStorage.getItem('miraifu_avatar_stage') !== 'select') setStep(2);
            if (restored.adjusted || migratedLegacyModel) setNotice('利用できないモデルやパーツを初期設定に戻しました。色など、使える設定は引き継いでいます。');
          }
        }
      } catch { /* Keep defaults if saved data is malformed. */ }
      setHydrated(true);
    }, 0);

    // プレビュー用 WebGL が存在するか確認
    fetch(assets.previewSrc, { method: 'HEAD' })
      .then((res) => {
        setHasPreviewBuild(res.ok);
      })
      .catch(() => {
        setHasPreviewBuild(false);
      });
    return () => window.clearTimeout(restore);
  }, [assets.id, assets.previewSrc, initialCandidateId]);

  // Keep the iframe mounted; its bridge queues changes until Unity is ready.
  useEffect(() => {
    if (!hydrated) return;
    let warningTimer: number | undefined;
    try {
      localStorage.setItem('miraifu_avatar_stage', step === 1 ? 'select' : 'edit');
      localStorage.setItem('miraifu_avatar', JSON.stringify(choice));
    } catch { warningTimer = window.setTimeout(() => setSaveError(true), 0); }
    const send = () => iframeRef.current?.contentWindow?.postMessage(
      { type: 'miraifu:avatar', choice }, window.location.origin,
    );
    const ready = (event: MessageEvent) => {
      if (event.origin === window.location.origin &&
        event.source === iframeRef.current?.contentWindow &&
        event.data?.type === 'miraifu:avatar-ready') {
        send();
        iframeRef.current?.contentWindow?.postMessage({ type: 'miraifu:catalog-request' }, window.location.origin);
      }
      if (event.origin === window.location.origin && event.source === iframeRef.current?.contentWindow && event.data?.type === 'miraifu:avatar-catalog') {
        const catalog = readAvatarCatalog(event.data.catalog);
        if (!catalog) return;
        setCatalogReceived(true);
        setModels(catalog.models);
        if (initialDefaultPending.current && assets.id !== 'legacy') {
          const first = catalog.models.find(model => model.id === initialCandidateId) ?? catalog.models[0];
          if (first) { setChoice(first.defaults); initialDefaultPending.current = false; }
        }
        setPartCounts(catalog.partCounts);
        setSkinColorSupported(catalog.skinColorSupported);
      }
    };
    window.addEventListener('message', ready);
    send();
    // The iframe may have finished before this effect subscribed. Ask for its
    // cached capabilities as well as listening for future readiness events.
    iframeRef.current?.contentWindow?.postMessage({ type: 'miraifu:catalog-request' }, window.location.origin);
    return () => {
      window.removeEventListener('message', ready);
      window.clearTimeout(warningTimer);
    };
  }, [choice, hydrated, step, assets.id, initialCandidateId]);

  const updateChoice = (updates: Partial<AvatarChoice>) => {
    setChoice((prev) => ({ ...prev, ...updates }));
  };

  const changeStep = (next: 1 | 2) => {
    setStep(next);
    try { localStorage.setItem('miraifu_avatar_stage', next === 1 ? 'select' : 'edit'); }
    catch { setSaveError(true); }
    requestAnimationFrame(() => document.getElementById('avatar-step-title')?.focus());
  };

  const handleGoToCity = () => {
    try {
      localStorage.setItem('miraifu_avatar', JSON.stringify(choice));
    } catch {
      setSaveError(true);
    }
    const avatarParam = encodeURIComponent(JSON.stringify(choice));
    const profileParam = assets.id === 'legacy' ? '' : `&profile=${encodeURIComponent(assets.id)}`;
    router.push(`/plaza?avatar=${avatarParam}${profileParam}`);
  };

  const unsupportedModel = hydrated && catalogReceived && (assets.id !== 'legacy' || choice.baseAvatarId !== LEGACY_BASE_AVATAR_ID) && !models.some(model => model.id === choice.baseAvatarId);
  const standardModel = models.find(model => model.id === initialCandidateId) ?? models[0];
  const standardModelId = assets.id === 'legacy' ? LEGACY_BASE_AVATAR_ID : standardModel?.id;
  const unsupportedParts = hydrated && catalogReceived && Object.entries(partCounts).some(([key, count]) => typeof choice[key as keyof AvatarChoice] === 'number' && Number(choice[key as keyof AvatarChoice]) >= count);
  const settingsNotice = unsupportedModel ? 'このアバターをプレビューに表示できません。設定は保持しています。'
    : unsupportedParts ? '一部のパーツをプレビューに表示できません。設定は保持しています。' : notice;

  // プリセット
  const applyPreset = (presetName: string) => {
    if (presetName === 'casual') {
      updateChoice({
        outerwear: 0,
        pants: 0,
        hair: 0,
        topColor: '#2563EB',
        pantsColor: '#627B99',
        hairColor: '#60412E',
        glasses: -1,
        accessory: -1,
      });
    } else if (presetName === 'chic') {
      updateChoice({
        outerwear: 2,
        pants: 1,
        hair: 1,
        topColor: '#374151',
        pantsColor: '#111827',
        hairColor: '#1C1917',
        glasses: 0,
        accessory: -1,
      });
    } else if (presetName === 'active') {
      updateChoice({
        outerwear: 1,
        pants: 0,
        hair: 0,
        topColor: '#EA580C',
        pantsColor: '#1E293B',
        hairColor: '#8B7355',
        glasses: -1,
        accessory: 1, // イヤホン
      });
    }
  };

  return (
    <div className="avatar-main-container" aria-busy={!hydrated}>
      <nav className="avatar-steps" aria-label="アバター作成のステップ">
        <span aria-current={step === 1 ? 'step' : undefined}>STEP 1 · 基本アバター</span>
        <span aria-hidden="true">→</span>
        <span aria-current={step === 2 ? 'step' : undefined}>STEP 2 · 着せ替え</span>
        <span aria-hidden="true">→</span>
        <span>STEP 3 · ワールドへ</span>
      </nav>
      {/* 左側：3Dアバタープレビュー枠 */}
      <section className="avatar-preview-section">
        <div className="avatar-preview-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span className="avatar-preview-badge">3Dプレビュー</span>
          </div>
          <div className="avatar-presets" hidden={step === 1}>
            <button
              type="button"
              onClick={() => applyPreset('casual')}
              style={{ padding: '4px 8px', fontSize: '11px', borderRadius: '6px', border: '1px solid #cbd5e1', background: '#fff', cursor: 'pointer' }}
            >
              カジュアル
            </button>
            <button
              type="button"
              onClick={() => applyPreset('chic')}
              style={{ padding: '4px 8px', fontSize: '11px', borderRadius: '6px', border: '1px solid #cbd5e1', background: '#fff', cursor: 'pointer' }}
            >
              シック
            </button>
            <button
              type="button"
              onClick={() => applyPreset('active')}
              style={{ padding: '4px 8px', fontSize: '11px', borderRadius: '6px', border: '1px solid #cbd5e1', background: '#fff', cursor: 'pointer' }}
            >
              スポーティ
            </button>
          </div>
        </div>

        <div className="avatar-preview-body">
          {hasPreviewBuild ? (
            <iframe
              ref={iframeRef}
              className="avatar-preview-iframe"
              title="3Dアバタープレビュー"
              src={`${assets.previewSrc}${assets.id !== 'legacy' && genderState.status === 'ready' ? `&gender=${genderState.gender}` : ''}`}
            />
          ) : (
            <div className="avatar-preview-fallback">
              <div style={{ fontSize: '48px', marginBottom: '12px' }}>🧍</div>
              <h3>3Dプレビュー</h3>
              <div className="avatar-preview-notice" role="status">
                {hasPreviewBuild === null ? '3Dプレビューを準備しています…' : '3Dプレビューを読み込めません。着せ替えの設定はそのまま保存できます。'}
              </div>
            </div>
          )}
        </div>
        <p className="avatar-rotate-guide">ドラッグ・スワイプで360度回転できます</p>
      </section>

      {/* 右側：編集パネル */}
      <section className="avatar-editor-section" aria-labelledby="avatar-step-title">
        <div className="avatar-editor-title">
          <h2 id="avatar-step-title" tabIndex={-1}>{step === 1 ? 'あなたの分身を選ぼう！' : 'アバターの衣装とパーツ'}</h2>
          <p>{step === 1 ? '好きなタイプを選んで、着せ替えへ。' : 'パーツを選ぶと、3Dに反映されます。'}</p>
          {step === 2 && <button type="button" className="avatar-btn-secondary avatar-change-base" onClick={() => changeStep(1)}>基本アバターを変更</button>}
        </div>
        {settingsNotice && <p className="avatar-message" role="status">{settingsNotice}</p>}
        {saveError && <p className="avatar-message" role="status">このブラウザでは保存できません。「街へ行く」ボタンから現在の設定を引き継げます。</p>}
        {!hydrated ? <p role="status">保存したアバターを確認しています…</p> : step === 1 ? <>
          {process.env.NODE_ENV === 'development' && genderState.status === 'ready' && genderState.source === 'development-fixture' && (
            <details className="avatar-dev-note">
              <summary>開発用の表示情報</summary>
              <p>開発用の仮データ：{genderState.gender === 'female' ? '女性' : '男性'}の候補を表示中です。</p>
            </details>
          )}
          {!allCandidatesMode && genderState.status !== 'ready' && <p className="avatar-inline-hint" role="status">{genderState.status === 'loading' ? 'アバター候補を確認しています…' : genderState.status === 'error' ? '候補を読み込めませんでした。標準アバターで続けられます。' : '候補は準備中です。標準アバターで続けられます。'}</p>}
          {!allCandidatesMode && genderState.status === 'ready' && catalogReceived && !candidates.some(base => models.some(model => model.id === base.id)) && <p className="avatar-inline-hint" role="status">候補は準備中です。標準アバターで続けられます。</p>}
          <div className="avatar-base-grid" aria-label="基本アバター" aria-busy={genderState.status === 'loading'}>
            {candidates.map(base => {
              const model = models.find(model => model.id === base.id);
              const selected = choice.baseAvatarId === base.id;
              const cardLabel = base.ageLabel.includes('男') || base.ageLabel.includes('女')
                ? base.ageLabel
                : (base.id.includes('man') && !base.id.includes('woman')
                    ? `${base.ageLabel}男性`
                    : `${base.ageLabel}女性`);
              return <button key={base.id} type="button" className={`avatar-base-card ${selected ? 'selected' : ''}`} disabled={!model} aria-pressed={selected}
                aria-label={`${cardLabel}（${base.label}）${model ? '' : '：準備中'}`} onClick={() => {
                  if (!model || choice.baseAvatarId === base.id) return;
                  const restored = restoreAvatarChoice(model.defaults);
                  if (restored) {
                    if (assets.id === 'v3-unified-20261104') {
                      restored.choice.face = 2; // 初期表情をおだやか(2)にする
                    }
                    setChoice(restored.choice);
                    setNotice('');
                  }
                }}>
                <span className="avatar-thumbnail">
                  <Image
                    src={`${assets.baseImageRoot}/${base.id}.${assets.id === 'v3-unified-20261104' ? 'png' : 'webp'}`}
                    alt={`${base.label}の全身像`}
                    width={384}
                    height={512}
                    unoptimized
                  />
                  {selected && <span className="avatar-thumbnail-selected" aria-hidden="true">✓ 選択中</span>}
                </span>
                <strong>{cardLabel}</strong>{!model && <span className="avatar-card-status">準備中</span>}
              </button>;
            })}
          </div>
          {assets.id === 'legacy' && (
            <button type="button" className="avatar-legacy-choice" disabled={!standardModel} aria-pressed={choice.baseAvatarId === standardModelId} onClick={() => {
              updateChoice({ baseAvatarId: LEGACY_BASE_AVATAR_ID });
            }}>
              標準アバターを使う{choice.baseAvatarId === standardModelId && <span aria-hidden="true"> ✓</span>}
            </button>
          )}
          <div className="avatar-action-bar">
            <button type="button" className="avatar-go-btn" onClick={() => changeStep(2)}>このアバターで編集へ →</button>
          </div>
        </> : <>
          {/* カテゴリタブ */}
          <div className="avatar-tabs" aria-label="着せ替えのカテゴリ">
            <button
              type="button"
              className={`avatar-tab-btn ${activeTab === 'outerwear' ? 'active' : ''}`}
              aria-pressed={activeTab === 'outerwear'}
              onClick={() => setActiveTab('outerwear')}
            >
              上着・服
            </button>
            <button
              type="button"
              className={`avatar-tab-btn ${activeTab === 'pants' ? 'active' : ''}`}
              aria-pressed={activeTab === 'pants'}
              onClick={() => setActiveTab('pants')}
            >
              ズボン
            </button>
            <button
              type="button"
              className={`avatar-tab-btn ${activeTab === 'hair' ? 'active' : ''}`}
              aria-pressed={activeTab === 'hair'}
              onClick={() => setActiveTab('hair')}
            >
              髪型
            </button>
            <button
              type="button"
              className={`avatar-tab-btn ${activeTab === 'face' ? 'active' : ''}`}
              aria-pressed={activeTab === 'face'}
              onClick={() => setActiveTab('face')}
            >
              表情
            </button>
            <button
              type="button"
              className={`avatar-tab-btn ${activeTab === 'shoes' ? 'active' : ''}`}
              aria-pressed={activeTab === 'shoes'}
              onClick={() => setActiveTab('shoes')}
            >
              靴
            </button>
            <button
              type="button"
              className={`avatar-tab-btn ${activeTab === 'accessories' ? 'active' : ''}`}
              aria-pressed={activeTab === 'accessories'}
              onClick={() => setActiveTab('accessories')}
            >
              小物・メガネ
            </button>
            <button
              type="button"
              className={`avatar-tab-btn ${activeTab === 'colors' ? 'active' : ''}`}
              aria-pressed={activeTab === 'colors'}
              onClick={() => setActiveTab('colors')}
            >
              色の調整
            </button>
            <button type="button" className={`avatar-tab-btn ${activeTab === 'costume' ? 'active' : ''}`} aria-pressed={activeTab === 'costume'} onClick={() => setActiveTab('costume')}>コスチューム</button>
          </div>

          {(choice.costume >= 0 || activeTab === 'costume') && <p className="avatar-inline-hint" role="status">{choice.costume >= 0 ? '着用中は一部のパーツが隠れます。「通常の服」で戻せます。' : '衣装を選んでも、元の着せ替えは残ります。'}</p>}
          {activeTab === 'costume' && <div>
            <div className="avatar-options-grid">
              {COSTUME_OPTIONS.map(option => (
                <button type="button" key={option.value}
                  className={`avatar-option-card ${choice.costume === option.value ? 'selected' : ''}`}
                  aria-pressed={choice.costume === option.value}
                  disabled={option.value >= (partCounts.costume ?? 0)}
                  onClick={() => updateChoice({ costume: option.value })}>
                  <AvatarPartThumbnail headImageRoot={assets.headImageRoot} slot="costume" value={option.value} baseAvatarId={choice.baseAvatarId} />
                  <div className="avatar-option-label">{option.label}</div>
                </button>
              ))}
            </div>
          </div>}
          {/* タブに応じたコンテンツ */}
          {activeTab === 'outerwear' && (
            <div>
              <div className="avatar-options-grid">
                {AVATAR_OPTIONS.outerwear.map((opt) => (
                  <button
                    type="button"
                    key={opt.value}
                    className={`avatar-option-card ${choice.outerwear === opt.value ? 'selected' : ''}`}
                    aria-pressed={choice.outerwear === opt.value}
                    disabled={opt.value >= (partCounts.outerwear ?? 0)}
                    title={opt.value >= (partCounts.outerwear ?? 0) ? 'プレビューの更新後に利用できます' : undefined}
                    onClick={() => updateChoice({ outerwear: opt.value })}
                  >
                    <AvatarPartThumbnail headImageRoot={assets.headImageRoot} slot="outerwear" value={opt.value} baseAvatarId={choice.baseAvatarId} />
                    <div className="avatar-option-label">{opt.label}</div>
                  </button>
                ))}
              </div>

              <div className="avatar-color-group">
                <div className="avatar-color-title">上着の色</div>
                <div className="avatar-palette-grid">
                  {COLOR_PALETTE.tops.map((c) => (
                    <button
                      type="button"
                      key={c.hex}
                      className={`avatar-color-dot ${choice.topColor.toLowerCase() === c.hex.toLowerCase() ? 'selected' : ''}`}
                      style={{ background: c.hex }}
                      title={c.label}
                      aria-label={c.label}
                      aria-pressed={choice.topColor.toLowerCase() === c.hex.toLowerCase()}
                      onClick={() => updateChoice({ topColor: c.hex })}
                    />
                  ))}
                </div>
              </div>
            </div>
          )}

          {activeTab === 'pants' && (
            <div>
              <div className="avatar-options-grid">
                {AVATAR_OPTIONS.pants.map((opt) => (
                  <button
                    type="button"
                    key={opt.value}
                    className={`avatar-option-card ${choice.pants === opt.value ? 'selected' : ''}`}
                    aria-pressed={choice.pants === opt.value}
                    disabled={opt.value >= (partCounts.pants ?? 0)}
                    title={opt.value >= (partCounts.pants ?? 0) ? 'プレビューの更新後に利用できます' : undefined}
                    onClick={() => updateChoice({ pants: opt.value })}
                  >
                    <AvatarPartThumbnail headImageRoot={assets.headImageRoot} slot="pants" value={opt.value} baseAvatarId={choice.baseAvatarId} />
                    <div className="avatar-option-label">{opt.label}</div>
                  </button>
                ))}
              </div>

              <div className="avatar-color-group">
                <div className="avatar-color-title">ズボンの色</div>
                <div className="avatar-palette-grid">
                  {COLOR_PALETTE.pants.map((c) => (
                    <button
                      type="button"
                      key={c.hex}
                      className={`avatar-color-dot ${choice.pantsColor.toLowerCase() === c.hex.toLowerCase() ? 'selected' : ''}`}
                      style={{ background: c.hex }}
                      title={c.label}
                      aria-label={c.label}
                      aria-pressed={choice.pantsColor.toLowerCase() === c.hex.toLowerCase()}
                      onClick={() => updateChoice({ pantsColor: c.hex })}
                    />
                  ))}
                </div>
              </div>
            </div>
          )}

          {activeTab === 'hair' && (
            <div>
              <div className="avatar-options-grid">
                {AVATAR_OPTIONS.hair.map((opt) => (
                  <button
                    type="button"
                    key={opt.value}
                    className={`avatar-option-card ${choice.hair === opt.value ? 'selected' : ''}`}
                    aria-pressed={choice.hair === opt.value}
                    disabled={opt.value >= (partCounts.hair ?? 0)}
                    title={opt.value >= (partCounts.hair ?? 0) ? 'プレビューの更新後に利用できます' : undefined}
                    onClick={() => updateChoice({ hair: opt.value })}
                  >
                    <AvatarPartThumbnail headImageRoot={assets.headImageRoot} slot="hair" value={opt.value} baseAvatarId={choice.baseAvatarId} />
                    <div className="avatar-option-label">{hairLabel(opt.value, opt.label, choice.baseAvatarId)}</div>
                  </button>
                ))}
              </div>

              <div className="avatar-color-group">
                <div className="avatar-color-title">髪色</div>
                <div className="avatar-palette-grid">
                  {COLOR_PALETTE.hair.map((c) => (
                    <button
                      type="button"
                      key={c.hex}
                      className={`avatar-color-dot ${choice.hairColor.toLowerCase() === c.hex.toLowerCase() ? 'selected' : ''}`}
                      style={{ background: c.hex }}
                      title={c.label}
                      aria-label={c.label}
                      aria-pressed={choice.hairColor.toLowerCase() === c.hex.toLowerCase()}
                      onClick={() => updateChoice({ hairColor: c.hex })}
                    />
                  ))}
                </div>
              </div>
            </div>
          )}

          {activeTab === 'face' && (
            <div className="avatar-options-grid">
              {AVATAR_OPTIONS.face.map((opt) => (
                <button
                  type="button"
                  key={opt.value}
                  className={`avatar-option-card ${choice.face === opt.value ? 'selected' : ''}`}
                  aria-pressed={choice.face === opt.value}
                  disabled={opt.value >= (partCounts.face ?? 0)}
                  title={opt.value >= (partCounts.face ?? 0) ? 'プレビューの更新後に利用できます' : undefined}
                  onClick={() => updateChoice({ face: opt.value })}
                >
                  <AvatarPartThumbnail headImageRoot={assets.headImageRoot} slot="face" value={opt.value} baseAvatarId={choice.baseAvatarId} />
                  <div className="avatar-option-label">
                    {opt.value === 0 ? '怒り (表情0)' : opt.value === 1 ? '笑顔 (表情1)' : opt.value === 2 ? 'おだやか (表情2)' : opt.label}
                  </div>
                </button>
              ))}
            </div>
          )}

          {activeTab === 'shoes' && (
            <div className="avatar-options-grid">
              {AVATAR_OPTIONS.shoes.map((opt) => (
                <button
                  type="button"
                  key={opt.value}
                  className={`avatar-option-card ${choice.shoes === opt.value ? 'selected' : ''}`}
                  aria-pressed={choice.shoes === opt.value}
                  disabled={opt.value >= (partCounts.shoes ?? 0)}
                  title={opt.value >= (partCounts.shoes ?? 0) ? 'プレビューの更新後に利用できます' : undefined}
                  onClick={() => updateChoice({ shoes: opt.value })}
                >
                  <AvatarPartThumbnail headImageRoot={assets.headImageRoot} slot="shoes" value={opt.value} baseAvatarId={choice.baseAvatarId} />
                  <div className="avatar-option-label">{opt.label}</div>
                </button>
              ))}
            </div>
          )}

          {activeTab === 'accessories' && (
            <div>
              <div className="avatar-color-title">メガネ</div>
              <div className="avatar-options-grid">
                {AVATAR_OPTIONS.glasses.map((opt) => (
                  <button
                    type="button"
                    key={opt.value}
                    className={`avatar-option-card ${choice.glasses === opt.value ? 'selected' : ''}`}
                    aria-pressed={choice.glasses === opt.value}
                    disabled={opt.value >= (partCounts.glasses ?? 0)}
                    title={opt.value >= (partCounts.glasses ?? 0) ? 'プレビューの更新後に利用できます' : undefined}
                    onClick={() => updateChoice({ glasses: opt.value })}
                  >
                    <AvatarPartThumbnail headImageRoot={assets.headImageRoot} slot="glasses" value={opt.value} baseAvatarId={choice.baseAvatarId} />
                    <div className="avatar-option-label">{opt.label}</div>
                  </button>
                ))}
              </div>

              <div className="avatar-color-title" style={{ marginTop: '16px' }}>アクセサリー / 小物</div>
              <div className="avatar-options-grid">
                {AVATAR_OPTIONS.accessory.map((opt) => (
                  <button
                    type="button"
                    key={opt.value}
                    className={`avatar-option-card ${choice.accessory === opt.value ? 'selected' : ''}`}
                    aria-pressed={choice.accessory === opt.value}
                    disabled={opt.value >= (partCounts.accessory ?? 0)}
                    title={opt.value >= (partCounts.accessory ?? 0) ? 'プレビューの更新後に利用できます' : undefined}
                    onClick={() => updateChoice({ accessory: opt.value })}
                  >
                    <AvatarPartThumbnail headImageRoot={assets.headImageRoot} slot="accessory" value={opt.value} baseAvatarId={choice.baseAvatarId} />
                    <div className="avatar-option-label">{opt.label}</div>
                  </button>
                ))}
              </div>
            </div>
          )}

          {activeTab === 'colors' && (
            <div className="avatar-color-settings">
              <div className="avatar-color-group">
                <div className="avatar-color-title">肌色：{SKIN_COLORS.find(color => color.value.toLowerCase() === choice.skinColor.toLowerCase())?.label.replace('肌色・', '') ?? 'カスタム'}</div>
                {catalogReceived && !skinColorSupported && (
                  <p role="status">このプレビューでは肌色を変更できません。</p>
                )}
                <div className="avatar-palette-grid">
                  {SKIN_COLORS.map(color => (
                    <button type="button" key={color.value}
                      className={`avatar-color-dot ${choice.skinColor.toLowerCase() === color.value.toLowerCase() ? 'selected' : ''}`}
                      style={{ background: color.swatch }} title={color.label} aria-label={color.label}
                      aria-pressed={choice.skinColor.toLowerCase() === color.value.toLowerCase()}
                      disabled={!skinColorSupported}
                      onClick={() => updateChoice({ skinColor: color.value })} />
                  ))}
                </div>
              </div>
              <div className="avatar-color-group">
                <div className="avatar-color-title">上着の色</div>
                <div className="avatar-palette-grid">
                  {COLOR_PALETTE.tops.map((c) => (
                    <button
                      type="button"
                      key={c.hex}
                      className={`avatar-color-dot ${choice.topColor.toLowerCase() === c.hex.toLowerCase() ? 'selected' : ''}`}
                      style={{ background: c.hex }}
                      title={c.label}
                      aria-label={c.label}
                      aria-pressed={choice.topColor.toLowerCase() === c.hex.toLowerCase()}
                      onClick={() => updateChoice({ topColor: c.hex })}
                    />
                  ))}
                </div>
              </div>

              <div className="avatar-color-group">
                <div className="avatar-color-title">ズボンの色</div>
                <div className="avatar-palette-grid">
                  {COLOR_PALETTE.pants.map((c) => (
                    <button
                      type="button"
                      key={c.hex}
                      className={`avatar-color-dot ${choice.pantsColor.toLowerCase() === c.hex.toLowerCase() ? 'selected' : ''}`}
                      style={{ background: c.hex }}
                      title={c.label}
                      aria-label={c.label}
                      aria-pressed={choice.pantsColor.toLowerCase() === c.hex.toLowerCase()}
                      onClick={() => updateChoice({ pantsColor: c.hex })}
                    />
                  ))}
                </div>
              </div>

              <div className="avatar-color-group">
                <div className="avatar-color-title">髪色</div>
                <div className="avatar-palette-grid">
                  {COLOR_PALETTE.hair.map((c) => (
                    <button
                      type="button"
                      key={c.hex}
                      className={`avatar-color-dot ${choice.hairColor.toLowerCase() === c.hex.toLowerCase() ? 'selected' : ''}`}
                      style={{ background: c.hex }}
                      title={c.label}
                      aria-label={c.label}
                      aria-pressed={choice.hairColor.toLowerCase() === c.hex.toLowerCase()}
                      onClick={() => updateChoice({ hairColor: c.hex })}
                    />
                  ))}
                </div>
              </div>
            </div>
          )}

          {/* アクションバー */}
          <div className="avatar-action-bar">
            <button
              type="button"
              className="avatar-go-btn"
              disabled={!hydrated}
              onClick={handleGoToCity}
            >
              <span>このアバターで街へ行く</span>
              <span style={{ fontSize: '20px' }}>→</span>
            </button>
          </div>
        </>}
      </section>
    </div>
  );
}
