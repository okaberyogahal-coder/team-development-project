"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { notifyAuthChanged } from "../../shared/auth/events";

export function LogoutButton() {
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");
  const router = useRouter();
  return <div>
    <button type="button" className="secondary-button" disabled={pending} aria-busy={pending} onClick={async () => {
      setPending(true); setError("");
      try {
        const response = await fetch("/api/auth/sign-out", { method: "POST", headers: { "Content-Type": "application/json" }, body: "{}" });
        if (!response.ok) throw new Error();
        notifyAuthChanged();
        router.replace("/login"); router.refresh();
      } catch { setError("ログアウトできませんでした。通信状態を確認してお試しください。"); }
      finally { setPending(false); }
    }}>{pending ? "ログアウト中…" : "ログアウト"}</button>
    {error && <p role="alert" className="form-alert">{error}</p>}
  </div>;
}
