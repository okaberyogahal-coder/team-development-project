import { getCurrentUser } from "../../../server/auth/server";
import { allowAuthAttempt } from "../../../server/auth/http";
import { getDatabase } from "../../../server/db/client";
import { createFriendHandlers } from "../../../server/friends/handler";
import {
  readFriendPage,
  removeFriendConnection,
  requestFriend,
  respondToFriendRequest,
  searchFriendCandidates,
} from "../../../server/friends/store";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const handlers = createFriendHandlers({
  getUser: getCurrentUser,
  read: userId => readFriendPage(getDatabase(), userId),
  search: (userId, nickname) => searchFriendCandidates(getDatabase(), userId, nickname),
  request: (userId, targetId) => requestFriend(getDatabase(), userId, targetId),
  respond: (userId, requesterId, response) => respondToFriendRequest(getDatabase(), userId, requesterId, response),
  remove: (userId, targetId) => removeFriendConnection(getDatabase(), userId, targetId),
  allow: (userId, action) => allowAuthAttempt(getDatabase(), `friend-${action}`, userId, action === "search" ? 30 : 20),
});

export const { GET, POST, PATCH, DELETE } = handlers;
