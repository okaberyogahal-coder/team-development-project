import { test, expect, type APIRequestContext, type Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { createDatabase } from '../../server/db/connection';
import { authTestDatabaseUrl } from '../support/auth-database';

const database = createDatabase(authTestDatabaseUrl());
const emails: string[] = [];
const password = 'browser-auth-test-password-123';
const origin = 'http://127.0.0.1:3012';

function registration() {
  const email = `${randomUUID()}@example.invalid`;
  emails.push(email);
  return { email, password, confirmation: password, adult: true, terms: true, privacy: true, nickname: '学び仲間', avatar: 'cat', interests: ['写真'], wantsToLearn: ['園芸'], teaches: ['料理'], strengths: '写真を撮ること', goal: '本人だけの目標' };
}

const post = (request: APIRequestContext, path: string, data: unknown, requestOrigin = origin) => request.post(path, { data, headers: { Origin: requestOrigin } });

async function fillSignup(page: Page, data: ReturnType<typeof registration>) {
  await page.goto('/signup');
  await page.getByLabel('メールアドレス', { exact: true }).fill(data.email.toUpperCase());
  await page.getByLabel('パスワード', { exact: true }).fill(data.password);
  await page.getByLabel('パスワード（確認）', { exact: true }).fill(data.confirmation);
  await page.getByLabel('18歳以上です').check();
  await page.getByLabel('開発版の利用条件を確認しました').check();
  await page.getByLabel('アカウント情報の保存に同意します').check();
  await page.getByRole('button', { name: '次へ', exact: true }).click();
  await page.getByLabel('ニックネーム', { exact: true }).fill(data.nickname);
  await page.getByRole('button', { name: 'アバター 2', exact: true }).click();
  await page.locator('.auth-topics-interests').getByRole('button', { name: '写真', exact: true }).click();
  await page.locator('#interests').fill(data.interests.join('、'));
  await page.getByRole('button', { name: '次へ', exact: true }).click();
  await page.locator('.auth-topics-learn').getByRole('button', { name: '園芸', exact: true }).click();
  await page.locator('#learn').fill(data.wantsToLearn.join('、'));
  await page.locator('.auth-topics-teach').getByRole('button', { name: '料理', exact: true }).click();
  await page.locator('#teach').fill(data.teaches.join('、'));
  await page.locator('#strength').fill(data.strengths);
  await page.locator('#goal').fill(data.goal);
}

async function assertAccount(page: Page, data: ReturnType<typeof registration>) {
  await expect(page.getByRole('heading', { name: `こんにちは、${data.nickname}さん。` })).toBeVisible();
  for (const value of [data.email, data.interests.join('、'), data.wantsToLearn.join('、'), data.teaches.join('、'), data.goal]) {
    await expect(page.locator('.account-details dd').filter({ hasText: value })).toHaveCount(1);
  }
}

test.afterAll(async () => {
  try { await database.user.deleteMany({ where: { email: { in: emails } } }); }
  finally { await database.$disconnect(); }
});

test('未ログインでマイページを開いても個人情報は表示しない', async ({ page }) => {
  await page.goto('/account');
  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole('heading', { name: 'おかえりなさい。' })).toBeVisible();
});

test('UnityにはCookie本人の表示用プロフィールだけを返し、ゲストとログアウトを区別する', async ({ request }) => {
  const guest = await request.get(`/api/unity/session?userId=${randomUUID()}`);
  expect(guest.status()).toBe(200);
  expect(await guest.json()).toEqual({ authenticated: false, profile: null });
  expect(guest.headers()['cache-control']).toBe('no-store');
  expect(guest.headers().vary).toContain('Cookie');
  expect(guest.headers()['cross-origin-resource-policy']).toBe('same-origin');
  const data = registration();
  expect((await post(request, '/api/account/signup', data)).ok()).toBe(true);
  const user = await database.user.findUniqueOrThrow({ where: { email: data.email } });
  const response = await request.get(`/api/unity/session?userId=${randomUUID()}`);
  expect(response.headers()['cache-control']).toBe('no-store');
  expect(await response.json()).toEqual({ authenticated: true, profile: {
    userId: user.id, nickname: data.nickname, avatarId: data.avatar,
    teaches: data.teaches, wantsToLearn: data.wantsToLearn,
  } });
  expect((await post(request, '/api/auth/sign-out', {})).ok()).toBe(true);
  expect(await (await request.get('/api/unity/session')).json()).toEqual({ authenticated: false, profile: null });
});

