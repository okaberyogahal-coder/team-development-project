"use client";
import Link from 'next/link';
import {useEffect,useRef,useState} from 'react';
import {useRouter} from 'next/navigation';
import {profileEditInput,type AccountProfile,type ProfileFields} from '../../shared/account-profile';
import {notifyAuthChanged} from '../../shared/auth/events';

type Draft=ProfileFields;
type Editor={draft:Draft;profileVersion:number;preferencesVersion:number;ownerId:string};
const tagFields=[['interests','興味のある分野'],['wantsToLearn','学びたいこと'],['teaches','教えられること']] as const;
export function ProfileEditForm(){
 const router=useRouter();
 const [editor,setEditor]=useState<Editor|null>(null);
 const [loading,setLoading]=useState(true);
 const [attempt,setAttempt]=useState(0);
 const [busy,setBusy]=useState(false);
 const [error,setError]=useState('');
 const [conflict,setConflict]=useState(false);
 const [needsLogin,setNeedsLogin]=useState(false);
 const pending=useRef(false);
 useEffect(()=>{
  const controller=new AbortController();
  async function load(){
   try{
    const response=await fetch('/api/account/profile',{cache:'no-store',signal:controller.signal});
    if(response.status===401){router.replace('/login');return;}
    if(!response.ok)throw new Error('プロフィールを読み込めませんでした。');
    const body=await response.json() as AccountProfile&{ownerId:string};
    const data=profileEditInput(Object.fromEntries(Object.entries(body).filter(([key])=>key!=='ownerId')));
    if(typeof body.ownerId!=='string'||!body.ownerId)throw new Error('プロフィールを確認できませんでした。');
    if(controller.signal.aborted)return;
    setEditor({ownerId:body.ownerId,profileVersion:data.profileVersion,preferencesVersion:data.preferencesVersion,draft:{nickname:data.nickname,interests:data.interests.length?data.interests:[''],wantsToLearn:data.wantsToLearn.length?data.wantsToLearn:[''],teaches:data.teaches.length?data.teaches:[''],strengths:data.strengths,goal:data.goal}});
    setError('');
   }catch{if(!controller.signal.aborted)setError('プロフィールを読み込めませんでした。もう一度お試しください。');}
   finally{if(!controller.signal.aborted)setLoading(false);}
  }
  void load();return()=>controller.abort();
 },[attempt,router]);
 function change<K extends keyof Draft>(key:K,value:Draft[K]){setEditor(current=>current?{...current,draft:{...current.draft,[key]:value}}:null);}
 async function save(event:React.FormEvent<HTMLFormElement>){
  event.preventDefault();if(!editor||pending.current)return;
  setError('');setConflict(false);setNeedsLogin(false);
  const tags=(value:string[])=>value.map(x=>x.trim()).filter(Boolean);
  let input:AccountProfile;
  try{input=profileEditInput({...editor.draft,interests:tags(editor.draft.interests),wantsToLearn:tags(editor.draft.wantsToLearn),teaches:tags(editor.draft.teaches),profileVersion:editor.profileVersion,preferencesVersion:editor.preferencesVersion});}
  catch(error){setError(error instanceof Error?error.message:'入力内容を確認してください。');return;}
  pending.current=true;setBusy(true);
  try{
   const response=await fetch('/api/account/profile',{method:'PATCH',headers:{'Content-Type':'application/json','X-Profile-Owner':editor.ownerId},body:JSON.stringify(input)});
   if(!response.ok){
    const data=await response.json();
    setConflict(response.status===409);setNeedsLogin(response.status===401);
    setError(data.message||'保存できませんでした。入力内容は残しています。');return;
   }
   notifyAuthChanged();router.replace('/account?updated=1');router.refresh();
  }catch{setError('通信できませんでした。入力内容は残しています。もう一度お試しください。');}
  finally{pending.current=false;setBusy(false);}
 }
 return <section className="auth-form-screen account-screen profile-edit-screen">
  <div className="auth-form-title"><p className="auth-title-kicker">あなたの「好き」を、今のあなたに。</p><h1 className="form-heading">プロフィールを編集</h1><p className="auth-form-description">表示名・学びたいこと・教えられることはワールドにも反映されます。</p></div>
  {loading&&<p role="status">プロフィールを読み込んでいます…</p>}
  {error&&<p role="alert" className="form-alert">{error}</p>}
  {!loading&&!editor&&<button type="button" className="secondary-button" onClick={()=>{setLoading(true);setAttempt(x=>x+1);}}>読み込みをやり直す</button>}
  {editor&&<form onSubmit={save} aria-busy={busy}>
   <fieldset disabled={busy} className="profile-edit-fields">
    <legend className="sr-only">編集するプロフィール</legend>
    <div className="form-field"><label htmlFor="profile-nickname">ニックネーム（必須）</label><input id="profile-nickname" autoComplete="nickname" required maxLength={20} value={editor.draft.nickname} onChange={event=>change('nickname',event.target.value)}/><span className="auth-field-hint">20文字以内</span></div>
    {tagFields.map(([key,label])=><fieldset className="profile-topic-group" key={key}>
     <legend>{label}</legend>
     {editor.draft[key].map((value,index)=><div className="profile-topic-row form-field" key={index}>
      <label className="sr-only" htmlFor={`profile-${key}-${index}`}>{index===0?label:`${label} ${index+1}`}</label>
      <textarea rows={1} id={`profile-${key}-${index}`} maxLength={30} value={value} onChange={event=>change(key,editor.draft[key].map((item,i)=>i===index?event.target.value:item))} aria-describedby={`profile-${key}-hint`}/>
      <button type="button" className="profile-topic-remove" aria-label={`${label} ${index+1}を削除`} onClick={()=>change(key,editor.draft[key].length===1?['']:editor.draft[key].filter((_,i)=>i!==index))}>削除</button>
     </div>)}
     <p className="auth-field-hint" id={`profile-${key}-hint`}>1項目ずつ入力してください（30文字・10項目以内）。空欄は保存されません。</p>
     <button type="button" className="auth-back-button" disabled={editor.draft[key].length>=10} onClick={()=>change(key,[...editor.draft[key],''])}>{label}を追加</button>
    </fieldset>)}
    <div className="form-field"><label htmlFor="profile-strengths">得意なこと（本人のみ）</label><textarea id="profile-strengths" rows={3} maxLength={500} value={editor.draft.strengths} onChange={event=>change('strengths',event.target.value)}/></div>
    <div className="form-field"><label htmlFor="profile-goal">目標（本人のみ）</label><textarea id="profile-goal" rows={3} maxLength={500} value={editor.draft.goal} onChange={event=>change('goal',event.target.value)}/><span className="auth-field-hint">それぞれ500文字以内</span></div>
   </fieldset>
   <p className="auth-demo-note">衣装はアバター設定画面から変更できます。</p>
   {conflict&&<p><button type="button" className="auth-back-button" onClick={()=>window.location.reload()}>入力内容を破棄して最新情報を読み込む</button></p>}
   {needsLogin&&<p><Link href="/login" target="_blank" rel="noopener noreferrer">別タブでログインする</Link>と、入力内容を残して再試行できます。</p>}
   <div className="account-actions"><button type="submit" className="primary-button" disabled={busy||conflict}>{busy?'保存中…':'変更を保存'}</button><Link className="secondary-button" href="/account">キャンセル</Link></div>
  </form>}
 </section>;
}
