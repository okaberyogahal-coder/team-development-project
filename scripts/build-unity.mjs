import { existsSync, readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = fileURLToPath(new URL('../', import.meta.url));
const version = readFileSync(path.join(root, 'unity/FutureCity/ProjectSettings/ProjectVersion.txt'), 'utf8')
  .match(/^m_EditorVersion:\s*(\S+)/m)?.[1];
if (!version) throw new Error('Unity のプロジェクトバージョンを確認できません。');
const defaultEditor = process.platform === 'win32'
  ? path.join(process.env.ProgramFiles || 'C:\\Program Files', 'Unity', 'Hub', 'Editor', version, 'Editor', 'Unity.exe')
  : `/Applications/Unity/Hub/Editor/${version}/Unity.app/Contents/MacOS/Unity`;
const unity = process.env.UNITY_EDITOR_PATH || defaultEditor;
if (!existsSync(unity)) {
  console.error(`Unity ${version} が見つかりません。UNITY_EDITOR_PATH に Unity 実行ファイルのパスを指定してください。`);
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
