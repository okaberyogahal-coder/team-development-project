import { expect, test } from '@playwright/test';

test('セクションはスクロールで画面に入ると一度だけ表示状態になる', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 650 });
  await page.goto('/');

  const community = page.locator('#community');
  await expect(community).toHaveClass(/scroll-reveal-ready/);
  await community.scrollIntoViewIfNeeded();
  await expect(community).toHaveClass(/scroll-revealed/);
});

test('動きを減らす設定ではセクションを静止表示する', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 650 });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/');

  const community = page.locator('#community');
  await expect(community).toHaveClass(/scroll-reveal-ready/);
  await expect.poll(() => community.evaluate(element => getComputedStyle(element).opacity)).toBe('1');
  await expect.poll(() => community.evaluate(element => getComputedStyle(element).transform)).toBe('none');
});
