'use client';

import React, { useState, useEffect, useRef } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  AvatarChoice,
  DEFAULT_AVATAR_CHOICE,
  AVATAR_OPTIONS,
  COLOR_PALETTE,
} from '../../shared/avatar-types';

export function AvatarCustomizer() {
  const router = useRouter();
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const [activeTab, setActiveTab] = useState<'outerwear' | 'pants' | 'hair' | 'face' | 'shoes' | 'accessories' | 'colors'>('outerwear');
  const [choice, setChoice] = useState<AvatarChoice>(DEFAULT_AVATAR_CHOICE);
  const [hydrated, setHydrated] = useState(false);
  const [hasPreviewBuild, setHasPreviewBuild] = useState<boolean | null>(null);

  // 初期化：localStorage に保存された設定があれば読み込み
  useEffect(() => {
    // Restore browser storage after hydration, before enabling automatic saves.
    const restore = window.setTimeout(() => {
      try {
        const saved = localStorage.getItem('miraifu_avatar');
        if (saved) {
          const parsed = JSON.parse(saved);
          if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
            const restored = { ...DEFAULT_AVATAR_CHOICE };
            for (const key of ['body', 'face', 'hair', 'outerwear', 'pants', 'shoes', 'glasses', 'accessory'] as const) {
              if (Number.isInteger(parsed[key])) restored[key] = parsed[key];
            }
            for (const key of ['hairColor', 'topColor', 'pantsColor'] as const) {
              if (typeof parsed[key] === 'string' && /^#[0-9a-f]{6}$/i.test(parsed[key])) restored[key] = parsed[key];
            }
            setChoice(restored);
          }
        }
      } catch { /* Keep defaults if saved data is malformed. */ }
      setHydrated(true);
    }, 0);

    // プレビュー用 WebGL が存在するか確認
    fetch('/unity/AvatarPreview/index.html', { method: 'HEAD' })
      .then((res) => {
        setHasPreviewBuild(res.ok);
      })
      .catch(() => {
        setHasPreviewBuild(false);
      });
    return () => window.clearTimeout(restore);
  }, []);

  // Keep the iframe mounted; its bridge queues changes until Unity is ready.
  useEffect(() => {
    if (!hydrated) return;
    try {
      localStorage.setItem('miraifu_avatar', JSON.stringify(choice));
    } catch { /* URL transfer still works if storage is unavailable. */ }
    const send = () => iframeRef.current?.contentWindow?.postMessage(
      { type: 'miraifu:avatar', choice }, window.location.origin,
    );
    const ready = (event: MessageEvent) => {
      if (event.origin === window.location.origin &&
          event.source === iframeRef.current?.contentWindow &&
          event.data?.type === 'miraifu:avatar-ready') send();
    };
    window.addEventListener('message', ready);
    send();
    return () => window.removeEventListener('message', ready);
  }, [choice, hydrated]);

  const updateChoice = (updates: Partial<AvatarChoice>) => {
    setChoice((prev) => ({ ...prev, ...updates }));
  };

  const handleGoToCity = () => {
    try {
      localStorage.setItem('miraifu_avatar', JSON.stringify(choice));
    } catch {
      // ignore
    }
    const avatarParam = encodeURIComponent(JSON.stringify(choice));
    router.push(`/plaza?avatar=${avatarParam}`);
  };

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
        accessory: 2, // ヘッドホン
      });
    }
  };

  return (
    <div className="avatar-main-container">
      {/* 左側：3Dアバタープレビュー枠 */}
      <section className="avatar-preview-section">
        <div className="avatar-preview-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span className="avatar-preview-badge">3Dプレビュー</span>
            <span style={{ fontSize: '13px', color: '#475569', fontWeight: 600 }}>リアルタイム表示</span>
          </div>
          <div style={{ display: 'flex', gap: '6px' }}>
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
              src="/unity/AvatarPreview/index.html"
            />
          ) : (
            <div className="avatar-preview-fallback">
              <div style={{ fontSize: '48px', marginBottom: '12px' }}>🧍</div>
              <h3>アバター編集プレビュー</h3>
              <p>右側のパネルで、服や髪型、お好みの色を自由に組み合わせてみましょう。</p>
              <div
                style={{
                  margin: '16px auto',
                  padding: '12px',
                  borderRadius: '12px',
                  background: 'rgba(255,255,255,0.8)',
                  display: 'inline-flex',
                  gap: '8px',
                  alignItems: 'center',
                }}
              >
                <span style={{ width: '18px', height: '18px', borderRadius: '50%', background: choice.topColor, display: 'inline-block' }} />
                <span style={{ fontSize: '12px', fontWeight: 700 }}>上着: {choice.topColor}</span>
                <span style={{ width: '18px', height: '18px', borderRadius: '50%', background: choice.pantsColor, display: 'inline-block' }} />
                <span style={{ fontSize: '12px', fontWeight: 700 }}>ズボン: {choice.pantsColor}</span>
              </div>
              <div className="avatar-preview-notice">
                💡 <strong>Unity上でリアルタイム3Dプレビューをビルドする場合:</strong>
                <br />
                Unity Editor のメニュー <code>Tools &gt; Miraifu &gt; 4. Build Creative 3D Preview (WebGL)</code> を実行すると、ここにUnityの3Dキャラクターが表示されます。
              </div>
            </div>
          )}
        </div>
      </section>

      {/* 右側：編集パネル */}
      <section className="avatar-editor-section">
        <div className="avatar-editor-title">
          <h2>アバターの衣装とパーツ</h2>
          <p>あなたらしいスタイルを見つけて、ミライフの街へ出かけましょう。</p>
        </div>

        {/* カテゴリタブ */}
        <div className="avatar-tabs">
          <button
            type="button"
            className={`avatar-tab-btn ${activeTab === 'outerwear' ? 'active' : ''}`}
            onClick={() => setActiveTab('outerwear')}
          >
            上着・服
          </button>
          <button
            type="button"
            className={`avatar-tab-btn ${activeTab === 'pants' ? 'active' : ''}`}
            onClick={() => setActiveTab('pants')}
          >
            ズボン
          </button>
          <button
            type="button"
            className={`avatar-tab-btn ${activeTab === 'hair' ? 'active' : ''}`}
            onClick={() => setActiveTab('hair')}
          >
            髪型
          </button>
          <button
            type="button"
            className={`avatar-tab-btn ${activeTab === 'face' ? 'active' : ''}`}
            onClick={() => setActiveTab('face')}
          >
            表情
          </button>
          <button
            type="button"
            className={`avatar-tab-btn ${activeTab === 'shoes' ? 'active' : ''}`}
            onClick={() => setActiveTab('shoes')}
          >
            靴
          </button>
          <button
            type="button"
            className={`avatar-tab-btn ${activeTab === 'accessories' ? 'active' : ''}`}
            onClick={() => setActiveTab('accessories')}
          >
            小物・メガネ
          </button>
          <button
            type="button"
            className={`avatar-tab-btn ${activeTab === 'colors' ? 'active' : ''}`}
            onClick={() => setActiveTab('colors')}
          >
            色の調整
          </button>
        </div>

        {/* タブに応じたコンテンツ */}
        {activeTab === 'outerwear' && (
          <div>
            <div className="avatar-options-grid">
              {AVATAR_OPTIONS.outerwear.map((opt) => (
                <button
                  type="button"
                  key={opt.value}
                  className={`avatar-option-card ${choice.outerwear === opt.value ? 'selected' : ''}`}
                  onClick={() => updateChoice({ outerwear: opt.value })}
                >
                  <div className="avatar-option-label">{opt.label}</div>
                </button>
              ))}
            </div>

            <div className="avatar-color-group">
              <div className="avatar-color-title">上着のカラー</div>
              <div className="avatar-palette-grid">
                {COLOR_PALETTE.tops.map((c) => (
                  <button
                    type="button"
                    key={c.hex}
                    className={`avatar-color-dot ${choice.topColor.toLowerCase() === c.hex.toLowerCase() ? 'selected' : ''}`}
                    style={{ background: c.hex }}
                    title={c.label}
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
                  onClick={() => updateChoice({ pants: opt.value })}
                >
                  <div className="avatar-option-label">{opt.label}</div>
                </button>
              ))}
            </div>

            <div className="avatar-color-group">
              <div className="avatar-color-title">ズボンのカラー</div>
              <div className="avatar-palette-grid">
                {COLOR_PALETTE.pants.map((c) => (
                  <button
                    type="button"
                    key={c.hex}
                    className={`avatar-color-dot ${choice.pantsColor.toLowerCase() === c.hex.toLowerCase() ? 'selected' : ''}`}
                    style={{ background: c.hex }}
                    title={c.label}
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
                  onClick={() => updateChoice({ hair: opt.value })}
                >
                  <div className="avatar-option-label">{opt.label}</div>
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
                onClick={() => updateChoice({ face: opt.value })}
              >
                <div className="avatar-option-label">{opt.label}</div>
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
                onClick={() => updateChoice({ shoes: opt.value })}
              >
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
                  onClick={() => updateChoice({ glasses: opt.value })}
                >
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
                  onClick={() => updateChoice({ accessory: opt.value })}
                >
                  <div className="avatar-option-label">{opt.label}</div>
                </button>
              ))}
            </div>
          </div>
        )}

        {activeTab === 'colors' && (
          <div>
            <div className="avatar-color-group">
              <div className="avatar-color-title">上着のカラー</div>
              <div className="avatar-palette-grid">
                {COLOR_PALETTE.tops.map((c) => (
                  <button
                    type="button"
                    key={c.hex}
                    className={`avatar-color-dot ${choice.topColor.toLowerCase() === c.hex.toLowerCase() ? 'selected' : ''}`}
                    style={{ background: c.hex }}
                    title={c.label}
                    onClick={() => updateChoice({ topColor: c.hex })}
                  />
                ))}
              </div>
            </div>

            <div className="avatar-color-group">
              <div className="avatar-color-title">ズボンのカラー</div>
              <div className="avatar-palette-grid">
                {COLOR_PALETTE.pants.map((c) => (
                  <button
                    type="button"
                    key={c.hex}
                    className={`avatar-color-dot ${choice.pantsColor.toLowerCase() === c.hex.toLowerCase() ? 'selected' : ''}`}
                    style={{ background: c.hex }}
                    title={c.label}
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
      </section>
    </div>
  );
}

