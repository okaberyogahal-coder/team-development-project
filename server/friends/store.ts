import "server-only";
import type { PrismaClient, Prisma } from "../../generated/prisma/client";
import type { FriendConnectionStatus } from "../../generated/prisma/client";
import { FriendActionError } from "./errors";

type Database = Pick<PrismaClient, "friendConnection" | "user" | "$transaction">;
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
  const users = await database.user.findMany({
    where: {
      id: { not: userId },
      status: { not: "SUSPENDED" },
      deletedAt: null,
      profile: { is: { nickname: { equals: nickname, mode: "insensitive" } } },
    },
    select: { id: true, name: true, profile: { select: { nickname: true } } },
    orderBy: { createdAt: "asc" },
    take: 10,
  });
  const connections = await database.friendConnection.findMany({
    where: { OR: [{ userAId: userId }, { userBId: userId }] },
    select: { userAId: true, userBId: true, requestedById: true, status: true },
  });
  const stateByUser = new Map<string, "friend" | "incoming" | "outgoing">();
  for (const connection of connections) {
    const targetId = connection.userAId === userId ? connection.userBId : connection.userAId;
    if (connection.status === "ACCEPTED") stateByUser.set(targetId, "friend");
    else if (connection.status === "PENDING") {
      stateByUser.set(targetId, connection.requestedById === userId ? "outgoing" : "incoming");
    }
  }
  return users.map(user => ({
    ...profile(user),
    connectionStatus: stateByUser.get(user.id) ?? "none",
  }));
}

export async function requestFriend(database: Database, userId: string, targetId: string) {
  const target = await database.user.findFirst({
    where: { id: targetId, status: { not: "SUSPENDED" }, deletedAt: null },
    select: { id: true },
  });
  if (!target) throw new FriendActionError("TARGET_UNAVAILABLE");

  const users = pair(userId, targetId);
  await database.$transaction(async tx => {
    const existing = await tx.friendConnection.findUnique({ where: { userAId_userBId: users }, select: { status: true } });
    if (existing?.status === "ACCEPTED" || existing?.status === "PENDING") {
      throw new FriendActionError("CONNECTION_EXISTS");
    }
    if (existing) {
      await tx.friendConnection.update({
        where: { userAId_userBId: users },
        data: { status: "PENDING", requestedById: userId },
      });
    } else {
      await tx.friendConnection.create({
        data: { ...users, requestedById: userId, status: "PENDING" },
      });
    }
  });
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
