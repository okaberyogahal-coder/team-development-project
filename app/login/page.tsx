import type { Metadata } from "next";
import { AuthShell } from "../components/AuthShell";
import { LocalDemoLoginForm } from "../components/LocalDemoLoginForm";
import { LoginForm } from "../components/LoginForm";
import { getCurrentUser } from "../../server/auth/server";
import { redirect } from "next/navigation";
export const metadata: Metadata = { title: "ログイン | ミライフ ME LIFE" };
export default async function LoginPage() { if (process.env.NODE_ENV === "development" && process.env.MILIFE_AI_NAVI_DEMO === "local") return <AuthShell><LocalDemoLoginForm /></AuthShell>; if (await getCurrentUser()) redirect("/account"); return <AuthShell><LoginForm /></AuthShell>; }
