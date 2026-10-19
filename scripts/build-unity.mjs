import { existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = fileURLToPath(new URL('../', import.meta.url));
const unity = process.env.UNITY_EDITOR_PATH || '/Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app/Contents/MacOS/Unity';
if (!existsSync(unity)) {
  console.error('Unity 6000.4.2f1 が見つかりません。UNITY_EDITOR_PATH に Unity 実行ファイルのパスを指定してください。');
  process.exit(1);
}
const result = spawnSync(unity, [
  '-batchmode', '-nographics', '-quit', '-buildTarget', 'WebGL',
  '-projectPath', path.join(root, 'unity/FutureCity'),
  '-executeMethod', 'FutureCity.EditorTools.FutureCityWebBuild.Build',
  '-logFile', '-',
], { stdio: 'inherit' });
if (result.error) console.error(result.error.message);
process.exit(result.status ?? 1);
