import { test, expect } from '@playwright/test';

test('TOPからUnityワールドを開き、TOPへ戻れる', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('link', { name: 'ワールドに入る', exact: true }).first().click();
  await expect(page).toHaveURL(/\/plaza$/);
  await expect(page.getByTitle('ミライフ Unityワールド')).toBeVisible();
  await expect(page.getByTitle('ミライフ Unityワールド')).toHaveAttribute('src', '/unity/FutureCity/index.html');
  await page.getByRole('link', { name: 'TOPへ戻る', exact: true }).click();
  await expect(page).toHaveURL('/');
});

test('Unityワールド本体がブラウザー内で起動する', async ({ page }, testInfo) => {
  test.setTimeout(180000);
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  page.on('console', message => {
    if (message.type() === 'error' && /Exception|abort\(|failed to load|could not be loaded/i.test(message.text())) errors.push(message.text());
  });
  await page.goto('/plaza');
  const world = page.frameLocator('iframe[title="ミライフ Unityワールド"]');
  await expect(world.locator('body')).toHaveAttribute('data-unity-state', 'ready', { timeout: 150000 });
  await expect(world.locator('#unity-canvas')).toBeVisible();
  await expect(world.locator('#loading')).toBeHidden();
  await page.waitForTimeout(5000);
  const canvas = world.locator('#unity-canvas');
  const bounds = await canvas.boundingBox();
  if (!bounds) throw new Error('Unity canvas is missing');
  await canvas.click({ position: { x: bounds.width * 0.725, y: bounds.height * 0.875 }, delay: 150 });
  await page.waitForTimeout(2000);
  await page.keyboard.press('Escape', { delay: 200 });
  expect(errors).toEqual([]);
  await page.screenshot({ path: testInfo.outputPath('melife-unity-world.png') });
});
