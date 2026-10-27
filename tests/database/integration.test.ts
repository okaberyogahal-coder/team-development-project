import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { after, before, test } from "node:test";
import { createDatabase } from "../../server/db/connection";

const connectionString = process.env.DATABASE_URL;
if (!connectionString) throw new Error("Run npm run test:db against the isolated test database.");
const target = new URL(connectionString);
if (!["localhost", "127.0.0.1", "[::1]"].includes(target.hostname) || !target.pathname.endsWith("_test")) {
  throw new Error("Refusing to run integration tests outside a local _test database.");
}
const database = createDatabase(connectionString);
const userIds: string[] = [];
let authorId: string;
let lectureId: string;
let learnerIds: string[];

before(async () => {
  for (let i = 0; i < 14; i++) {
    const user = await database.user.create({
      data: {
        email: `${randomUUID()}@example.invalid`,
        status: "ACTIVE",
        profile: { create: { nickname: `検証${i}` } },
      },
    });
    userIds.push(user.id);
  }
  [authorId, ...learnerIds] = userIds;
  const lecture = await database.lecture.create({
    data: { authorId, title: "予約制講座", description: "DB整合性検証用", status: "PUBLISHED" },
  });
  lectureId = lecture.id;
});

after(async () => {
  try {
    await database.lectureBooking.deleteMany({ where: { session: { lecture: { authorId: { in: userIds } } } } });
    await database.lectureSession.deleteMany({ where: { lecture: { authorId: { in: userIds } } } });
    await database.lecture.deleteMany({ where: { authorId: { in: userIds } } });
    await database.materialProgress.deleteMany({ where: { material: { authorId: { in: userIds } } } });
    await database.learningMaterial.deleteMany({ where: { authorId: { in: userIds } } });
    await database.mediaAsset.deleteMany({ where: { ownerId: { in: userIds } } });
    await database.user.deleteMany({ where: { id: { in: userIds } } });
  } finally {
    await database.$disconnect();
  }
});

async function session(capacity = 1) {
  return database.lectureSession.create({
    data: {
      lectureId, capacity, status: "OPEN",
      startsAt: new Date(Date.now() + 3600000),
      endsAt: new Date(Date.now() + 7200000),
    },
  });
}

test("appスキーマはPUBLICに公開されず、実DBに16テーブル存在する", async () => {
  const [result] = await database.$queryRaw<{ tables: bigint; publicUsage: boolean }[]>`
    SELECT (SELECT count(*) FROM information_schema.tables WHERE table_schema = 'app' AND table_type = 'BASE TABLE') AS tables,
      EXISTS(SELECT 1 FROM pg_namespace n,
        LATERAL aclexplode(coalesce(n.nspacl, acldefault('n', n.nspowner))) acl
        WHERE n.nspname = 'app' AND acl.grantee = 0 AND acl.privilege_type = 'USAGE') AS "publicUsage"
  `;
  assert.equal(result.tables, BigInt(16));
  assert.equal(result.publicUsage, false);
});

test("アカウントは未有効状態で作成され、プロフィール作成失敗はアカウントもロールバックする", async () => {
  const pending = await database.user.create({ data: {} });
  userIds.push(pending.id);
  assert.equal(pending.status, "PENDING");
  const email = `${randomUUID()}@example.invalid`;
  await assert.rejects(database.user.create({ data: { email, profile: { create: { nickname: " " } } } }));
  assert.equal(await database.user.count({ where: { email } }), 0);
});

test("メールと外部認証IDの重複、および未正規化メールをDBが拒否する", async () => {
  const user = await database.user.findUniqueOrThrow({ where: { id: authorId } });
  await assert.rejects(database.user.create({ data: { email: user.email } }));
  await assert.rejects(database.user.create({ data: { email: `${randomUUID()}@EXAMPLE.INVALID` } }));
  const subject = randomUUID();
  await database.authIdentity.create({ data: { userId: authorId, provider: "test", subject } });
  await assert.rejects(database.authIdentity.create({ data: { userId: learnerIds[0], provider: "test", subject } }));
});

test("公開プロフィールと非公開の目標・AI年齢利用設定を分離して保存する", async () => {
  await database.learningPreferences.create({ data: { userId: authorId, goal: "本人だけの目標" } });
  const publicProfile = await database.userProfile.findUniqueOrThrow({ where: { userId: authorId } });
  assert.equal("goal" in publicProfile, false);
  const preferences = await database.learningPreferences.findUniqueOrThrow({ where: { userId: authorId } });
  assert.equal(preferences.goal, "本人だけの目標");
  assert.equal(preferences.useAgeForAi, false);
  await assert.rejects(database.qualification.create({ data: { userId: authorId, name: "資格", acquiredMonth: "2026-13" } }));
});

test("別の所有者のメディア、負のサイズ、メディアなしの教材公開をDBが拒否する", async () => {
  const media = await database.mediaAsset.create({ data: {
    ownerId: authorId, storageKey: `test/${randomUUID()}`, mimeType: "video/mp4", kind: "VIDEO", status: "READY",
  } });
  await assert.rejects(database.mediaAsset.update({ where: { id: media.id }, data: { byteSize: BigInt(-1) } }));
  await assert.rejects(database.learningMaterial.create({ data: {
    authorId: learnerIds[0], mediaAssetId: media.id, title: "他者のメディア", category: "趣味", kind: "VIDEO",
  } }));
  await assert.rejects(database.learningMaterial.create({ data: {
    authorId, title: "メディアなし", description: "説明", category: "趣味", kind: "VIDEO", status: "PUBLISHED",
  } }));
});

