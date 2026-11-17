import type {PublicLesson,TutorReply} from '../../shared/ai-instructor';
import {InstructorError} from './store';
export function validateTutorReply(value:unknown,lesson:PublicLesson):TutorReply{
 const bad=()=>{throw new InstructorError('INVALID_OUTPUT');};
 if(!value||typeof value!=='object'||Array.isArray(value))return bad();
 const v=value as Record<string,unknown>;
 if(Object.keys(v).sort().join(',')!=='kind,sectionIds,text'||(v.kind!=='answer'&&v.kind!=='out_of_scope')||typeof v.text!=='string'||!v.text.trim()||[...v.text].length>2000||!Array.isArray(v.sectionIds)||v.sectionIds.length>lesson.sections.length)return bad();
 if(!v.sectionIds.every(id=>typeof id==='string'&&lesson.sections.some(s=>s.id===id))||new Set(v.sectionIds).size!==v.sectionIds.length)return bad();
 if(v.kind==='answer'?!v.sectionIds.length:!!v.sectionIds.length)return bad();
 return {kind:v.kind as TutorReply['kind'],text:v.text.trim(),sectionIds:v.sectionIds as string[]};
}
