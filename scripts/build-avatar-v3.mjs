import {spawnSync} from 'node:child_process';
import {existsSync,readFileSync} from 'node:fs';
import path from 'node:path';
const version=readFileSync('unity/FutureCity/ProjectSettings/ProjectVersion.txt','utf8').match(/^m_EditorVersion:\s*(\S+)/m)?.[1];
const editor=process.env.UNITY_EDITOR_PATH||(process.platform==='win32'?path.join(process.env.ProgramFiles||'C:\\Program Files','Unity','Hub','Editor',version,'Editor','Unity.exe'):`/Applications/Unity/Hub/Editor/${version}/Unity.app/Contents/MacOS/Unity`);
if(!existsSync(editor))throw new Error('Matching Unity Editor is required. Set UNITY_EDITOR_PATH.');
const result=spawnSync(editor,['-batchmode','-nographics','-quit','-buildTarget','WebGL','-projectPath',path.resolve('unity/FutureCity'),'-executeMethod','FutureCity.EditorTools.AvatarV3Build.BuildPreview','-logFile','-'],{stdio:'inherit'});
process.exit(result.status??1);
