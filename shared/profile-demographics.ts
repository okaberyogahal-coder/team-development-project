import {AuthInputError} from './auth/validation';

export type Gender = 'male' | 'female';
export type AgeBadge = 'ユース' | 'ミドル' | 'ミドルシニア' | 'シニア';
export function worldProfileReady(profile: {gender?:unknown;age?:unknown} | null | undefined): boolean {
 return !!profile && (profile.gender === 'male' || profile.gender === 'female') && ageBadge(profile.age) !== null;
}
export function ageBadge(age: unknown): AgeBadge | null {
 if(typeof age !== 'number' || !Number.isInteger(age) || age < 18 || age > 130) return null;
 return age < 30 ? 'ユース' : age < 50 ? 'ミドル' : age < 60 ? 'ミドルシニア' : 'シニア';
}
export function demographicInput(data: {gender?:unknown;age?:unknown}): {gender:Gender;age:number} {
 if(data.gender !== 'male' && data.gender !== 'female') throw new AuthInputError('性別を選択してください。');
 if(!ageBadge(data.age)) throw new AuthInputError('年齢は18〜130歳の整数で入力してください。');
 return {gender:data.gender,age:data.age as number};
}