test('登録は実DBにプロフィール・ハッシュ・同意を保存し、ログインを維持する', async ({ page }) => {
  const data = registration();
  const response = await post(page.request, '/api/account/signup', data);
  expect(response.status()).toBe(200);
  expect(await response.json()).toEqual({ created: true, signedIn: true });
  await page.goto('/account');
  await expect(page.getByRole('heading', { name: 'こんにちは、学び仲間さん。' })).toBeVisible();
  await expect(page.getByText(data.email, { exact: true })).toBeVisible();
  await expect(page.getByText(data.goal, { exact: true })).toBeVisible();
  await page.screenshot({ path: 'test-results/account-desktop.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: 'test-results/account-mobile.png', fullPage: true });
  await page.reload();
  await expect(page.getByText(data.goal, { exact: true })).toBeVisible();
  const row = await database.user.findUniqueOrThrow({ where: { email: data.email }, include: { profile: true, preferences: true, consents: true, authAccounts: true } });
  expect(row.profile?.nickname).toBe(data.nickname);
  expect(row.preferences?.goal).toBe(data.goal);
  expect(row.consents).toHaveLength(3);
  expect(row.authAccounts[0].password).not.toBe(password);
  expect(row.status).toBe('PENDING');
  const session = await page.request.get('/api/auth/get-session');
  expect(await session.json()).toEqual({ authenticated: true });
  expect(session.headers()['cache-control']).toBe('no-store');
  await page.goto('/signup');
  await expect(page).toHaveURL(/\/account$/);
});

test('ログアウトでCookieが失効し、再ログインしてもプロフィールを保持する', async ({ page, context, playwright }) => {
  const data = registration();
  expect((await post(page.request, '/api/account/signup', data)).ok()).toBe(true);
  const cookies = await context.cookies();
  const token = cookies.find(cookie => cookie.name.includes('session_token'))!;
  expect(token.httpOnly).toBe(true);
  expect(token.sameSite).toBe('Lax');
  await page.goto('/account');
  await page.getByRole('button', { name: 'ログアウト', exact: true }).click();
  await expect(page).toHaveURL(/\/login$/);
  const stale = await playwright.request.newContext({ baseURL: origin, extraHTTPHeaders: { Cookie: `${token.name}=${token.value}` } });
  try { expect(await (await stale.get('/api/auth/get-session')).json()).toEqual({ authenticated: false }); }
  finally { await stale.dispose(); }
  await page.getByLabel('メールアドレス', { exact: true }).fill(data.email.toUpperCase());
  await page.getByLabel('パスワード', { exact: true }).fill('wrong-password-12345');
  await page.getByRole('button', { name: 'ログイン', exact: true }).click();
  await expect(page.getByRole('alert').filter({ hasText: 'メールアドレスまたはパスワード' })).toBeVisible();
  await page.getByLabel('パスワード', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'ログイン', exact: true }).click();
  await expect(page).toHaveURL(/\/avatar$/);
  await page.goto('/account');
  await expect(page.getByText(data.goal, { exact: true })).toBeVisible();
});

test('重複登録と別ルート経由の登録を拒否し、既存プロフィールを上書きしない', async ({ request }) => {
  const data = registration();
  expect((await post(request, '/api/account/signup', data)).ok()).toBe(true);
  const duplicate = await post(request, '/api/account/signup', { ...data, nickname: '上書き不可' });
  expect(duplicate.status()).toBe(400);
  expect(await database.user.count({ where: { email: data.email } })).toBe(1);
  const row = await database.user.findUniqueOrThrow({ where: { email: data.email }, include: { profile: true } });
  expect(row.profile?.nickname).toBe(data.nickname);
  expect((await post(request, '/api/auth/sign-up/email', { name: '別ルート', email: registration().email, password })).status()).toBe(404);
});

test('外部Originと必須確認のない登録はサーバーで拒否する', async ({ request }) => {
  const data = registration();
  expect((await post(request, '/api/account/signup', data, 'https://attacker.invalid')).status()).toBe(403);
  expect((await post(request, '/api/auth/sign-in/email', data, 'https://attacker.invalid')).status()).toBe(403);
  expect((await post(request, '/api/auth/sign-out', {}, 'https://attacker.invalid')).status()).toBe(403);
  for (const missing of [{ adult: false }, { terms: false }, { privacy: false }]) {
    expect((await post(request, '/api/account/signup', { ...data, ...missing })).status()).toBe(400);
  }
  expect(await database.user.count({ where: { email: data.email } })).toBe(0);
});

test('期限切れ・停止中のセッションでマイページを閲覧できない', async ({ page }) => {
  const data = registration();
  expect((await post(page.request, '/api/account/signup', data)).ok()).toBe(true);
  const user = await database.user.findUniqueOrThrow({ where: { email: data.email } });
  await database.authSession.updateMany({ where: { userId: user.id }, data: { expiresAt: new Date(Date.now() - 1000) } });
  expect(await (await page.request.get('/api/unity/session')).json()).toEqual({ authenticated: false, profile: null });
  await page.goto('/account');
  await expect(page).toHaveURL(/\/login$/);
  expect((await post(page.request, '/api/auth/sign-in/email', { email: data.email, password })).ok()).toBe(true);
  await database.user.update({ where: { id: user.id }, data: { status: 'SUSPENDED' } });
  expect(await (await page.request.get('/api/unity/session')).json()).toEqual({ authenticated: false, profile: null });
  await page.goto('/account');
  await expect(page).toHaveURL(/\/login$/);
  expect(await (await page.request.get('/api/auth/get-session')).json()).toEqual({ authenticated: false });
});

test('同じアドレスへのログイン試行に上限がある', async ({ request }) => {
  const email = registration().email;
  const statuses: number[] = [];
  for (let i = 0; i < 11; i++) statuses.push((await post(request, '/api/auth/sign-in/email', { email, password })).status());
  expect(statuses.slice(0, 10)).toEqual(Array(10).fill(401));
  expect(statuses[10]).toBe(429);
});

for (const viewport of [{ width: 1440, height: 1000 }, { width: 390, height: 844 }]) {
  test(`幅${viewport.width}で画面登録からログアウト・別ブラウザーでの再ログインまで完結する`, async ({ page, browser }, testInfo) => {
    const data = { ...registration(), nickname: `一通確認${viewport.width}`, interests: ['写真', '天体観測'], wantsToLearn: ['園芸', '野菜作り'], teaches: ['料理', 'パン作り'] };
    await page.setViewportSize(viewport);
    await fillSignup(page, data);
    await page.getByRole('button', { name: 'アカウントを作成', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'ミライフへようこそ！' })).toBeVisible();
    await page.getByRole('link', { name: /マイページへ/ }).click();
    await assertAccount(page, data);
    const row = await database.user.findUniqueOrThrow({ where: { email: data.email }, include: { profile: true, preferences: true, consents: true, authAccounts: true } });
    expect(row.profile?.nickname).toBe(data.nickname);
    expect(row.profile?.avatarId).toBe('cat');
    expect(row.profile?.wantsToLearn).toEqual(data.wantsToLearn);
    expect(row.profile?.teaches).toEqual(data.teaches);
    expect(row.preferences?.interests).toEqual(data.interests);
    expect(row.preferences?.strengths).toBe(data.strengths);
    expect(row.preferences?.goal).toBe(data.goal);
    expect(row.consents.map(consent => consent.policy).sort()).toEqual(['account-data-storage', 'adult-self-declaration', 'development-use']);
    expect(row.authAccounts).toHaveLength(1);
    expect(row.authAccounts[0].password === data.password).toBe(false);
    expect(await database.authSession.count({ where: { userId: row.id } })).toBe(1);
    await page.reload();
    await assertAccount(page, data);
    await page.screenshot({ path: testInfo.outputPath('account-after-ui-signup.png'), fullPage: true });
    await page.getByRole('link', { name: 'トップページへ', exact: true }).click();
    if (viewport.width === 390) await page.getByRole('button', { name: 'メニュー', exact: true }).click();
    await expect(page.getByRole('link', { name: 'マイページ', exact: true })).toBeVisible();
    if (viewport.width === 390) {
      await page.screenshot({ path: testInfo.outputPath('authenticated-mobile-menu.png') });
      await page.getByRole('button', { name: 'メニュー', exact: true }).click();
    }
    await page.getByRole('link', { name: 'ワールドに入る', exact: true }).first().click();
    await expect(page.getByTitle('ミライフ Unityワールド')).toBeVisible();
    await page.getByRole('link', { name: 'TOPへ戻る', exact: true }).click();
    if (viewport.width === 390) await page.getByRole('button', { name: 'メニュー', exact: true }).click();
    await page.getByRole('link', { name: 'マイページ', exact: true }).click();
    await assertAccount(page, data);
    await page.getByRole('button', { name: 'ログアウト', exact: true }).click();
    await expect(page).toHaveURL(/\/login$/);
    expect(await database.authSession.count({ where: { userId: row.id } })).toBe(0);
    await page.goto('/account');
    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByText(data.goal, { exact: true })).toHaveCount(0);
    await page.goto('/');
    if (viewport.width === 390) await page.getByRole('button', { name: 'メニュー', exact: true }).click();
    await expect(page.locator('.home-header').getByRole('link', { name: 'ログイン', exact: true })).toBeVisible();
    await expect(page.locator('.home-header').getByRole('link', { name: 'マイページ', exact: true })).toHaveCount(0);
    const freshContext = await browser.newContext({ baseURL: origin, viewport });
    try {
      const freshPage = await freshContext.newPage();
      await freshPage.goto('/account');
      await expect(freshPage).toHaveURL(/\/login$/);
      await freshPage.getByLabel('メールアドレス', { exact: true }).fill(data.email.toUpperCase());
      await freshPage.getByLabel('パスワード', { exact: true }).fill(data.password);
      await freshPage.getByRole('button', { name: 'ログイン', exact: true }).click();
      await expect(freshPage).toHaveURL(/\/avatar$/);
      await freshPage.goto('/account');
      await assertAccount(freshPage, data);
      await freshPage.reload();
      await assertAccount(freshPage, data);
      await freshPage.screenshot({ path: testInfo.outputPath('account-after-fresh-login.png'), fullPage: true });
      await freshPage.goto('/login');
      await expect(freshPage).toHaveURL(/\/account$/);
      await freshPage.getByRole('button', { name: 'ログアウト', exact: true }).click();
      await expect(freshPage).toHaveURL(/\/login$/);
      expect(await database.authSession.count({ where: { userId: row.id } })).toBe(0);
    } finally { await freshContext.close(); }
  });
}

