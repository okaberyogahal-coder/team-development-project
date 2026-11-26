import "server-only";
import type { PrismaClient, Prisma } from "../../generated/prisma/client";
import type { FriendConnectionStatus } from "../../generated/prisma/client";
import { FriendActionError } from "./errors";
import type { FriendConnectionState } from "../../shared/plaza-people";

type Database = Pick<PrismaClient, "friendConnection" | "user">;
type FriendProfile = { id: string; nickname: string };
type ConnectionUser = { id: string; name: string; profile: { nickname: string } | null };
type ConnectionRecord = {
  userAId: string;
  userBId: string;
  requestedById: string;
  status: FriendConnectionStatus;
  createdAt: Date;
  userA: ConnectionUser;
  userB: ConnectionUser;
};

function profile(user: ConnectionUser): FriendProfile {
  return { id: user.id, nickname: user.profile?.nickname || user.name || "利用者" };
}

function pair(userId: string, targetId: string) {
  return userId < targetId
    ? { userAId: userId, userBId: targetId }
    : { userAId: targetId, userBId: userId };
}

function otherUser(connection: ConnectionRecord, userId: string): FriendProfile {
  return profile(connection.userAId === userId ? connection.userB : connection.userA);
}

const connectionUser = {
  id: true,
  name: true,
  profile: { select: { nickname: true } },
} satisfies Prisma.UserSelect;

export async function readFriendPage(database: Database, userId: string) {
  const connections = await database.friendConnection.findMany({
    where: { status: { in: ["PENDING", "ACCEPTED"] }, OR: [{ userAId: userId }, { userBId: userId }] },
    select: {
      userAId: true,
      userBId: true,
      requestedById: true,
      status: true,
      createdAt: true,
      userA: { select: connectionUser },
      userB: { select: connectionUser },
    },
    orderBy: { createdAt: "desc" },
  });
  return {
    friends: connections.filter(connection => connection.status === "ACCEPTED").map(connection => otherUser(connection, userId)),
    incoming: connections
      .filter(connection => connection.status === "PENDING" && connection.requestedById !== userId)
      .map(connection => ({ ...otherUser(connection, userId), createdAt: connection.createdAt.toISOString() })),
    outgoing: connections
      .filter(connection => connection.status === "PENDING" && connection.requestedById === userId)
      .map(connection => ({ ...otherUser(connection, userId), createdAt: connection.createdAt.toISOString() })),
  };
}

export async function searchFriendCandidates(database: Database, userId: string, nickname: string) {
  // Prisma uses PostgreSQL ILIKE for case-insensitive equality.
  const exactNickname = nickname.replace(/[\\%_]/g, "\\$&");
  const users = await database.user.findMany({
    where: {
      id: { not: userId },
      status: { not: "SUSPENDED" },
      deletedAt: null,
      profile: { is: { nickname: { equals: exactNickname, mode: "insensitive" } } },
    },
    select: { id: true, name: true, profile: { select: { nickname: true } } },
    orderBy: { createdAt: "asc" },
    take: 10,
  });
  const stateByUser = await readFriendConnectionStates(database, userId, users.map(user => user.id));
  return users.map(user => ({
    ...profile(user),
    connectionStatus: stateByUser.get(user.id) ?? "none",
  }));
}

export async function readFriendConnectionStates(database: Pick<Database, "friendConnection">, userId: string, targetIds: string[]) {
  const stateByUser = new Map<string, FriendConnectionState>();
  if (!targetIds.length) return stateByUser;
  const connections = await database.friendConnection.findMany({
    where: {
      status: { in: ["PENDING", "ACCEPTED"] },
      OR: [
        { userAId: userId, userBId: { in: targetIds } },
        { userBId: userId, userAId: { in: targetIds } },
      ],
    },
    select: { userAId: true, userBId: true, requestedById: true, status: true },
  });
  for (const connection of connections) {
    const targetId = connection.userAId === userId ? connection.userBId : connection.userAId;
    if (connection.status === "ACCEPTED") stateByUser.set(targetId, "friend");
    else if (connection.status === "PENDING") {
      stateByUser.set(targetId, connection.requestedById === userId ? "outgoing" : "incoming");
    }
  }
  return stateByUser;
}

export async function requestFriend(database: Database, userId: string, targetId: string) {
  const target = await database.user.findFirst({
    where: { id: targetId, status: { not: "SUSPENDED" }, deletedAt: null },
    select: { id: true },
  });
  if (!target) throw new FriendActionError("TARGET_UNAVAILABLE");

  const users = pair(userId, targetId);
  const renewed = await database.friendConnection.updateMany({
    where: { ...users, status: "DECLINED" },
    data: { status: "PENDING", requestedById: userId, createdAt: new Date() },
  });
  if (renewed.count === 1) return;

  try {
    await database.friendConnection.create({
      data: { ...users, requestedById: userId, status: "PENDING" },
    });
  } catch (error) {
    if (error && typeof error === "object" && "code" in error && error.code === "P2002") {
      throw new FriendActionError("CONNECTION_EXISTS");
    }
    throw error;
  }
}

export async function respondToFriendRequest(
  database: Database,
  userId: string,
  requesterId: string,
  response: "accept" | "decline",
) {
  const users = pair(userId, requesterId);
  const result = await database.friendConnection.updateMany({
    where: { ...users, status: "PENDING", requestedById: requesterId },
    data: { status: response === "accept" ? "ACCEPTED" : "DECLINED" },
  });
  if (result.count !== 1) throw new FriendActionError("REQUEST_NOT_FOUND");
}

export async function removeFriendConnection(database: Database, userId: string, targetId: string) {
  const users = pair(userId, targetId);
  const result = await database.friendConnection.deleteMany({
    where: {
      ...users,
      OR: [
        { status: "ACCEPTED" },
        { status: "PENDING", requestedById: userId },
      ],
    },
  });
  if (result.count !== 1) throw new FriendActionError("NOT_FRIENDS");
}
