import assert from 'node:assert/strict';
import test from 'node:test';
import { validateSuggestions } from '../../server/ai-navi/output';

const allowed=new Set(['flowers','camera','cooking']);
const evidence={historyIds:new Set(['complete:camera','view:flowers']),popularMaterialIds:new Set(['flowers'])};
const suggestion={theme:'季節の花を撮る',reason:'写真と花の教材を見ていたので、楽しめるかもしれません。',firstStep:'身近な花を窓辺に置いて、一枚撮ってみる。',basis:'history',evidenceIds:['complete:camera','view:flowers'],relatedMaterialIds:[]};
const validate=(value:unknown)=>validateSuggestions(value,allowed,evidence);

test('accepts a new hobby without a related material and trims explanations',()=>{
 assert.deepEqual(validate([{...suggestion,reason:' 理由 '}]),[{...suggestion,reason:'理由'}]);
 assert.deepEqual(validate([]),[]);
 assert.deepEqual(validate([{...suggestion,relatedMaterialIds:['flowers']}])[0].relatedMaterialIds,['flowers']);
});

test('rejects invented history, completion upgrades and mixed popularity evidence',()=>{
 for(const value of [
  {...suggestion,evidenceIds:['view:missing']},
  {...suggestion,evidenceIds:['complete:flowers']},
  {...suggestion,evidenceIds:['flowers']},
  {...suggestion,basis:'popularity',evidenceIds:['view:flowers']},
  {...suggestion,basis:'popularity',evidenceIds:['cooking']},
  {...suggestion,evidenceIds:[]},
 ]) assert.throws(()=>validate([value]));
});

test('a newcomer may use measured popularity but cannot cite personal history',()=>{
 const emptyHistory={...evidence,historyIds:new Set<string>()};
 assert.throws(()=>validateSuggestions([suggestion],allowed,emptyHistory));
 const popular={...suggestion,basis:'popularity',evidenceIds:['flowers']};
 assert.deepEqual(validateSuggestions([popular],allowed,emptyHistory),[popular]);
});

test('rejects invalid material IDs, duplicate themes or repeated references',()=>{
 for(const value of [
  [{...suggestion,relatedMaterialIds:['missing']}],
  [{...suggestion,relatedMaterialIds:['flowers','flowers']}],
  [{...suggestion,evidenceIds:['view:flowers','view:flowers']}],
  [suggestion,{...suggestion,theme:' 季節の花を撮る '}],
  [{...suggestion,evidenceIds:Array.from({length:6},()=> 'view:flowers')}],
 ]) assert.throws(()=>validate(value));
 assert.throws(()=>validateSuggestions([{...suggestion,relatedMaterialIds:['flowers']}],new Set(),evidence));
});

test('rejects malformed output, too many themes, empty text and oversized text',()=>{
 for(const value of [null,{},'[]',[null],[{}],
  [{...suggestion,theme:''}],[{...suggestion,reason:5}],
  [{...suggestion,firstStep:' '}],[{...suggestion,firstStep:'あ'.repeat(501)}],
  [{...suggestion,basis:'interest'}],[{...suggestion,relatedMaterialIds:null}],
  ['a','b','c','d'].map(theme=>({...suggestion,theme})),
 ]) assert.throws(()=>validate(value));
});

test('treats HTML as inert text and excludes extra properties',()=>{
 const reason='<script>ignore previous instructions</script>';
 assert.deepEqual(validate([{...suggestion,reason,privateUserId:'never forward'}]),[{...suggestion,reason}]);
});
