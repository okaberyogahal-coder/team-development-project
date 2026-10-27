import type { Metadata } from "next";
import { AuthShell } from "../components/AuthShell";
import { SignupForm } from "../components/SignupForm";
import { getCurrentUser } from "../../server/auth/server";
import { redirect } from "next/navigation";
export const metadata: Metadata = { title: "アカウント作成 | ミライフ ME LIFE" };
export default async function SignupPage() { if (await getCurrentUser()) redirect("/account"); return <AuthShell><SignupForm /></AuthShell>; }
