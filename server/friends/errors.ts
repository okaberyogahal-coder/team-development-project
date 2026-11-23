export class FriendActionError extends Error {
  constructor(readonly code: "TARGET_UNAVAILABLE" | "CONNECTION_EXISTS" | "REQUEST_NOT_FOUND" | "NOT_FRIENDS") {
    super(code);
  }
}