test("本人操作の完了状態を保存し、教材の論理削除後も履歴と題名を保持する", async () => {
  const material = await database.learningMaterial.create({ data: { authorId, title: "履歴に残る教材", category: "趣味", kind: "DOCUMENT" } });
  const progress = await database.materialProgress.create({ data: {
    userId: learnerIds[0], materialId: material.id, materialVersion: material.version, titleSnapshot: material.title,
  } });
  assert.equal(progress.status, "IN_PROGRESS");
  assert.equal(progress.completedAt, null);
  await assert.rejects(database.materialProgress.update({ where: { userId_materialId: { userId: learnerIds[0], materialId: material.id } }, data: { status: "COMPLETED" } }));
  await database.materialProgress.update({ where: { userId_materialId: { userId: learnerIds[0], materialId: material.id } }, data: { status: "COMPLETED", completedAt: new Date() } });
  await database.learningMaterial.update({ where: { id: material.id }, data: { deletedAt: new Date() } });
  const history = await database.materialProgress.findUniqueOrThrow({ where: { userId_materialId: { userId: learnerIds[0], materialId: material.id } } });
  assert.equal(history.titleSnapshot, "履歴に残る教材");
  assert.equal(history.status, "COMPLETED");
  await assert.rejects(database.learningMaterial.delete({ where: { id: material.id } }));
});

test("12人が同時予約しても定員4人を超えず、同じ人の重複予約を拒否する", async () => {
  const target = await session(4);
  const results = await Promise.allSettled(learnerIds.slice(0, 12).map(userId => database.lectureBooking.create({ data: { sessionId: target.id, userId } })));
  assert.equal(results.filter(result => result.status === "fulfilled").length, 4);
  assert.equal(await database.lectureBooking.count({ where: { sessionId: target.id, status: "CONFIRMED" } }), 4);
  const first = await database.lectureBooking.findFirstOrThrow({ where: { sessionId: target.id } });
  await database.lectureSession.update({ where: { id: target.id }, data: { capacity: 5 } });
  await assert.rejects(database.lectureBooking.create({ data: { sessionId: target.id, userId: first.userId } }));
});

test("取消で席を解放し、再予約は同じ予約レコードを使用する", async () => {
  const target = await session();
  const booking = await database.lectureBooking.create({ data: { sessionId: target.id, userId: learnerIds[0] } });
  await database.lectureBooking.update({ where: { id: booking.id }, data: { status: "CANCELLED", cancelledAt: new Date() } });
  const second = await database.lectureBooking.create({ data: { sessionId: target.id, userId: learnerIds[1] } });
  await assert.rejects(database.lectureBooking.update({ where: { id: booking.id }, data: { status: "CONFIRMED", cancelledAt: null } }));
  await database.lectureBooking.update({ where: { id: second.id }, data: { status: "CANCELLED", cancelledAt: new Date() } });
  const rebooked = await database.lectureBooking.update({ where: { id: booking.id }, data: { status: "CONFIRMED", cancelledAt: null } });
  assert.equal(rebooked.id, booking.id);
  assert.equal(await database.lectureBooking.count({ where: { sessionId: target.id, status: "CONFIRMED" } }), 1);
});

test("DB時刻で開始30分前に達した回、非公開の回、講師本人の予約を拒否する", async () => {
  const [closed] = await database.$queryRaw<{ id: string }[]>`
    INSERT INTO app.lecture_sessions (lecture_id, starts_at, ends_at, capacity, status, updated_at)
      VALUES (${lectureId}::uuid, clock_timestamp() + interval '30 minutes', clock_timestamp() + interval '90 minutes', 1, 'OPEN', now())
      RETURNING id
  `;
  await assert.rejects(database.lectureBooking.create({ data: { sessionId: closed.id, userId: learnerIds[0] } }));
  const target = await session();
  await assert.rejects(database.lectureBooking.create({ data: { sessionId: target.id, userId: authorId } }));
  await database.lectureSession.update({ where: { id: target.id }, data: { status: "DRAFT" } });
  await assert.rejects(database.lectureBooking.create({ data: { sessionId: target.id, userId: learnerIds[0] } }));
});

test("予約がある開催回の定員削減・無通知の日程変更と、不正な日時を拒否する", async () => {
  const target = await session(2);
  await database.lectureBooking.create({ data: { sessionId: target.id, userId: learnerIds[0] } });
  await database.lectureBooking.create({ data: { sessionId: target.id, userId: learnerIds[1] } });
  await assert.rejects(database.lectureSession.update({ where: { id: target.id }, data: { capacity: 1 } }));
  await assert.rejects(database.lectureSession.update({ where: { id: target.id }, data: { startsAt: new Date(target.startsAt.getTime() + 60000) } }));
  await assert.rejects(database.lectureSession.create({ data: { lectureId, startsAt: new Date(), endsAt: new Date(0), capacity: 1 } }));
});

test("スナップショット分離の同時予約でも最後の1席を二重確定しない", async () => {
  const target = await session();
  const results = await Promise.allSettled(learnerIds.slice(0, 2).map(userId => database.$transaction(
    tx => tx.lectureBooking.create({ data: { sessionId: target.id, userId } }),
    { isolationLevel: "RepeatableRead" },
  )));
  assert.equal(results.filter(result => result.status === "fulfilled").length, 1);
  assert.equal(await database.lectureBooking.count({ where: { sessionId: target.id } }), 1);
});
