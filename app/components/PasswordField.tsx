"use client";
import { useState } from "react";

export function PasswordField({ label, value, onChange, id, minLength }: { label: string; value: string; onChange: (value: string) => void; id: string; minLength?: number }) {
  const [visible, setVisible] = useState(false);
  return <div className="form-field"><label htmlFor={id}>{label}</label><div className="relative">
    <input id={id} type={visible ? "text" : "password"} value={value} onChange={event => onChange(event.target.value)} required minLength={minLength} maxLength={128} autoComplete={id === "login-password" ? "current-password" : "new-password"} className="!pr-20" />
    <button type="button" className="absolute right-2 top-1/2 min-h-11 -translate-y-1/2 rounded-lg px-3 text-sm font-bold text-blue-800 hover:bg-blue-50" aria-label={`${label}を${visible ? "非表示" : "表示"}`} aria-pressed={visible} onClick={() => setVisible(!visible)}>{visible ? "隠す" : "表示"}</button>
  </div></div>;
}
