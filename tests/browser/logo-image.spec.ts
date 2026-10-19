import { expect, test } from '@playwright/test';

test('ヘッダーのミライフロゴを画像で表示し、ヒーロー見出しはテキストで保つ', async ({ page }) => {
  await page.goto('/');

  const logo = page.locator('header').getByRole('link', { name: 'ミライフ トップへ' });
  await expect(logo.locator('img')).toHaveAttribute('src', /milife-logo-matte\.png/);
  await expect(logo.locator('img')).toBeVisible();

  const heading = page.getByRole('heading', { level: 1 });
  await expect(heading).toHaveText(/いくつになっても、\s*「はじめて」は\s*おもしろい。/);
  await expect(heading.locator('img')).toHaveCount(0);
});
