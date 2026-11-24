import {test, expect} from '@playwright/test';

// Exercise the real WebGL player with display-profile fixtures; no DB writes or auth claims.
test('新版ワールドで移動・戦闘操作とプロフィール連携を確認する', async ({page}, testInfo) => {
  test.setTimeout(240000);
  let authenticated = false;
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  page.on('console', message => {
    if (message.type() === 'error' && /Exception|abort\(|failed to load|could not be loaded/i.test(message.text())) errors.push(message.text());
  });
  await page.route('**/api/unity/session', route => route.fulfill({json: authenticated ? {
    authenticated: true, profile: {
      userId: '00000000-0000-4000-8000-000000000001', nickname: '新版の確認', avatarId: 'cat',
      gender: 'male', ageBadge: 'ミドルシニア', teaches: ['料理'], wantsToLearn: ['園芸'],
    },
  } : {authenticated: false, profile: null}}));
  await page.goto('/unity/FutureCity/index.html');
  await expect(page.locator('body')).toHaveAttribute('data-unity-state', 'ready', {timeout: 180000});
  await expect(page.locator('body')).toHaveAttribute('data-session-state', 'guest', {timeout: 30000});
  const canvas = page.locator('#unity-canvas');
  const snapshot = async () => {
    await page.evaluate(() => (window as any).unityInstance.SendMessage('MiraiLife', 'ReportWebInteraction'));
    await page.waitForTimeout(150);
    return page.evaluate(() => (window as any).melifeWorldInteraction);
  };
  const enter = async () => {
    const bounds = await canvas.boundingBox();
    if (!bounds) throw new Error('Unity canvas is missing');
    await canvas.click({position: {x: bounds.width * 0.725, y: bounds.height * 0.875}, delay: 150});
    await expect.poll(async () => (await snapshot())?.panel, {timeout: 20000}).toBe('None');
  };
  await enter();
  await page.screenshot({path: testInfo.outputPath('anime-lobby.png')});
  const position = (await snapshot()).position;
  await page.keyboard.down('w');
  await page.waitForTimeout(1200);
  await page.keyboard.up('w');
  await expect.poll(async () => {
    const now = (await snapshot()).position;
    return Math.hypot(now.x - position.x, now.z - position.z);
  }).toBeGreaterThan(0.1);

  await page.keyboard.press('f', {delay: 200});
  await page.waitForTimeout(700);
  await page.screenshot({path: testInfo.outputPath('anime-combat.png')});
  await canvas.click({position: {x: 600, y: 400}, delay: 150});
  await page.keyboard.press('e', {delay: 200});
  await page.mouse.down({button: 'right'});
  await page.waitForTimeout(400);
  await page.screenshot({path: testInfo.outputPath('anime-aim.png')});
  await page.mouse.up({button: 'right'});
  await page.keyboard.press('f', {delay: 200});
  await page.keyboard.press('Escape', {delay: 200});

  authenticated = true;
  await page.evaluate(() => {
    const channel = new BroadcastChannel('melife-auth');
    channel.postMessage('changed');
    channel.close();
  });
  await expect(page.locator('body')).toHaveAttribute('data-session-state', 'authenticated', {timeout: 30000});
  await expect.poll(() => page.evaluate(() => (window as any).melifeAppliedSession?.nickname)).toBe('新版の確認');
  await enter();
  await page.keyboard.press('5', {delay: 200});
  await expect.poll(async () => (await snapshot())?.panel).toBe('Profile');
  await page.screenshot({path: testInfo.outputPath('anime-profile.png')});
  expect(errors).toEqual([]);
});