test('別利用者のIDを指定しても表示内容を切り替えられず、Cookieも共有しない', async ({ page, browser }) => {
  const first = { ...registration(), nickname: '利用者ひとりめ', goal: `秘密A-${randomUUID()}` };
  const second = { ...registration(), nickname: '利用者ふたりめ', goal: `秘密B-${randomUUID()}` };
  expect((await post(page.request, '/api/account/signup', first)).ok()).toBe(true);
  const secondContext = await browser.newContext({ baseURL: origin });
  try {
    expect(await (await secondContext.request.get('/api/auth/get-session')).json()).toEqual({ authenticated: false });
    expect((await post(secondContext.request, '/api/account/signup', second)).ok()).toBe(true);
    const [firstUser, secondUser] = await Promise.all([first, second].map(data => database.user.findUniqueOrThrow({ where: { email: data.email } })));
    expect((await (await page.request.get(`/api/unity/session?userId=${secondUser.id}`)).json()).profile.userId).toBe(firstUser.id);
    expect((await (await secondContext.request.get(`/api/unity/session?userId=${firstUser.id}`)).json()).profile.userId).toBe(secondUser.id);
    const secondPage = await secondContext.newPage();
    await page.goto(`/account?userId=${secondUser.id}`);
    await assertAccount(page, first);
    await expect(page.getByText(second.email, { exact: true })).toHaveCount(0);
    await expect(page.getByText(second.goal, { exact: true })).toHaveCount(0);
    await secondPage.goto(`/account?userId=${firstUser.id}`);
    await assertAccount(secondPage, second);
    await expect(secondPage.getByText(first.email, { exact: true })).toHaveCount(0);
    await expect(secondPage.getByText(first.goal, { exact: true })).toHaveCount(0);
    await page.getByRole('button', { name: 'ログアウト', exact: true }).click();
    await expect(page).toHaveURL(/\/login$/);
    await secondPage.reload();
    await assertAccount(secondPage, second);
  } finally { await secondContext.close(); }
});

