import assert from "node:assert/strict";
import { test } from "node:test";
import { createFriendHandlers } from "../../server/friends/handler";
import { FriendActionError } from "../../server/friends/errors";

const userId = "11111111-1111-4111-8111-111111111111";
const targetId = "22222222-2222-4222-8222-222222222222";
const request = (method: string, body?: unknown, origin = "http://localhost:3002", path = "/api/friends") =>
  new Request(`http://localhost:3002${path}`, {
    method,
    headers: { origin, ...(body === undefined ? {} : { "Content-Type": "application/json" }) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });

function dependencies(overrides: Partial<Parameters<typeof createFriendHandlers>[0]> = {}) {
  return {
    getUser: async () => ({ id: userId }),
    read: async () => ({ friends: [], incoming: [], outgoing: [] }),
    search: async (_id: string, _nickname: string) => [],
    request: async (_id: string, _target: string) => {},
    respond: async (_id: string, _requester: string, _response: "accept" | "decline") => {},
    remove: async (_id: string, _target: string) => {},
    allow: async () => true,
    ...overrides,
  };
}

test("friend list and exact nickname search require authentication and disable caching", async () => {
  const handlers = createFriendHandlers(dependencies({ search: async (id, nickname) => [{ id, nickname }] }));
  const response = await handlers.GET(request("GET", undefined, "http://localhost:3002", "/api/friends?nickname=%E3%81%BF%E3%82%89%E3%81%84"));
  assert.equal(response.status, 200);
  assert.equal(response.headers.get("cache-control"), "no-store");
  assert.deepEqual((await response.json() as { matches: unknown[] }).matches, [{ id: userId, nickname: "みらい" }]);
  assert.equal((await createFriendHandlers(dependencies({ getUser: async () => null })).GET(request("GET"))).status, 401);
  assert.equal((await handlers.GET(request("GET", undefined, "http://localhost:3002", "/api/friends?nickname=x"))).status, 400);
});

test("friend search is rate limited and storage failures are reported without details", async () => {
  const limited = createFriendHandlers(dependencies({ allow: async () => false }));
  assert.equal((await limited.GET(request("GET", undefined, "http://localhost:3002", "/api/friends?nickname=みらい"))).status, 429);

  const failed = createFriendHandlers(dependencies({ read: async () => { throw new Error("database-secret"); } }));
  const response = await failed.GET(request("GET"));
  assert.equal(response.status, 503);
  assert.ok(!(await response.text()).includes("database-secret"));
});

test("friend requests validate origin and identity and pass only the session user to storage", async () => {
  let received: unknown;
  const handlers = createFriendHandlers(dependencies({ request: async (id, target) => { received = [id, target]; } }));
  assert.equal((await handlers.POST(request("POST", { targetId }, "https://attacker.invalid"))).status, 403);
  assert.equal((await handlers.POST(request("POST", { targetId: userId }))).status, 400);
  assert.equal((await handlers.POST(request("POST", { targetId: "not-an-id" }))).status, 400);
  const response = await handlers.POST(request("POST", { targetId }));
  assert.equal(response.status, 200);
  assert.deepEqual(received, [userId, targetId]);
});

test("incoming requests can be accepted or declined and existing states return conflicts", async () => {
  let received: unknown;
  const handlers = createFriendHandlers(dependencies({
    respond: async (id, requester, action) => { received = [id, requester, action]; },
  }));
  assert.equal((await handlers.PATCH(request("PATCH", { targetId, action: "accept" }))).status, 200);
  assert.deepEqual(received, [userId, targetId, "accept"]);
  assert.equal((await handlers.PATCH(request("PATCH", { targetId, action: "ignore" }))).status, 400);

  const conflict = createFriendHandlers(dependencies({
    respond: async () => { throw new FriendActionError("REQUEST_NOT_FOUND"); },
  }));
  assert.equal((await conflict.PATCH(request("PATCH", { targetId, action: "decline" }))).status, 409);
});

test("users can cancel outgoing requests or remove friends", async () => {
  let removed: unknown;
  const handlers = createFriendHandlers(dependencies({ remove: async (id, target) => { removed = [id, target]; } }));
  assert.equal((await handlers.DELETE(request("DELETE", { targetId }))).status, 200);
  assert.deepEqual(removed, [userId, targetId]);
  assert.equal((await createFriendHandlers(dependencies({
    remove: async () => { throw new FriendActionError("NOT_FRIENDS"); },
  })).DELETE(request("DELETE", { targetId }))).status, 409);
});
