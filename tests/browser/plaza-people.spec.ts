import {randomUUID} from 'node:crypto';
import {expect, test, type Page} from '@playwright/test';
import {createDatabase} from '../../server/db/connection';
import {authTestDatabaseUrl} from '../support/auth-database';

const database = createDatabase(authTestDatabaseUrl());
test.afterAll(() => database.$disconnect());

async function register(page: Page, origin: string, nickname: string) {
  const email = 'plaza-' + randomUUID() + '@example.invalid';
  const password = 'plaza-browser-test-123';
  const response = await page.request.post('/api/account/signup', {
    headers: {Origin: origin},
    data: {
      email, password, confirmation: password, nickname, avatar: 'cat', gender: 'female', age: 32,
      adult: true, terms: true, privacy: true, interests: [], teaches: ['写真'], wantsToLearn: ['園芸'],
      strengths: '非公開の得意なこと', goal: '非公開の目標',
    },
  });
  expect(response.ok(), await response.text()).toBe(true);
  return database.user.findUniqueOrThrow({where: {email}, select: {id: true, email: true}});
}

test('real plaza lists active accounts, shows public profiles and sends an existing friend request', async ({page, browser, baseURL}) => {
  const origin = new URL(baseURL!).origin;
  const peerContext = await browser.newContext({baseURL});
  const outsiderContext = await browser.newContext({baseURL});
  const ids: string[] = [];
  try {
    expect((await page.request.post('/api/plaza/people', {headers: {Origin: origin}})).status()).toBe(401);
    const peerPage = await peerContext.newPage();
    const outsiderPage = await outsiderContext.newPage();
    const viewer = await register(page, origin, '広場の送信者');
    ids.push(viewer.id);
    const peer = await register(peerPage, origin, '広場の受信者');
    ids.push(peer.id);
    ids.push((await register(outsiderPage, origin, '広場の外の利用者')).id);
    await Promise.all([page.goto('/plaza'), peerPage.goto('/plaza')]);

    const people = (tab: Page) => tab.getByRole('button', {name: '広場にいる人', exact: true});
    await expect(people(peerPage)).toBeVisible({timeout: 60_000});
    await expect(people(page)).toBeVisible({timeout: 60_000});
    await people(page).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('button', {name: '広場の受信者', exact: true}).click({timeout: 20_000});
    await expect(dialog).toContainText('ミドル');
    await expect(dialog).toContainText('写真');
    await expect(dialog).toContainText('園芸');
    await expect(dialog).not.toContainText(peer.email!);
    await expect(dialog).not.toContainText('非公開');

    const roster = await page.request.post('/api/plaza/people', {headers: {Origin: origin, 'X-Profile-Owner': viewer.id}});
    expect(await roster.json()).toEqual({people: [{
      id: peer.id, nickname: '広場の受信者', ageBadge: 'ミドル',
      teaches: ['写真'], wantsToLearn: ['園芸'], connectionStatus: 'none',
    }]});
    const changedOwner = await page.request.post('/api/friends', {
      headers: {Origin: origin, 'X-Profile-Owner': peer.id}, data: {targetId: peer.id},
    });
    expect(changedOwner.status()).toBe(409);

    await dialog.getByRole('button', {name: 'フレンド申請を送る'}).click();
    await expect(dialog.getByText('フレンド申請を送りました。')).toBeVisible();
    await expect(dialog.getByText('申請済み')).toBeVisible();
    const received = await (await peerPage.request.get('/api/friends')).json();
    expect(received.incoming).toEqual([expect.objectContaining({id: viewer.id})]);
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveCount(0);
    await expect(people(page)).toBeFocused();

    await peerPage.getByRole('button', {name: 'ログアウト', exact: true}).click();
    await expect(people(peerPage)).toHaveCount(0);
    const remaining = await page.request.post('/api/plaza/people', {headers: {Origin: origin, 'X-Profile-Owner': viewer.id}});
    expect(await remaining.json()).toEqual({people: []});
  } finally {
    await Promise.all([peerContext.close(), outsiderContext.close()]);
    await database.user.deleteMany({where: {id: {in: ids}}});
  }
});