test('登録・ログアウト・ログインの通信失敗後も入力と状態を保って再試行できる', async ({ page }) => {
  const data = registration();
  await fillSignup(page, data);
  const abortOnce = async (path: string) => {
    let failed = false;
    await page.route(`**${path}`, route => {
      if (!failed) { failed = true; return route.abort('connectionfailed'); }
      return route.continue();
    });
  };
  await abortOnce('/api/account/signup');
  await page.getByRole('button', { name: 'アカウントを作成', exact: true }).click();
  await expect(page.getByRole('alert').filter({ hasText: '接続できませんでした' })).toBeVisible();
  await expect(page.locator('#goal')).toHaveValue(data.goal);
  expect(await database.user.count({ where: { email: data.email } })).toBe(0);
  await page.getByRole('button', { name: 'アカウントを作成', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'ミライフへようこそ！' })).toBeVisible();
  expect(await database.user.count({ where: { email: data.email } })).toBe(1);
  await page.getByRole('link', { name: /マイページへ/ }).click();
  await assertAccount(page, data);
  await abortOnce('/api/auth/sign-out');
  await page.getByRole('button', { name: 'ログアウト', exact: true }).click();
  await expect(page.getByRole('alert').filter({ hasText: 'ログアウトできませんでした' })).toBeVisible();
  await assertAccount(page, data);
  expect(await (await page.request.get('/api/auth/get-session')).json()).toEqual({ authenticated: true });
  await page.getByRole('button', { name: 'ログアウト', exact: true }).click();
  await expect(page).toHaveURL(/\/login$/);
  await abortOnce('/api/auth/sign-in/email');
  await page.getByLabel('メールアドレス', { exact: true }).fill(data.email);
  await page.getByLabel('パスワード', { exact: true }).fill(data.password);
  await page.getByRole('button', { name: 'ログイン', exact: true }).click();
  await expect(page.getByRole('alert').filter({ hasText: '接続できませんでした' })).toBeVisible();
  await expect(page.getByLabel('パスワード', { exact: true })).toHaveValue(data.password);
  await page.getByRole('button', { name: 'ログイン', exact: true }).click();
  await expect(page).toHaveURL(/\/avatar$/);
  await page.goto('/account');
  await assertAccount(page, data);
});

