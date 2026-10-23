import assert from 'node:assert/strict';
import test from 'node:test';
import { aggregatePopularity } from '../../server/ai-navi/popularity';
import type { LearningEvent, Material } from '../../server/ai-navi/types';

const now = Date.UTC(2026, 9, 23);
const start = now - 30 * 86400000;
const materials: Material[] = ['flowers', 'camera', 'empty'].map(id => ({id, title:id, description:'', topics:[], published:true}));
const event = (userId:string, materialId:string, kind: 'view'|'complete' = 'view', at=now): LearningEvent => ({userId, materialId, kind, at});

test('80% is popular while 60% is not, counting a person only once across views and completions', () => {
 const events = [event('1','flowers'), event('1','flowers'), event('1','flowers','complete'), event('2','flowers'), event('3','flowers'), event('4','flowers'), event('1','camera'), event('2','camera'), event('5','camera')];
 const result = aggregatePopularity(materials, events, now);
 assert.equal(result.activeUsers, 5);
 assert.deepEqual(result.items.find(x => x.materialId==='flowers'), {materialId:'flowers',users:4,completedUsers:1,ratio:0.8,popular:true});
 assert.deepEqual(result.items.find(x => x.materialId==='camera'), {materialId:'camera',users:3,completedUsers:0,ratio:0.6,popular:false});
});

test('counts only public materials and the last 30 days, excluding future and invalid timestamps', () => {
 const result = aggregatePopularity([...materials, {...materials[0],id:'private',published:false}], [event('1','flowers','view',start+1),event('2','camera','view',start),event('3','camera','view',now+1),event('4','private'),event('5','missing'),event('6','camera','view',NaN)], now);
 assert.equal(result.activeUsers,1);
 assert.equal(result.items.length,3);
 assert.equal(result.items.find(x=>x.materialId==='flowers')?.users,1);
 assert.equal(result.items.find(x=>x.materialId==='camera')?.users,0);
 assert.equal(result.start,start);
 assert.equal(result.end,now);
});

test('no users produces null ratios, not invented popularity', () => {
 const result = aggregatePopularity(materials, [], now);
 assert.equal(result.activeUsers,0);
 assert.ok(result.items.every(x=>x.ratio===null && !x.popular));
});

test('sorts by distinct completed users, then distinct users, then stable material ID without mutating inputs', () => {
 const events = [event('1','camera'),event('2','camera'),event('1','flowers','complete'),event('1','flowers','complete')];
 const before = structuredClone(events);
 assert.deepEqual(aggregatePopularity(materials,events,now).items.map(x=>x.materialId),['flowers','camera','empty']);
 assert.deepEqual(events,before);
 assert.throws(()=>aggregatePopularity(materials,events,NaN));
});
