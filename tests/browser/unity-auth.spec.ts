import { test, expect, type FrameLocator, type Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { createDatabase } from '../../server/db/connection';
import { authTestDatabaseUrl } from '../support/auth-database';

const database = createDatabase(authTestDatabaseUrl());
const emails: string[] = [];
const origin = 'http://127.0.0.1:3012';
const password = 'unity-auth-test-password-123';
type World = Page | FrameLocator;
type Applied = { authenticated: boolean; revision: number; state: string; userId: string; nickname: string; avatarId: string; avatarLabel: string; suitColor: string; teaches: string[]; wantsToLearn: string[] };

function registration(nickname: string, avatar: string) {
  const email = `${randomUUID()}@example.invalid`;
  emails.push(email);
  return { email, password, confirmation: password, nickname, avatar, adult: true, terms: true, privacy: true,
    interests: ['写真'], teaches: ['料理', 'パン作り'], wantsToLearn: ['園芸', '野菜作り'], strengths: '', goal: '非公開の目標' };
}

async function applied(world: World) {
  return world.locator('body').evaluate(() => (window as Window & { melifeAppliedSession?: Applied }).melifeAppliedSession);
}

async function ready(world: World, state: string) {
  await expect(world.locator('body')).toHaveAttribute('data-unity-state', 'ready', { timeout: 150000 });
  await expect(world.locator('body')).toHaveAttribute('data-session-state', state, { timeout: 20000 });
}

async function login(page: Page, data: ReturnType<typeof registration>) {
  await page.goto('/login');
  await page.getByLabel('メールアドレス', { exact: true }).fill(data.email);
  await page.getByLabel('パスワード', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'ログイン', exact: true }).click();
  await expect(page).toHaveURL(/\/account$/);
}

async function openProfile(page: Page, world: World) {
  const canvas = world.locator('#unity-canvas');
  const bounds = await canvas.boundingBox();
  if (!bounds) throw new Error('Unity canvas is missing');
  // IMGUI buttons are painted into the canvas. Click the visible entry button.
  await canvas.click({ position: { x: bounds.width * 0.725, y: bounds.height * 0.875 }, delay: 150 });
  await page.waitForTimeout(1000);
  await page.keyboard.press('5', { delay: 200 });
  await page.waitForTimeout(1000);
}

test.afterAll(async () => {
  try { await database.user.deleteMany({ where: { email: { in: emails } } }); }
  finally { await database.$disconnect(); }
});

