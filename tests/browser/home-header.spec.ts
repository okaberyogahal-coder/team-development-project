import { expect, test, type Page } from '@playwright/test';

async function waitForAnchorScroll(page: Page) {
  await page.waitForTimeout(120);
  await expect.poll(async () => {
    const start = await page.evaluate(() => window.scrollY);
    await page.waitForTimeout(120);
    const end = await page.evaluate(() => window.scrollY);
    return Math.abs(end - start) < 1;
  }, { timeout: 5000 }).toBe(true);
}

test('スマホではメニューを開けて、リンク選択後に閉じられる', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');

  const menuButton = page.getByRole('button', { name: 'メニュー' });
  const menu = page.getByRole('navigation', { name: 'モバイルメニュー' });

  await expect(menuButton).toBeVisible();
  await expect(menuButton).toHaveAttribute('aria-expanded', 'false');
  await menuButton.click();
  await expect(menuButton).toHaveAttribute('aria-expanded', 'true');
  await expect(menu).toBeVisible();
  await menu.getByRole('link', { name: '世代をこえて' }).click();

  await expect(page).toHaveURL(/#community$/);
  await expect(menuButton).toHaveAttribute('aria-expanded', 'false');
  await expect(menu).toBeHidden();
  await expect(page.locator('.home-header').getByRole('link', { name: 'ワールドに入る' })).toBeVisible();
  const community = page.locator('#community');
  await expect.poll(() => community.evaluate(element => {
    const header = document.querySelector('.home-header')!.getBoundingClientRect();
    return element.getBoundingClientRect().top + parseFloat(getComputedStyle(element).paddingTop) - header.bottom;
  })).toBeGreaterThanOrEqual(8);
  await expect.poll(() => community.evaluate(element => element.getBoundingClientRect().bottom - window.innerHeight)).toBeGreaterThanOrEqual(-2);
});

test('スマホではハンバーガーがCTAの右隣に揃う', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');

  const cta = await page.locator('.home-header .home-button-small').boundingBox();
  const menu = await page.getByRole('button', { name: 'メニュー' }).boundingBox();
  const header = await page.locator('.home-header-inner').boundingBox();

  expect(cta).not.toBeNull();
  expect(menu).not.toBeNull();
  expect(header).not.toBeNull();
  expect(menu!.x).toBeGreaterThanOrEqual(cta!.x + cta!.width);
  expect(menu!.x - (cta!.x + cta!.width)).toBeLessThanOrEqual(14);
  expect(header!.x + header!.width - (menu!.x + menu!.width)).toBeLessThanOrEqual(20);
});

test('PCではフルナビを表示し、ハンバーガーメニューは隠す', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');

  await expect(page.getByRole('navigation', { name: 'メインメニュー' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'メニュー' })).toBeHidden();
});

for (const viewport of [{ width: 1352, height: 762 }, { width: 390, height: 844 }]) {
  test(`ミライフとははロゴと同じ先頭表示に戻る (${viewport.width}px)`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await page.goto('/#community');
    await page.locator('.home-header').getByRole('link', { name: 'ミライフ トップへ' }).click();
    await page.waitForURL(url => url.pathname === '/' && url.hash === '');
    await page.waitForLoadState('networkidle');
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
    const logoHeroTop = await page.locator('#about').evaluate(element => element.getBoundingClientRect().top);

    const navigation = page.getByRole('navigation', { name: viewport.width > 900 ? 'メインメニュー' : 'モバイルメニュー' });
    if (viewport.width <= 900) await page.getByRole('button', { name: 'メニュー' }).click();
    await navigation.getByRole('link', { name: '世代をこえて' }).click();
    await waitForAnchorScroll(page);
    if (viewport.width <= 900) await page.getByRole('button', { name: 'メニュー' }).click();
    await navigation.getByRole('link', { name: 'ミライフとは' }).click();

    await expect.poll(() => new URL(page.url()).hash).toBe('');
    await page.waitForLoadState('networkidle');
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
    const navHeroTop = await page.locator('#about').evaluate(element => element.getBoundingClientRect().top);
    expect(navHeroTop).toBeCloseTo(logoHeroTop, 0);
  });
}