test('画面を経由しない不正入力・大きい本文・Originなしも登録を拒否する', async ({ request }) => {
  const data = registration();
  for (const invalid of [{ password: 'short', confirmation: 'short' }, { confirmation: 'different-password' }, { avatar: 'unknown' }, { interests: Array(11).fill('写真') }, { nickname: ' ' }, { goal: 'a'.repeat(501) }]) {
    expect((await post(request, '/api/account/signup', { ...data, ...invalid })).status()).toBe(400);
  }
  expect((await post(request, '/api/account/signup', { ...data, extra: 'a'.repeat(17000) })).status()).toBe(400);
  expect((await request.post('/api/account/signup', { data })).status()).toBe(403);
  expect((await request.post('/api/account/signup', { data: JSON.stringify(data), headers: { Origin: origin, 'Content-Type': 'text/plain' } })).status()).toBe(400);
  expect(await database.user.count({ where: { email: data.email } })).toBe(0);
});

test('論理削除された利用者は既存Cookieでも再ログインでも本人情報を取得できない', async ({ page }) => {
  const data = registration();
  expect((await post(page.request, '/api/account/signup', data)).ok()).toBe(true);
  const user = await database.user.findUniqueOrThrow({ where: { email: data.email } });
  await database.user.update({ where: { id: user.id }, data: { deletedAt: new Date() } });
  expect(await (await page.request.get('/api/unity/session')).json()).toEqual({ authenticated: false, profile: null });
  await page.goto('/account');
  await expect(page).toHaveURL(/\/login$/);
  expect(await (await page.request.get('/api/auth/get-session')).json()).toEqual({ authenticated: false });
  expect((await post(page.request, '/api/auth/sign-in/email', { email: data.email, password })).status()).toBe(401);
  await expect(page.getByText(data.goal, { exact: true })).toHaveCount(0);
});
