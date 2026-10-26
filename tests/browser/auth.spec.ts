import { test, expect } from '@playwright/test';

test('トップから登録とログインへ進める', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('link', { name: 'ログイン', exact: true }).first().click();
  await expect(page.getByRole('heading', { name: 'おかえりなさい。' })).toBeVisible();
  await page.getByRole('link', { name: 'アカウントを作成' }).click();
  await expect(page.getByRole('heading', { name: 'アカウントを作成' })).toBeVisible();
});

test('登録の入力確認と戻る操作、体験完了まで進める', async ({ page }) => {
  await page.goto('/signup');
  await page.getByLabel('メールアドレス', { exact: true }).fill('hello@example.com');
  await page.getByLabel('パスワード', { exact: true }).fill('password123');
  await page.getByLabel('パスワード（確認）', { exact: true }).fill('different123');
  await page.getByLabel('18歳以上です').check();
  await page.getByLabel('利用規約に同意します').check();
  await page.getByLabel('プライバシーポリシーに同意します').check();
  await page.getByRole('button', { name: '次へ' }).click();
  await expect(page.getByRole('alert').filter({ hasText: 'パスワードが一致しません' })).toBeVisible();
  await page.getByLabel('パスワード（確認）', { exact: true }).fill('password123');
  await page.getByRole('button', { name: '次へ' }).click();
  await page.getByLabel('ニックネーム').fill('みらい');
  await page.getByRole('button', { name: '次へ' }).click();
  await page.getByRole('button', { name: '戻る', exact: true }).click();
  await expect(page.getByLabel('ニックネーム')).toHaveValue('みらい');
  await page.getByRole('button', { name: '次へ' }).click();
  await page.getByRole('button', { name: '入力を完了する' }).click();
  await expect(page.getByRole('heading', { name: 'ミライフへようこそ！' })).toBeVisible();
  await expect(page.getByText('体験プロフィールを作成しました。')).toBeVisible();
  await page.getByRole('link', { name: '3Dアバターを作成する' }).click();
  await expect(page).toHaveURL(/\/avatar$/);
  await expect(page.getByTitle('3Dアバター作成')).toBeVisible();
});

test('ログインは表示切替と認証エラーを提供する', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('メールアドレス', { exact: true }).fill('hello@example.com');
  await page.getByLabel('パスワード', { exact: true }).fill('password123');
  await page.getByRole('button', { name: 'パスワードを表示', exact: true }).click();
  await expect(page.getByLabel('パスワード', { exact: true })).toHaveAttribute('type', 'text');
  await page.getByRole('button', { name: 'ログイン', exact: true }).click();
  await expect(page.getByRole('alert').filter({ hasText: 'メールアドレスまたはパスワード' })).toBeVisible();
});

test('ログインとアカウント作成でロゴをヘッダーサイズに収める', async ({ page }) => {
  for (const width of [1440, 390]) {
    await page.setViewportSize({ width, height: 900 });
    for (const route of ['/login', '/signup']) {
      await page.goto(route);
      const logo = page.locator('.auth-header .home-brand-logo-image');
      await expect(logo).toBeVisible();
      const logoWidth = await logo.evaluate(image => image.getBoundingClientRect().width);
      expect(logoWidth).toBeGreaterThanOrEqual(140);
      expect(logoWidth).toBeLessThanOrEqual(200);
    }
  }
});

