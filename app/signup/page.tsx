import type { Metadata } from "next";
import { AuthShell } from "../components/AuthShell";
import { SignupForm } from "../components/SignupForm";
export const metadata: Metadata = { title: "アカウント作成 | ミライフ ME LIFE" };
export default function SignupPage() { return <AuthShell><SignupForm /></AuthShell>; }
