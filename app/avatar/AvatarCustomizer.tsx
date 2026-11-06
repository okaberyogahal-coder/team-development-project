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
import {readDemoProfile} from '../../shared/demo-profile';
import {isV3Appearance,v3Fingerprint} from '../../shared/avatar-v3';
import {readV3Draft,writeV3Draft,completeV3Avatar} from '../../shared/avatar-v3-storage';
import {previewAccepted} from '../../shared/avatar-v3-preview';
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
  ownerId, nickname,
}: {
  ownerId:string; nickname:string;
  genderState?: AvatarGenderState;
  assets?: AvatarAssetProfile;
  allCandidatesMode?: boolean;
}) {
  const candidates = allCandidatesMode ? getAllAvatarCandidates() : getAvatarCandidates(genderState);
  const initialCandidateId = candidates[0]?.id;
  const initialDefaultPending = useRef(assets.id !== 'legacy');
  const router = useRouter();
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const [activeTab, setActiveTab] = useState<'outerwear' | 'pants' | 'hair' | 'face' | 'shoes' | 'accessories' | 'glasses' | 'colors' | 'costume'>('outerwear');
  const [accessoryPage,setAccessoryPage]=useState(0);
  const [{choice,revision},setSelection] = useState(() => ({
    choice:{...DEFAULT_AVATAR_CHOICE,baseAvatarId:initialCandidateId || 'young-man',face:2},revision:0,
  }));
  const setChoice = (next:AvatarChoice|((previous:AvatarChoice)=>AvatarChoice)) => {
    setSelection(previous=>({choice:typeof next==='function'?next(previous.choice):next,revision:previous.revision+1}));
  };
  const [catalogReceived, setCatalogReceived] = useState(false);
  const [skinColorSupported, setSkinColorSupported] = useState(false);
  const [models, setModels] = useState<{ id: string; defaults: AvatarChoice }[]>([]);
  const [partCounts, setPartCounts] = useState<Record<string, number>>({ body: 1, face: 3, hair: 2, outerwear: 3, pants: 2, shoes: 3, glasses: 2, accessory: 5 });
  const [step, setStep] = useState<1 | 2>(1);
  const [notice, setNotice] = useState('');
  const [saveError, setSaveError] = useState(false);
  const [hydrated, setHydrated] = useState(false);
  const [hasPreviewBuild, setHasPreviewBuild] = useState<boolean | null>(null);

  const current = useRef({choice, revision:0});
  const frameEpoch=useRef(0);
  const lifecycle=useRef({active:false,generation:0,identityChanged:false});
  const [sessionChanged,setSessionChanged]=useState(false);
  useEffect(()=>{
    const state=lifecycle.current;state.active=true;state.generation++;
    const invalidate=()=>{state.generation++;};
    const authChanged=()=>{invalidate();state.identityChanged=true;setSessionChanged(true);setNotice('ログイン状態が変わりました。再度ログインしてください。');};
    let channel:BroadcastChannel|undefined;
    try{channel=new BroadcastChannel('melife-auth');channel.addEventListener('message',event=>{if(event.data==='changed')authChanged();});}catch{/* The final server check remains required. */}
    window.addEventListener('focus',invalidate);document.addEventListener('visibilitychange',invalidate);
    return()=>{state.active=false;invalidate();channel?.close();window.removeEventListener('focus',invalidate);document.removeEventListener('visibilitychange',invalidate);};
  },[]);
  const [ack, setAck] = useState<{revision:number; fingerprint:string} | null>(null);
  const [saving, setSaving] = useState(false);
  const confirmed = !sessionChanged && ack?.revision === revision && ack.fingerprint === v3Fingerprint(choice);

  useEffect(() => {
    const restore = window.setTimeout(() => {
      try {
        const completed = readDemoProfile(localStorage);
        const draft = readV3Draft(localStorage,ownerId);
        const previous = completed?.id === ownerId ? completed.appearance : null;
        const restored = draft ?? (isV3Appearance(previous) ? previous.choice : null);
        if(restored){initialDefaultPending.current=false;setChoice(restored);setStep(2);}
        else if(previous) setNotice('以前のアバターは保存されています。新しい外見は「このアバターで街へ行く」で確定します。');
      } catch { setSaveError(true); }
      setHydrated(true);
    },0);
    fetch(assets.previewSrc,{method:'HEAD'}).then(res=>setHasPreviewBuild(res.ok)).catch(()=>setHasPreviewBuild(false));
    return ()=>window.clearTimeout(restore);
  },[ownerId,assets.previewSrc]);

  useEffect(() => {
    current.current={choice,revision};
    if(!hydrated)return;
    let warningTimer:number|undefined;
    try{if(!lifecycle.current.identityChanged)writeV3Draft(localStorage,ownerId,choice);}catch{warningTimer=window.setTimeout(()=>setSaveError(true),0);}
    const send = () => iframeRef.current?.contentWindow?.postMessage({type:'miraifu:avatar',...current.current},window.location.origin);
    const receive = (event:MessageEvent) => {
      if(event.origin!==window.location.origin||event.source!==iframeRef.current?.contentWindow)return;
      if(event.data?.type==='miraifu:avatar-ready')send();
      if(event.data?.type==='miraifu:avatar-state'){
        if(previewAccepted(event.data,current.current.revision,current.current.choice))setAck({revision:current.current.revision,fingerprint:v3Fingerprint(current.current.choice)});
      }
      if(event.data?.type==='miraifu:avatar-catalog'){
        const catalog=readAvatarCatalog(event.data.catalog);if(!catalog)return;
        setCatalogReceived(true);setModels(catalog.models);setPartCounts(catalog.partCounts);setSkinColorSupported(catalog.skinColorSupported);
        if(initialDefaultPending.current){
          const first=catalog.models.find(model=>model.id===initialCandidateId)??catalog.models[0];
          if(first){initialDefaultPending.current=false;setChoice(first.defaults);}
        }
      }
    };
    window.addEventListener('message',receive);send();
    iframeRef.current?.contentWindow?.postMessage({type:'miraifu:catalog-request'},window.location.origin);
    return ()=>{window.removeEventListener('message',receive);window.clearTimeout(warningTimer);};
  },[choice,revision,hydrated,ownerId,initialCandidateId,hasPreviewBuild]);

  const updateChoice = (updates:Partial<AvatarChoice>) => {setAck(null);setChoice(prev=>({...prev,...updates}));};
  const changeStep = (next:1|2) => {setStep(next);requestAnimationFrame(()=>document.getElementById('avatar-step-title')?.focus());};
  const handleGoToCity = async () => {
    if(!confirmed||saving)return;
    const submitted={...current.current,epoch:frameEpoch.current,generation:lifecycle.current.generation};setSaving(true);
    try{
      const response=await fetch('/api/unity/session',{cache:'no-store'});
      const session=await response.json();
      if(!lifecycle.current.active)return;
      if(submitted.generation!==lifecycle.current.generation||lifecycle.current.identityChanged)throw new Error('ログイン状態が変わりました。再度ログインしてください。');
      if(!response.ok||!session.authenticated||session.profile?.userId!==ownerId)throw new Error('ログイン状態が変わりました。再度ログインしてください。');
      if(submitted.epoch!==frameEpoch.current||submitted.revision!==current.current.revision||v3Fingerprint(submitted.choice)!==v3Fingerprint(current.current.choice))throw new Error('変更後のプレビューを確認してから、もう一度お試しください。');
      completeV3Avatar(localStorage,ownerId,nickname,{version:3,assetProfile:'v3-unified-20261104',choice:submitted.choice});
      router.push('/plaza');
    }catch(error){if(!lifecycle.current.active)return;setNotice(error instanceof Error?error.message:'保存できませんでした。もう一度お試しください。');setSaving(false);}
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
              onLoad={() => {frameEpoch.current++;setAck(null);}}
              className="avatar-preview-iframe"
              title="3Dアバタープレビュー"
              src={`${assets.previewSrc}${assets.id !== 'legacy' && genderState.status === 'ready' ? `&gender=${genderState.gender}` : ''}`}
            />
          ) : (
            <div className="avatar-preview-fallback">
              <div style={{ fontSize: '48px', marginBottom: '12px' }}>🧍</div>
              <h3>3Dプレビュー</h3>
              <div className="avatar-preview-notice" role="status">
                {hasPreviewBuild === null ? '3Dプレビューを準備しています…' : '3Dプレビューを読み込めません。プレビューを確認できるまで確定できません。'}
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
        {saveError && <p className="avatar-message" role="status">このブラウザでは保存できません。保存設定を確認してください。以前のアバターは変更されていません。</p>}
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
              小物
            </button>
            <button type="button" className={`avatar-tab-btn ${activeTab === 'glasses' ? 'active' : ''}`} aria-pressed={activeTab === 'glasses'} onClick={() => setActiveTab('glasses')}>メガネ</button>
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

          <div className="avatar-category-content">
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

          {activeTab === 'glasses' && (
            <div>
              <div className="avatar-color-title">メガネ</div>
              <div className="avatar-options-grid">
                {AVATAR_OPTIONS.glasses.map(opt => (
                  <button type="button" key={opt.value} className={`avatar-option-card ${choice.glasses === opt.value ? 'selected' : ''}`} aria-pressed={choice.glasses === opt.value} disabled={opt.value >= (partCounts.glasses ?? 0)} onClick={() => updateChoice({glasses:opt.value})}>
                    <AvatarPartThumbnail headImageRoot={assets.headImageRoot} slot="glasses" value={opt.value} baseAvatarId={choice.baseAvatarId}/>
                    <div className="avatar-option-label">{opt.label}</div>
                  </button>
                ))}
              </div>
            </div>
          )}
          {activeTab === 'accessories' && (
            <div>
              <div className="avatar-color-title">小物</div>
              <div className="avatar-options-grid">
                {AVATAR_OPTIONS.accessory.slice(accessoryPage*3,accessoryPage*3+3).map(opt => (
                  <button type="button" key={opt.value} className={`avatar-option-card ${choice.accessory === opt.value ? 'selected' : ''}`} aria-pressed={choice.accessory === opt.value} disabled={opt.value >= (partCounts.accessory ?? 0)} onClick={() => updateChoice({accessory:opt.value})}>
                    <AvatarPartThumbnail headImageRoot={assets.headImageRoot} slot="accessory" value={opt.value} baseAvatarId={choice.baseAvatarId}/>
                    <div className="avatar-option-label">{opt.label}</div>
                  </button>
                ))}
              </div>
              <nav className="avatar-option-pages" aria-label="小物のページ">
                <button type="button" aria-label="前の小物" disabled={accessoryPage===0} onClick={()=>setAccessoryPage(page=>page-1)}>← 前へ</button>
                <span aria-live="polite">{accessoryPage+1} / {Math.ceil(AVATAR_OPTIONS.accessory.length/3)}</span>
                <button type="button" aria-label="次の小物" disabled={(accessoryPage+1)*3>=AVATAR_OPTIONS.accessory.length} onClick={()=>setAccessoryPage(page=>page+1)}>次へ →</button>
              </nav>
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

          </div>
          {/* アクションバー */}
          <div className="avatar-action-bar">
            <button
              type="button"
              className="avatar-go-btn"
              disabled={!hydrated || !confirmed || saving || unsupportedModel || unsupportedParts}
              onClick={handleGoToCity}
            >
              <span>{saving?'保存しています…':'このアバターで街へ行く'}</span>
              <span style={{ fontSize: '20px' }}>→</span>
            </button>
          </div>
        </>}
      </section>
    </div>
  );
}
