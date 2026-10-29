import type {PrismaClient,Prisma} from '../../generated/prisma/client';
import type {AccountProfile} from '../../shared/account-profile';
import {ProfileAccessError,ProfileConflictError} from './profile-handler';
export async function readAccountProfile(database:Pick<Prisma.TransactionClient,'user'>,userId:string):Promise<AccountProfile & {ownerId:string}>{
 const user=await database.user.findUnique({where:{id:userId},select:{name:true,profile:true,preferences:true}});
 if(!user)throw new ProfileAccessError();
 return {ownerId:userId,nickname:user.profile?.nickname||user.name||'利用者',interests:user.preferences?.interests||[],wantsToLearn:user.profile?.wantsToLearn||[],teaches:user.profile?.teaches||[],strengths:user.preferences?.strengths||'',goal:user.preferences?.goal||'',profileVersion:user.profile?.version||0,preferencesVersion:user.preferences?.version||0};
}
export async function saveAccountProfile(database:PrismaClient,userId:string,input:AccountProfile):Promise<AccountProfile & {ownerId:string}>{
 try{
  return await database.$transaction(async tx=>{
   // Lock and validate the active account before writing the two versioned records.
   const user=await tx.user.updateMany({where:{id:userId,deletedAt:null,status:{not:'SUSPENDED'}},data:{name:input.nickname}});
   if(user.count!==1)throw new ProfileAccessError();
   const profile={nickname:input.nickname,teaches:input.teaches,wantsToLearn:input.wantsToLearn};
   if(input.profileVersion===0)await tx.userProfile.create({data:{userId,...profile}});
   else{
    const result=await tx.userProfile.updateMany({where:{userId,version:input.profileVersion},data:{...profile,version:{increment:1}}});
    if(result.count!==1)throw new ProfileConflictError();
   }
   const preferences={interests:input.interests,strengths:input.strengths,goal:input.goal};
   if(input.preferencesVersion===0)await tx.learningPreferences.create({data:{userId,...preferences}});
   else{
    const result=await tx.learningPreferences.updateMany({where:{userId,version:input.preferencesVersion},data:{...preferences,version:{increment:1}}});
    if(result.count!==1)throw new ProfileConflictError();
   }
   return readAccountProfile(tx,userId);
  });
 }catch(error){
  // Concurrent creation of a previously absent record is also a stale-edit conflict.
  if(error&&typeof error==='object'&&'code' in error&&error.code==='P2002')throw new ProfileConflictError();
  throw error;
 }
}
