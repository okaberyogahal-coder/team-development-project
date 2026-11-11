import type { Suggestion } from './types';

function text(value: unknown): string {
  if (typeof value !== 'string') throw new Error('Invalid suggestion text');
  const result = value.trim();
  if (!result || result.length > 500) throw new Error('Invalid suggestion text length');
  return result;
}
function references(value:unknown, allowed:ReadonlySet<string>, required:boolean):string[]{
  if(!Array.isArray(value) || value.length>5 || (required && !value.length)) throw new Error('Invalid suggestion references');
  const ids=value.map(text);
  if(new Set(ids).size!==ids.length || ids.some(id=>!allowed.has(id))) throw new Error('Invalid suggestion reference');
  return ids;
}
/** IDs prove recorded evidence exists; free-text semantic claims require model quality evaluation. */
export function validateSuggestions(
  value:unknown, allowedIds:ReadonlySet<string>,
  evidence:{historyIds:ReadonlySet<string>;popularMaterialIds:ReadonlySet<string>},
):Suggestion[]{
  if(!Array.isArray(value) || value.length>3) throw new Error('Invalid suggestions array');
  const themes=new Set<string>();
  return value.map(item=>{
    if(!item || typeof item!=='object' || Array.isArray(item)) throw new Error('Invalid suggestion');
    const theme=text(item.theme);
    const key=theme.normalize('NFKC').toLowerCase();
    if(themes.has(key)) throw new Error('Invalid suggestion duplicate theme');
    themes.add(key);
    if(item.basis!=='history' && item.basis!=='popularity') throw new Error('Invalid suggestion basis');
    return {
      theme,reason:text(item.reason),firstStep:text(item.firstStep),basis:item.basis,
      evidenceIds:references(item.evidenceIds,item.basis==='history'?evidence.historyIds:evidence.popularMaterialIds,true),
      relatedMaterialIds:references(item.relatedMaterialIds,allowedIds,false),
    };
  });
}
