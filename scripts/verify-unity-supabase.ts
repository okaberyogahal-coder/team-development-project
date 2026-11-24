import assert from 'node:assert/strict';
import {createHash, randomBytes, randomUUID} from 'node:crypto';
import {mkdir, writeFile} from 'node:fs/promises';
import path from 'node:path';
import {loadEnvConfig} from '@next/env';
import {chromium, expect, type Page} from '@playwright/test';
import {createDatabase} from '../server/db/connection';

// Deliberate manual acceptance run. Never called by npm test or the local DB suites.
async function main() {
  loadEnvConfig(process.cwd());
  if (!process.argv.includes('--allow-supabase-test-account')) {
    throw new Error('Pass --allow-supabase-test-account to create and remove one synthetic account in the configured Supabase DB.');
  }
  const connection = process.env.DATABASE_URL;
  assert(connection, 'DATABASE_URL is required.');
  const databaseUrl = new URL(connection);
  assert(databaseUrl.hostname.endsWith('.supabase.com'), 'This check requires the configured Supabase connection.');
  const origin = new URL(process.env.UNITY_VERIFY_URL || 'http://127.0.0.1:3002');
  assert(['127.0.0.1', 'localhost'].includes(origin.hostname) && origin.protocol === 'http:', 'Use the local Next.js server.');
  const runId = randomUUID();
  const email = `unity-check-${runId}@example.invalid`;
  const password = randomBytes(24).toString('base64url');
  // Keep manual acceptance evidence outside Playwright's cleaned test-results directory.
  const output = path.resolve('playwright-report', 'manual', `unity-supabase-${runId}`);
  await mkdir(output, {recursive: true});
  const database = createDatabase(connection);
  const checks: string[] = [];
  let phase = 'preflight';
  let cleanupAllowed = false;
  let userId: string | undefined;
  let failure: unknown;
  let browser: Awaited<ReturnType<typeof chromium.launch>> | undefined;
  let diagnosticPage: Page | undefined;
  const done = (name: string) => { checks.push(name); console.log(`PASS ${name}`); };

  async function login(page: Page) {
    await page.goto('/login');
    await page.getByLabel('メールアドレス', {exact: true}).fill(email);
    await page.getByLabel('パスワード', {exact: true}).fill(password);
    await page.getByRole('button', {name: 'ログイン', exact: true}).click();
    await expect(page).toHaveURL(/\/avatar$/);
    await page.goto('/account');
  }

  try {
    const roles = await database.$queryRaw<{restricted: boolean}[]>`
      SELECT NOT (rolsuper OR rolcreaterole OR rolbypassrls) AS restricted
      FROM pg_roles WHERE rolname = current_user
    `;
    assert.equal(roles[0]?.restricted, true, 'Use the restricted app role.');
    assert.equal(await database.user.count({where: {email}}), 0);
    cleanupAllowed = true;
    done('restricted-role-and-unique-fixture');
    browser = await chromium.launch({
      headless: true,
      ...(process.env.PLAYWRIGHT_EXECUTABLE_PATH
        ? {executablePath: process.env.PLAYWRIGHT_EXECUTABLE_PATH} : {channel: 'chrome'}),
    });
    const context = await browser.newContext({baseURL: origin.origin, viewport: {width: 1440, height: 1000}});
    const page = await context.newPage();
    diagnosticPage = page;
    page.setDefaultTimeout(20000);
    page.setDefaultNavigationTimeout(60000);
    const errors: string[] = [];
    const watch = (target: Page) => {
      target.on('pageerror', error => errors.push(error.name));
      target.on('console', message => {
        if (message.type() === 'error' && /Exception|memory access|SendMessage.*not found|abort\(/i.test(message.text())) errors.push('Unity runtime error');
      });
    };
    watch(page);
    phase = 'screen-registration';
    await page.goto('/account');
    await expect(page).toHaveURL(/\/login$/);
    await page.goto('/signup');
    await page.getByLabel('メールアドレス', {exact: true}).fill(email);
    await page.getByLabel('パスワード', {exact: true}).fill(password);
    await page.getByLabel('パスワード（確認）', {exact: true}).fill(password);
    await page.getByLabel('18歳以上です').check();
    await page.getByLabel('開発版の利用条件を確認しました').check();
    await page.getByLabel('アカウント情報の保存に同意します').check();
    await page.getByRole('button', {name: '次へ', exact: true}).click();
    await page.getByLabel('ニックネーム', {exact: true}).fill('新版接続の確認');
    await page.getByLabel('性別（必須）').selectOption('male');
    await page.getByLabel('年齢（必須・本人のみ）').fill('29');
    await page.getByRole('button', {name: 'アバター 2', exact: true}).click();
    await page.locator('#interests').fill('写真');
    await page.getByRole('button', {name: '次へ', exact: true}).click();
    await page.locator('#learn').fill('園芸');
    await page.locator('#teach').fill('料理');
    await page.locator('#goal').fill('確認用の非公開目標');
    await page.getByRole('button', {name: 'アカウントを作成', exact: true}).click();
    await expect(page.getByText('アカウントを作成しました。', {exact: true})).toBeVisible({timeout: 60000});
    await page.getByRole('link', {name: 'マイページへ'}).click();
    await expect(page.getByRole('heading', {name: 'こんにちは、新版接続の確認さん。'})).toBeVisible();
    const user = await database.user.findUniqueOrThrow({where: {email}, include: {profile: true, preferences: true, consents: true, authAccounts: true}});
    userId = user.id;
    assert.equal(user.profile?.gender, 'male');
    assert.equal(user.profile?.age, 29);
    assert.equal(user.profile?.avatarId, 'cat');
    assert.equal(user.preferences?.goal, '確認用の非公開目標');
    assert.equal(user.consents.length, 3);
    assert(user.authAccounts[0]?.password && user.authAccounts[0].password !== password, 'Credentials must be hashed.');
    await page.reload();
    await expect(page.locator('.account-details')).toContainText('ユース');
    await page.screenshot({path: path.join(output, 'account-created.png'), fullPage: true});
    done('screen-registration-and-supabase-persistence');

    phase = 'screen-relogin';
    await page.getByRole('button', {name: 'ログアウト', exact: true}).click();
    await expect(page).toHaveURL(/\/login$/);
    assert.equal(await database.authSession.count({where: {userId}}), 0);
    await login(page);
    await expect(page.locator('.account-details')).toContainText('確認用の非公開目標');
    done('logout-session-removal-and-screen-relogin');

    phase = 'unity-initial-profile';
    const worldPage = await context.newPage();
    diagnosticPage = worldPage;
    watch(worldPage);
    await worldPage.goto('/avatar');
    const wardrobe = worldPage.frameLocator('iframe[title="3Dアバター作成"]');
    await expect(wardrobe.locator('body')).toHaveAttribute('data-unity-state', 'ready', {timeout: 180000});
    await expect(wardrobe.locator('body')).toHaveAttribute('data-session-state', 'authenticated', {timeout: 60000});
    await worldPage.screenshot({path: path.join(output, 'avatar-entry.png')});
    const wardrobeCanvas = wardrobe.locator('#unity-canvas');
    const wardrobeBounds = await wardrobeCanvas.boundingBox();
    assert(wardrobeBounds, 'Avatar canvas must be visible.');
    const scale = Math.max(0.6, wardrobeBounds.height / 900);
    const panelWidth = Math.min(1480, wardrobeBounds.width / scale - 36);
    const left = (wardrobeBounds.width / scale - panelWidth) / 2;
    // Complete the visible wardrobe entry flow, rather than injecting browser storage.
    await wardrobeCanvas.click({position: {x: (left + panelWidth - 194) * scale, y: 71 * scale}, delay: 150});
    await expect(worldPage).toHaveURL(/\/plaza$/, {timeout: 30000});
    done('visible-avatar-entry-completed');
    const world = worldPage.frameLocator('iframe[title="ミライフ Unityワールド"]');
    const body = world.locator('body');
    await expect(body).toHaveAttribute('data-unity-state', 'ready', {timeout: 180000});
    await expect(body).toHaveAttribute('data-session-state', 'authenticated', {timeout: 60000});
    const applied = () => body.evaluate(() => (window as Window & {
      melifeAppliedSession?: {authenticated: boolean; userId: string; nickname: string; avatarId: string; teaches: string[]; wantsToLearn: string[]};
    }).melifeAppliedSession);
    await expect.poll(async () => (await applied())?.nickname, {timeout: 30000}).toBe('新版接続の確認');
    assert.equal((await applied())?.userId, userId);
    assert.equal((await applied())?.avatarId, 'cat');
    done('real-webgl-cookie-owner-applied');

    phase = 'profile-edit-live-reflection';
    await page.goto('/account/edit');
    await page.getByLabel('ニックネーム（必須）').fill('変更後の学び仲間');
    await page.getByLabel('年齢（必須・本人のみ）').fill('50');
    await page.getByLabel('教えられること', {exact: true}).fill('写真撮影');
    await page.getByLabel('学びたいこと', {exact: true}).fill('野菜作り');
    await page.getByRole('button', {name: '変更を保存', exact: true}).click();
    await expect(page).toHaveURL(/\/account\?updated=1$/);
    await expect(page.locator('.account-details')).toContainText('ミドルシニア');
    const saved = await database.userProfile.findUniqueOrThrow({where: {userId}});
    assert.equal(saved.nickname, '変更後の学び仲間');
    assert.equal(saved.age, 50);
    await worldPage.bringToFront();
    await expect.poll(async () => (await applied())?.nickname, {timeout: 60000}).toBe('変更後の学び仲間');
    assert.deepEqual((await applied())?.teaches, ['写真撮影']);
    assert.deepEqual((await applied())?.wantsToLearn, ['野菜作り']);
    const sessionResponse = await page.request.get('/api/unity/session');
    const session = await sessionResponse.json();
    assert.equal(sessionResponse.headers()['cache-control'], 'no-store');
    assert.deepEqual(Object.keys(session.profile).sort(), ['userId','nickname','avatarId','gender','ageBadge','teaches','wantsToLearn'].sort());
    assert.equal(session.profile.ageBadge, 'ミドルシニア');
    done('saved-profile-live-reflection-and-private-age');

    phase = 'unity-profile-render';
    const canvas = world.locator('#unity-canvas');
    const bounds = await canvas.boundingBox();
    assert(bounds, 'Unity canvas must be visible.');
    // The completed wardrobe already starts the plaza. Use its visible Profile button.
    await canvas.click({position: {x: bounds.width * 0.72, y: bounds.height - 60}, delay: 150});
    await expect.poll(() => body.evaluate(() => {
      const view = window as Window & {unityInstance?: {SendMessage: (object: string, method: string) => void}; melifeWorldInteraction?: {panel: string}};
      view.unityInstance?.SendMessage('MiraiLife', 'ReportWebInteraction');
      return view.melifeWorldInteraction?.panel;
    }), {timeout: 20000}).toBe('Profile');
    await worldPage.screenshot({path: path.join(output, 'unity-updated-profile.png')});
    done('unity-profile-panel-rendered');

    phase = 'logout-and-cross-tab-relogin';
    await worldPage.keyboard.press('Escape', {delay: 200});
    await worldPage.keyboard.press('Escape', {delay: 200});
    await worldPage.locator('.world-viewer-toolbar').getByRole('button', {name: 'ログアウト', exact: true}).click();
    await expect(body).toHaveAttribute('data-session-state', 'guest', {timeout: 60000});
    assert.equal((await applied())?.userId, '');
    assert.deepEqual((await applied())?.teaches, []);
    assert.equal(await database.authSession.count({where: {userId}}), 0);
    await worldPage.screenshot({path: path.join(output, 'unity-logged-out.png')});
    await login(page);
    await worldPage.bringToFront();
    await expect(body).toHaveAttribute('data-session-state', 'authenticated', {timeout: 60000});
    await expect.poll(async () => (await applied())?.nickname, {timeout: 30000}).toBe('変更後の学び仲間');
    assert.equal((await applied())?.userId, userId);
    await worldPage.locator('.world-viewer-toolbar').getByRole('button', {name: 'ログアウト', exact: true}).click();
    await expect(body).toHaveAttribute('data-session-state', 'guest', {timeout: 60000});
    assert.equal(await database.authSession.count({where: {userId}}), 0);
    assert.deepEqual(errors, []);
    done('world-logout-cross-tab-relogin-and-no-runtime-errors');
  } catch (error) {
    failure = error;
    console.error(`FAIL ${phase}`);
    await diagnosticPage?.screenshot({path: path.join(output, 'failure.png')}).catch(() => {});
  } finally {
    try { await browser?.close(); }
    catch (error) { failure ??= error; }
    try {
      if (cleanupAllowed) {
        // Exact, unpredictable fixture email only; related rows cascade. No existing user is selected.
        const fixture = await database.user.findUnique({where: {email}, select: {id: true}});
        await database.user.deleteMany({where: {email}});
        const key = (action: string, value: string) => `account:${action}:${createHash('sha256').update(value).digest('hex')}`;
        const keys = [key('signup', email), key('signin', email)];
        if (fixture) keys.push(key('profile-edit', fixture.id));
        await database.authRateLimit.deleteMany({where: {key: {in: keys}}});
        assert.equal(await database.user.count({where: {email}}), 0);
        assert.equal(await database.authRateLimit.count({where: {key: {in: keys}}}), 0);
        if (fixture) assert.equal(await database.authSession.count({where: {userId: fixture.id}}), 0);
        done('fixture-account-sessions-and-specific-rate-limits-removed');
      }
    } catch (error) { failure ??= error; console.error('FAIL fixture-cleanup'); }
    finally { await database.$disconnect(); }
    // Store no password, cookies, tokens, DB connection string, or existing-user data.
    const message = failure instanceof Error ? failure.message.split(connection).join('[DB]').split(password).join('[PASSWORD]') : null;
    await writeFile(path.join(output, 'result.json'), JSON.stringify({date: new Date().toISOString(), runId, phase, checks, success: !failure, failure: message}, null, 2));
    console.log(`Report: ${path.relative(process.cwd(), output)}`);
    if (failure) process.exitCode = 1;
  }
}

void main().catch(() => { console.error('Verification preflight failed; check the opt-in flag and local environment.'); process.exitCode = 1; });