test('起動中のUnityに別タブのログイン・別利用者・通信復帰・期限切れ・ログアウトが反映される', async ({ page, context }, testInfo) => {
  const first = registration('きつねの学び仲間', 'fox');
  const second = registration('ふたりめの見学者', 'penguin');
  for (const data of [first, second]) {
    const response = await page.request.post('/api/account/signup', { data, headers: { Origin: origin } });
    expect(response.ok(), await response.text()).toBe(true);
    expect((await page.request.post('/api/auth/sign-out', { data: {}, headers: { Origin: origin } })).ok()).toBe(true);
  }
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  page.on('console', message => {
    if (message.type() === 'error' && /Exception|memory access|SendMessage.*not found|abort\(/i.test(message.text())) errors.push(message.text());
  });
  await page.goto('/plaza');
  const world = page.frameLocator('iframe[title="ミライフ Unityワールド"]');
  await ready(world, 'guest');
  expect(await applied(world)).toMatchObject({ authenticated: false, nickname: 'ゲスト', userId: '', teaches: [], wantsToLearn: [] });
  await expect(world.locator('#session-account')).toHaveText('ログインする');
  const account = await context.newPage();
  try {
    await login(account, first);
    await ready(world, 'authenticated');
    const firstUser = await database.user.findUniqueOrThrow({ where: { email: first.email } });
    expect(await applied(world)).toMatchObject({ authenticated: true, userId: firstUser.id, nickname: first.nickname,
      avatarId: 'fox', avatarLabel: 'きつね', suitColor: 'FA9447', teaches: first.teaches, wantsToLearn: first.wantsToLearn });
    await expect(world.locator('#session-status')).toHaveText(`${first.nickname}さんで参加中`);
    await expect(world.locator('#session-avatar')).toHaveAttribute('src', '/images/icons/fox.svg');
    await page.bringToFront();
    await openProfile(page, world);
    await page.screenshot({ path: testInfo.outputPath('unity-account-profile.png') });
    await page.keyboard.press('Escape', { delay: 200 });
    await page.screenshot({ path: testInfo.outputPath('unity-account-avatar.png') });
    // Escape closes the profile first; a second Escape releases 3D mouse capture.
    await page.keyboard.press('Escape', { delay: 200 });
    await expect.poll(() => world.locator('body').evaluate(() => document.pointerLockElement === null)).toBe(true);

    // The native HTML toolbar remains usable while Unity is running.
    await world.getByRole('button', { name: 'ログアウト', exact: true }).click();
    await ready(world, 'guest');
    expect(await applied(world)).toMatchObject({ authenticated: false, userId: '', nickname: 'ゲスト', teaches: [], wantsToLearn: [] });
    expect(await database.authSession.count({ where: { userId: firstUser.id } })).toBe(0);
    await expect(world.locator('#session-avatar')).toBeHidden();

    await login(account, second);
    await ready(world, 'authenticated');
    const secondUser = await database.user.findUniqueOrThrow({ where: { email: second.email } });
    expect(await applied(world)).toMatchObject({ userId: secondUser.id, nickname: second.nickname, avatarId: 'penguin', suitColor: '597399' });
    expect((await applied(world))?.userId).not.toBe(firstUser.id);

    // Verification failure clears the last identity; retry re-fetches the cookie owner.
    await page.route('**/api/unity/session', route => route.abort('connectionfailed'));
    await world.locator('body').evaluate(() => window.dispatchEvent(new Event('focus')));
    await ready(world, 'unavailable');
    expect(await applied(world)).toMatchObject({ authenticated: false, userId: '', nickname: 'ゲスト', teaches: [], wantsToLearn: [] });
    await expect(world.getByRole('alert')).toContainText('ログイン状態を確認できません');
    await page.unroute('**/api/unity/session');
    await world.getByRole('button', { name: '再確認', exact: true }).click();
    await ready(world, 'authenticated');
    expect((await applied(world))?.userId).toBe(secondUser.id);

    // No page reload or BroadcastChannel event: the periodic check detects expiry.
    await database.authSession.updateMany({ where: { userId: secondUser.id }, data: { expiresAt: new Date(Date.now() - 1000) } });
    await ready(world, 'guest');
    expect(await applied(world)).toMatchObject({ authenticated: false, userId: '', nickname: 'ゲスト', teaches: [], wantsToLearn: [] });
    await login(account, first);
    await ready(world, 'authenticated');
    await account.getByRole('button', { name: 'ログアウト', exact: true }).click();
    await expect(account).toHaveURL(/\/login$/);
    await ready(world, 'guest');
    expect(await applied(world)).toMatchObject({ authenticated: false, userId: '', nickname: 'ゲスト' });
    await page.bringToFront();
    await page.screenshot({ path: testInfo.outputPath('unity-after-logout.png') });
    expect(errors).toEqual([]);
  } finally { await account.close(); }
});

test('ワールドを単独で開いても本人の登録情報で参加し、マイページへ戻れる', async ({ page }, testInfo) => {
  const data = registration('ねこの参加者', 'cat');
  const response = await page.request.post('/api/account/signup', { data, headers: { Origin: origin } });
  expect(response.ok(), await response.text()).toBe(true);
  await page.goto('/account');
  await page.getByRole('link', { name: 'ワールドに入る', exact: true }).click();
  await expect(page).toHaveURL(/\/plaza$/);
  // The same generated player also works outside its iframe.
  await page.goto('/unity/FutureCity/index.html');
  await ready(page, 'authenticated');
  expect(await applied(page)).toMatchObject({ nickname: data.nickname, avatarId: 'cat', teaches: data.teaches, wantsToLearn: data.wantsToLearn });
  await page.getByRole('link', { name: 'マイページ', exact: true }).click();
  await expect(page).toHaveURL(/\/account$/);
  await expect(page.getByRole('heading', { name: `こんにちは、${data.nickname}さん。` })).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath('account-after-unity.png') });
});