for (const viewport of [{ width: 1440, height: 1000 }, { width: 390, height: 844 }]) {
  test(`未来ナビへ移動すると黄色いフッターが画面下に隠れる (${viewport.width}px)`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await page.goto('/');
    if (viewport.width <= 900) await page.getByRole('button', { name: 'メニュー' }).click();
    await page.getByRole('navigation', { name: viewport.width > 900 ? 'メインメニュー' : 'モバイルメニュー' }).getByRole('link', { name: '未来ナビ' }).click();
    await waitForAnchorScroll(page);
    await expect(page.locator('#ai-navi')).toHaveCSS('opacity', '1');
    await expect.poll(() => page.locator('.home-footer').evaluate(element => element.getBoundingClientRect().top - window.innerHeight)).toBeGreaterThanOrEqual(0);
  });
}

test('PCの各リンク先はスクリーンショットの画面範囲で表示される', async ({ page }) => {
  await page.setViewportSize({ width: 1352, height: 762 });
  await page.goto('/');

  await page.getByRole('navigation', { name: 'メインメニュー' }).getByRole('link', { name: '世代をこえて' }).click();
  await waitForAnchorScroll(page);
  const community = page.locator('#community');
  await expect(page).toHaveURL(/#community$/);
  await expect.poll(() => community.evaluate(element => {
    const header = document.querySelector('.home-header')!.getBoundingClientRect();
    return document.querySelector('.home-community-art .home-picture-frame')!.getBoundingClientRect().top - header.bottom;
  })).toBeGreaterThanOrEqual(80);
  await expect.poll(() => community.evaluate(() => {
    const header = document.querySelector('.home-header')!.getBoundingClientRect();
    return document.querySelector('.home-community-art .home-picture-frame')!.getBoundingClientRect().top - header.bottom;
  })).toBeLessThanOrEqual(105);
  await expect.poll(() => page.locator('#ai-navi').evaluate(element => getComputedStyle(element).opacity)).toBe('1');
  await expect.poll(() => page.locator('#ai-navi').evaluate(element => element.getBoundingClientRect().top)).toBeGreaterThan(720);
  await expect.poll(() => page.locator('#ai-navi').evaluate(element => element.getBoundingClientRect().top)).toBeLessThan(742);

  await page.getByRole('navigation', { name: 'メインメニュー' }).getByRole('link', { name: 'ミライフとは' }).click();
  await waitForAnchorScroll(page);
  await expect.poll(() => new URL(page.url()).hash).toBe('');
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);

  await page.getByRole('navigation', { name: 'メインメニュー' }).getByRole('link', { name: 'できること' }).click();
  await waitForAnchorScroll(page);
  await expect.poll(() => page.locator('.home-features .home-sticker').evaluate(element => {
    const header = document.querySelector('.home-header')!.getBoundingClientRect();
    return element.getBoundingClientRect().top - header.bottom;
  })).toBeGreaterThanOrEqual(40);
  await expect.poll(() => page.locator('.home-features .home-sticker').evaluate(element => {
    const header = document.querySelector('.home-header')!.getBoundingClientRect();
    return element.getBoundingClientRect().top - header.bottom;
  })).toBeLessThanOrEqual(65);
  await expect.poll(() => page.locator('.home-feature-card').last().evaluate(element => element.getBoundingClientRect().bottom)).toBeGreaterThan(650);
  await expect.poll(() => page.locator('.home-feature-card').last().evaluate(element => element.getBoundingClientRect().bottom)).toBeLessThan(705);

  await page.getByRole('navigation', { name: 'メインメニュー' }).getByRole('link', { name: '未来ナビ' }).click();
  await waitForAnchorScroll(page);
  await expect.poll(() => page.locator('.home-navi-conversation').evaluate(element => {
    const header = document.querySelector('.home-header')!.getBoundingClientRect();
    return element.getBoundingClientRect().top - header.bottom;
  })).toBeGreaterThanOrEqual(68);
  await expect.poll(() => page.locator('.home-navi-conversation').evaluate(element => {
    const header = document.querySelector('.home-header')!.getBoundingClientRect();
    return element.getBoundingClientRect().top - header.bottom;
  })).toBeLessThanOrEqual(78);
  await expect.poll(() => page.locator('.home-footer').evaluate(element => element.getBoundingClientRect().top - window.innerHeight)).toBeGreaterThanOrEqual(0);
});