for (const width of [1440, 390]) {
  test(`画面幅${width}で横にはみ出さず表示できる`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    for (const route of ['/', '/signup', '/login']) {
      await page.goto(route);
      await expect(page.locator('h1')).toBeVisible();
      if (route === '/') {
        for (const picture of await page.locator('main img').all()) {
          // Decorative images can be hidden by the mobile layout.
          if (await picture.isVisible()) await picture.scrollIntoViewIfNeeded();
          await expect.poll(() => picture.evaluate(image => (image as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
        }
        await page.evaluate(() => window.scrollTo({ top: 0, behavior: 'instant' }));
      }
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
      await page.screenshot({ path: `test-results/milife-${route === '/' ? 'home' : route.slice(1)}-${width}.png`, fullPage: true });
    }
  });
}

for (const viewport of [{ width: 1366, height: 768 }, { width: 1024, height: 600 }]) {
  test(`PC ${viewport.width}x${viewport.height}では認証ページ全体が上下に動かない`, async ({ page }) => {
    await page.setViewportSize(viewport);
    for (const route of ['/login', '/signup']) {
      await page.goto(route);
      expect(await page.evaluate(() => document.documentElement.scrollHeight <= window.innerHeight)).toBe(true);
      const panel = page.getByRole('region', { name: 'アカウント入力' });
      const initial = await panel.boundingBox();
      await panel.hover();
      await page.mouse.wheel(0, 700);
      await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
      expect(await panel.boundingBox()).toEqual(initial);
    }
    await page.getByLabel('メールアドレス', { exact: true }).fill('hello@example.com');
    await page.getByLabel('パスワード', { exact: true }).fill('password123');
    await page.getByLabel('パスワード（確認）', { exact: true }).fill('password123');
    await page.getByLabel('18歳以上です').check();
    await page.getByLabel('利用規約に同意します').check();
    await page.getByLabel('プライバシーポリシーに同意します').check();
    await page.getByRole('button', { name: '次へ' }).click();
    await expect(page.getByLabel('ニックネーム')).toBeVisible();
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
    await expect.poll(() => page.getByRole('region', { name: 'アカウント入力' }).evaluate(element => element.scrollTop)).toBe(0);
    await page.screenshot({ path: `test-results/auth-fixed-${viewport.width}.png` });
  });
}

for (const viewport of [{ width: 1366, height: 768 }, { width: 1440, height: 900 }]) {
  test(`PC ${viewport.width}x${viewport.height}では入力と操作ボタンを一画面で見渡せる`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await page.goto('/signup');
    const next = page.getByRole('button', { name: '次へ', exact: true });
    const bounds = await next.boundingBox();
    expect(bounds).not.toBeNull();
    expect(bounds!.y + bounds!.height).toBeLessThanOrEqual(viewport.height);
    const assertNoFormScroll = async () => {
      const overflow = await page.getByRole('region', { name: 'アカウント入力' }).evaluate(element =>
        [element, ...element.querySelectorAll('*')].filter(node => {
          const style = getComputedStyle(node);
          return /auto|scroll/.test(style.overflowY) && node.scrollHeight > node.clientHeight + 1;
        }).map(node => ({ tag: node.tagName, className: node.className, height: node.clientHeight, contentHeight: node.scrollHeight })));
      expect(overflow).toEqual([]);
    };
    await assertNoFormScroll();
    await page.getByLabel('メールアドレス', { exact: true }).fill('hello@example.com');
    await page.getByLabel('パスワード', { exact: true }).fill('password123');
    await page.getByLabel('パスワード（確認）', { exact: true }).fill('password123');
    await page.getByLabel('18歳以上です').check();
    await page.getByLabel('利用規約に同意します').check();
    await page.getByLabel('プライバシーポリシーに同意します').check();
    await next.click();
    expect((await next.boundingBox())!.y).toBeCloseTo(bounds!.y, 0);
    await page.screenshot({ path: `test-results/pop-profile-${viewport.width}.png` });
    await assertNoFormScroll();
    await page.getByLabel('ニックネーム').fill('みらい');
    await next.click();
    const finish = page.getByRole('button', { name: '入力を完了する' });
    expect((await finish.boundingBox())!.y).toBeCloseTo(bounds!.y, 0);
    await page.screenshot({ path: `test-results/pop-interests-${viewport.width}.png` });
    await assertNoFormScroll();
    await page.goto('/login');
    await expect(page.getByRole('button', { name: 'ログイン', exact: true })).toBeInViewport();
    await expect(page.getByRole('link', { name: 'アカウントを作成', exact: true })).toBeInViewport();
    await assertNoFormScroll();
  });
}

for (const viewport of [{ width: 1280, height: 720 }, { width: 1366, height: 650 }, { width: 1024, height: 600 }]) {
  test(`PC ${viewport.width}x${viewport.height}ではメール入力領域がスクロールしない`, async ({ page }) => {
    await page.setViewportSize(viewport);
    for (const route of ['/login', '/signup']) {
      await page.goto(route);
      const email = page.getByLabel('メールアドレス', { exact: true });
      await email.fill('hello@example.com');
      const overflow = await email.evaluate(element => {
        const result = [];
        for (let node = element.parentElement; node; node = node.parentElement) {
          if (/auto|scroll/.test(getComputedStyle(node).overflowY) && node.scrollHeight > node.clientHeight + 1) {
            result.push({ className: node.className, height: node.clientHeight, content: node.scrollHeight });
          }
        }
        return result;
      });
      expect(overflow).toEqual([]);
      const before = await email.boundingBox();
      await email.hover();
      await page.mouse.wheel(0, 300);
      await page.waitForTimeout(150);
      expect(await email.boundingBox()).toEqual(before);
      const action = page.getByRole('button', { name: route === '/login' ? 'ログイン' : '次へ', exact: true });
      await expect(action).toBeInViewport();
      await page.screenshot({ path: `test-results/email-fixed-${route.slice(1)}-${viewport.width}-${viewport.height}.png` });
    }
  });
}
