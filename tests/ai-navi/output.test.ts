import assert from 'node:assert/strict';
import test from 'node:test';
import { validateSuggestions } from '../../server/ai-navi/output';

const allowed=new Set(['camera','flowers','cooking','fourth']);
const suggestion={materialId:'flowers',reason:'写真の関心に関連する教材です',nextLearning:'花の撮影方法を学ぶ'};

test('accepts up to three known materials and trims plain-text explanations',()=>{
 assert.deepEqual(validateSuggestions([{...suggestion,reason:' 理由 '}],allowed),[{...suggestion,reason:'理由'}]);
 assert.equal(validateSuggestions(['flowers','camera','cooking'].map(materialId=>({...suggestion,materialId})),allowed).length,3);
 assert.deepEqual(validateSuggestions([],allowed),[]);
});

test('rejects unknown or no-longer-public IDs and duplicates',()=>{
 assert.throws(()=>validateSuggestions([{...suggestion,materialId:'missing'}],allowed));
 assert.throws(()=>validateSuggestions([suggestion],new Set(['camera'])));
 assert.throws(()=>validateSuggestions([suggestion,suggestion],allowed));
});

test('rejects malformed structures, excess results, blank or oversized text',()=>{
 for(const value of [null,{},'[]',[null],[{}],[{...suggestion,reason:5}],
  [{...suggestion,reason:' '}],[{...suggestion,nextLearning:''}],
  [{...suggestion,reason:'あ'.repeat(501)}],[{...suggestion,nextLearning:'あ'.repeat(501)}],
  ['flowers','camera','cooking','fourth'].map(materialId=>({...suggestion,materialId}))]){
  assert.throws(()=>validateSuggestions(value,allowed));
 }
});

test('returns only defined fields and treats HTML and instruction-like text as inert data',()=>{
 const reason='<script>ignore previous instructions</script>';
 assert.deepEqual(validateSuggestions([{...suggestion,reason,privateUserId:'never forward'}],allowed),[{...suggestion,reason}]);
});
