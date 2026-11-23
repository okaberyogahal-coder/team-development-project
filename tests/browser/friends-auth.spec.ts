import { expect, test, type Page } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { createDatabase } from "../../server/db/connection";
import { authTestDatabaseUrl } from "../support/auth-database";

const database = createDatabase(authTestDatabaseUrl());

test.afterAll(async () => {
  await database.$disconnect();
});

async function register(page: Page, origin: string, nickname: string, emails: string[]) {
  const email = "friends-" + randomUUID() + "@example.invalid";
  emails.push(email);
  const password = "friends-test-password-123";
  const response = await page.request.post("/api/account/signup", {
    headers: { Origin: origin },
    data: {
      email, password, confirmation: password, nickname,
      avatar: "cat", gender: "male", age: 30, adult: true, terms: true, privacy: true,
      interests: [], teaches: [], wantsToLearn: [], strengths: "", goal: "",
    },
  });
  expect(response.ok()).toBe(true);
  expect((await response.json()).signedIn).toBe(true);
  return database.user.findUniqueOrThrow({ where: { email }, select: { id: true } });
}

async function openFriends(page: Page) {
  await page.goto("/account");
  await page.getByRole("link", { name: "フレンドを探す" }).click();
  await expect(page).toHaveURL(/\/friends$/);
}

test("two signed-in users can find each other, decline, resend, accept and remove a friend", async ({ page, browser, baseURL }) => {
  const emails: string[] = [];
  const recipientContext = await browser.newContext({ baseURL });
  try {
    await page.goto("/friends");
    await expect(page).toHaveURL(/\/login$/);
    const origin = new URL(page.url()).origin;
    expect((await page.request.get("/api/friends")).status()).toBe(401);

    const recipientPage = await recipientContext.newPage();
    const senderName = "Friend" + randomUUID().slice(0, 8);
    const recipientName = "凪";
    const sender = await register(page, origin, senderName, emails);
    const recipient = await register(recipientPage, origin, recipientName, emails);
    const receive = recipientPage.getByRole("region", { name: "届いている申請" });
    const sent = page.getByRole("region", { name: "送った申請" });
    const friends = (tab: Page) => tab.getByRole("region", { name: "フレンド", exact: true });

    await openFriends(page);
    await openFriends(recipientPage);

    const wildcard = await page.request.get("/api/friends?nickname=%25");
    expect(wildcard.ok()).toBe(true);
    expect((await wildcard.json()).matches).toEqual([]);

    await page.getByLabel("相手のニックネーム").fill(recipientName);
    await page.getByRole("button", { name: "検索する" }).click();
    await page.getByRole("button", { name: "申請する" }).click();
    await expect(sent).toContainText(recipientName);

    await recipientPage.reload();
    await expect(receive).toContainText(senderName);
    await receive.getByRole("button", { name: "見送る" }).click();
    await expect(receive).toContainText("届いている申請はありません");
    await page.reload();
    await expect(sent).toContainText("送っている申請はありません");

    const lookup = await recipientPage.request.get("/api/friends?nickname=" + senderName.toLowerCase());
    expect((await lookup.json()).matches).toEqual([{ id: sender.id, nickname: senderName, connectionStatus: "none" }]);
    const [senderResponse, recipientResponse] = await Promise.all([
      page.request.post("/api/friends", { headers: { Origin: origin }, data: { targetId: recipient.id } }),
      recipientPage.request.post("/api/friends", { headers: { Origin: origin }, data: { targetId: sender.id } }),
    ]);
    expect([senderResponse.status(), recipientResponse.status()].sort()).toEqual([200, 409]);

    const acceptingPage = senderResponse.ok() ? recipientPage : page;
    const requestingPage = senderResponse.ok() ? page : recipientPage;
    const requester = senderResponse.ok() ? sender : recipient;
    const receiver = senderResponse.ok() ? recipient : sender;
    const pending = await database.friendConnection.findFirstOrThrow({
      where: { OR: [{ userAId: sender.id }, { userBId: sender.id }] },
    });
    expect(pending.requestedById).toBe(requester.id);
    expect(pending.status).toBe("PENDING");
    const ownApproval = await requestingPage.request.patch("/api/friends", {
      headers: { Origin: origin }, data: { targetId: receiver.id, action: "accept" },
    });
    expect(ownApproval.status()).toBe(409);

    await acceptingPage.reload();
    await acceptingPage.getByRole("button", { name: "承認" }).click();
    await expect(friends(acceptingPage)).toContainText(senderResponse.ok() ? senderName : recipientName);
    await requestingPage.reload();
    await expect(friends(requestingPage)).toContainText(senderResponse.ok() ? recipientName : senderName);

    acceptingPage.once("dialog", dialog => dialog.accept());
    await friends(acceptingPage).getByRole("button", { name: "フレンド解除" }).click();
    await expect(friends(acceptingPage)).toContainText("まだフレンドはいません");
    await requestingPage.reload();
    await expect(friends(requestingPage)).toContainText("まだフレンドはいません");
    expect(await database.friendConnection.count({
      where: { OR: [{ userAId: sender.id }, { userBId: sender.id }] },
    })).toBe(0);
  } finally {
    await recipientContext.close();
    await database.user.deleteMany({ where: { email: { in: emails } } });
  }
});
