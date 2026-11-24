import {test} from 'node:test';
import assert from 'node:assert/strict';
import {getPublicLesson,getPublicQuestions,gradeQuestion} from '../../server/ai-instructor/material';

test('lesson supports three questions with real review sections and no answer disclosure',()=>{
 const lesson=getPublicLesson('natural-light')!;
 assert.equal(lesson.sample,true);
 assert.ok(lesson.sections.map(s=>s.body).join('').length<=12000);
 const questions=getPublicQuestions(lesson.id);
 assert.equal(questions.length,3);
 for(const [index,q] of questions.entries()){
  assert.equal(q.choices.length,3);
  assert.deepEqual(Object.keys(q).sort(),['choices','id','prompt']);
  const answers=q.choices.map((_,choice)=>gradeQuestion(lesson.id,q.id,choice));
  assert.deepEqual(answers.map(a=>a.correct),[[true,false,false],[false,true,false],[false,false,true]][index]);
  for(const a of answers)assert.ok(lesson.sections.some(s=>s.id===a.sectionId));
 }
 assert.equal(getPublicLesson('missing'),null);
 assert.throws(()=>gradeQuestion(lesson.id,'missing',0));
 assert.throws(()=>gradeQuestion(lesson.id,questions[0].id,3));
 assert.throws(()=>gradeQuestion(lesson.id,questions[0].id,0.5));
});
