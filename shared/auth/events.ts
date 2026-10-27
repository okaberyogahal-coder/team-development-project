// Only invalidate other tabs' views; they must fetch the server's current session.
export function notifyAuthChanged() {
  if (typeof BroadcastChannel === "undefined") return;
  try {
    const channel = new BroadcastChannel("melife-auth");
    channel.postMessage("changed");
    channel.close();
  } catch {
    // Focus/visibility and periodic checks cover browsers without channel support.
  }
}
