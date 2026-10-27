# Next.js 更新記録（Issue #4）

2026-10-20、Next.js 14.2.35 から、現行の Active LTS である 16.4.2 へ更新した。[公式サポート方針](https://nextjs.org/support-policy) に沿い、今後も保守が続くバージョンを使用する。

- `next` / `@next/env` / `eslint-config-next` を 16.4.2 に統一。
- App Router の [公式移行手順](https://nextjs.org/docs/app/guides/upgrading/version-16) に合わせ、React / React DOM を 19.3.1、型定義を 19.3.0 へ更新。
- カスタム Webpack の監視対象除外を維持するため、開発・ビルドに `--webpack` を指定。ポーリングは `next.config.mjs` で指定し、Windows でも `npm run dev` を起動できるようにした。
- 廃止された `next lint` を ESLint 9 のCLIへ移行。
- 既存のページ離脱用リンクと旧広場のHook構造に対する11件は、対象ファイルに限定して警告扱いとし、Lint結果に表示する。その他は推奨ルールを適用する。
- Next.js が生成する `next-env.d.ts` と `tsconfig.json` のJSXランタイム・生成型の対象を更新。

Issue 対象の [Windows サーバーの脆弱性](https://github.com/vercel/next.js/security/advisories/GHSA-p293-qw3h-jr36) と [AVIF 画像処理の脆弱性](https://github.com/vercel/next.js/security/advisories/GHSA-2xp9-vwfh-vxw4) は、公式情報では16系は 16.3.3 以降が修正済み。

## 検証

Windows / Node.js 22.13.0 / Chrome で確認。

```powershell
npm.cmd test
npm.cmd run test:db
npm.cmd run lint
npm.cmd run build
npx.cmd tsc --noEmit --incremental false
npm.cmd audit --omit=dev
```

通常テスト44件・DBテスト11件、型検査、本番ビルドが成功。Lintはエラー0件・既存の警告11件。本番用依存関係の監査指摘は0件。

ブラウザーでの再確認はビルド後に実行する。既定では Playwright の Chromium を使用する。

認証接続後の現在のスイートはテスト専用DBも必要。先に `npm.cmd run db:test:up` と `npm.cmd run test:db` を実行する。認証追加後の対象・手順は [authentication.md](./authentication.md) を参照。

```powershell
npx.cmd playwright install chromium
npm.cmd run test:smoke
```

インストール済みの Chrome を使用する場合は、実行前に `PLAYWRIGHT_EXECUTABLE_PATH` を指定できる。

```powershell
$env:PLAYWRIGHT_EXECUTABLE_PATH = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
npm.cmd run test:smoke
```

本番サーバーを `127.0.0.1:3012` に起動し、既存の25件でTOP・登録・ログイン・メニュー・スクロール・ロゴ・Unityページへの遷移を確認する。実際の認証は未接続。Unity Webビルド本体の起動と従来の広場・通話はこのスイートの対象外。

## 残る監査指摘

全依存関係の `npm audit` では9件（high 7件・moderate 2件）が残る。いずれも開発用の Tailwind CSS / ESLint 関連の依存関係で、Next.js本体・React・本番用依存関係への指摘はない。Tailwind CSS 4 などへの移行は別の変更で対応する。監査全体が0件になったとは扱わない。
