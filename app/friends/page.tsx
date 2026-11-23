import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { getCurrentUser } from "../../server/auth/server";
import { getDatabase } from "../../server/db/client";
import { readFriendPage } from "../../server/friends/store";
import { FriendsClient } from "./FriendsClient";
import "./friends.css";

export const metadata: Metadata = {
  title: "フレンド | ミライフ",
  robots: { index: false, follow: false },
};

export default async function FriendsPage() {
  const user = await getCurrentUser();
  if (!user) redirect("/login");
  const friends = await readFriendPage(getDatabase(), user.id);
  return <FriendsClient nickname={user.profile?.nickname || "利用者"} initialData={{ ...friends, matches: [] }} />;
}
